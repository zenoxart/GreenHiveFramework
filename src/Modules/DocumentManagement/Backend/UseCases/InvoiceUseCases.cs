using System.Text.Json.Nodes;
using GreenHive.NET.Modules.DocumentManagement.Contracts;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Events;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Contracts;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.NET.Modules.DocumentManagement.Backend.UseCases;

/// <summary>Captures a new invoice as draft.</summary>
public sealed class CreateInvoice(DocumentRepository repository, Db db, AuditLog audit, RequestContext context)
{
    public Task<Result<InvoiceDto>> ExecuteAsync(CreateInvoiceRequest request, CancellationToken ct) =>
        Steps.Validate(request, r => Validation.Combine(
                Validation.RequiredMax("number", r.Number, 40),
                Validation.RequiredMax("supplier", r.Supplier, 200),
                Validation.Range("amount", r.Amount, 0.01m, 10_000_000m),
                Validation.RequiredMax("costCenter", r.CostCenter, 20),
                Validation.Valid(r).Ensure(x => x.DueOn >= x.IssuedOn, Error.Field("dueOn", "documents.invoice.dueBeforeIssue", "documents:invoice.dueBeforeIssue")),
                (number, supplier, amount, costCenter, valid) => new InvoiceDto(Guid.NewGuid(), number, supplier, amount,
                    string.IsNullOrWhiteSpace(valid.Currency) ? "EUR" : valid.Currency.ToUpperInvariant(), InvoiceStatus.Draft)
                {
                    IssuedOn = valid.IssuedOn,
                    DueOn = valid.DueOn,
                    CostCenter = costCenter,
                    DocumentId = valid.DocumentId,
                }))
            .Authorize(context.User, DocumentPermissions.Edit)
            .BindAsync(invoice => db.InTransactionAsync(session =>
                repository.InsertInvoiceAsync(session, invoice, context.User.TenantId, ct)
                    .BindAsync(_ => audit.WriteAsync(session, context.User, "invoice.created", "invoice", invoice.Id.ToString(), ct: ct))
                    .Map(_ => invoice), ct: ct));
}

/// <summary>Submits a draft invoice for approval - fires documents.invoice.submitted (starts the approval workflow).</summary>
public sealed class SubmitInvoice(DocumentRepository repository, Db db, OutboxWriter outbox, AuditLog audit, RequestContext context)
{
    public Task<Result<InvoiceDto>> ExecuteAsync(Guid invoiceId, CancellationToken ct) =>
        Steps.Authorize(context.User, DocumentPermissions.Edit)
            .BindAsync(_ => db.InTransactionAsync(session =>
                repository.FindInvoiceAsync(session, invoiceId, ct)
                    .Bind(invoice => Decide(invoice, context).Map(submitted => (Before: invoice, After: submitted)))
                    .BindAsync(x => repository.UpdateInvoiceAsync(session, x.After, x.Before.RowVersion, ct).Map(_ => x.After with { RowVersion = x.Before.RowVersion + 1 }))
                    .TapAsync(invoice => outbox.AddAsync(session, outbox.Create(EventTypes.InvoiceSubmitted, DecideInvoice.Payload(invoice),
                        NotificationAudience.WithPermission(DocumentPermissions.Approve)) with
                    {
                        Title = "documents:notification.invoiceSubmitted",
                        Message = $"{invoice.Number} - {invoice.Supplier} ({invoice.Amount:N2} {invoice.Currency})",
                        NavigationTarget = "documents.invoices",
                        NavigationParameter = invoice.Id.ToString(),
                    }, ct))
                    .TapAsync(invoice => audit.WriteAsync(session, context.User, "invoice.submitted", "invoice", invoice.Id.ToString(), ct: ct)), ct: ct));

    private static Result<InvoiceDto> Decide(InvoiceDto invoice, RequestContext context) =>
        invoice.Status is not (InvoiceStatus.Draft or InvoiceStatus.Rejected)
            ? Error.Conflict("documents.invoice.notDraft", "documents:invoice.notDraft").WithMetadata("status", invoice.Status)
            : invoice with
            {
                Status = InvoiceStatus.Submitted,
                SubmittedBy = context.User.Id,
                SubmittedByName = context.User.DisplayName,
                SubmittedAt = context.Now,
                DecidedBy = null,
                DecidedByName = null,
                DecidedAt = null,
                Comment = string.Empty,
            };
}

/// <summary>Marks an approved invoice as paid.</summary>
public sealed class PayInvoice(DocumentRepository repository, Db db, AuditLog audit, RequestContext context)
{
    public Task<Result<InvoiceDto>> ExecuteAsync(Guid invoiceId, CancellationToken ct) =>
        Steps.Authorize(context.User, DocumentPermissions.Edit)
            .BindAsync(_ => db.InTransactionAsync(session =>
                repository.FindInvoiceAsync(session, invoiceId, ct)
                    .Ensure(i => i.Status == InvoiceStatus.Approved, Error.Conflict("documents.invoice.notApproved", "documents:invoice.notApproved"))
                    .BindAsync(i => repository.UpdateInvoiceAsync(session, i with { Status = InvoiceStatus.Paid }, i.RowVersion, ct)
                        .Map(_ => i with { Status = InvoiceStatus.Paid, RowVersion = i.RowVersion + 1 }))
                    .TapAsync(i => audit.WriteAsync(session, context.User, "invoice.paid", "invoice", i.Id.ToString(), ct: ct)), ct: ct));
}

/// <summary>Archives the document of a paid or rejected invoice (workflow step documents.archiveInvoice).</summary>
public sealed class ArchiveInvoice(DocumentRepository repository, Db db, AuditLog audit, RequestContext context)
{
    public Task<Result<JsonObject>> ExecuteAsync(Guid invoiceId, CancellationToken ct) =>
        Steps.Authorize(context.User, DocumentPermissions.Edit)
            .BindAsync(_ => repository.FindInvoiceAsync(invoiceId, ct))
            .Ensure(i => i.Status is InvoiceStatus.Paid or InvoiceStatus.Rejected, Error.Conflict("documents.invoice.notFinished", "documents:invoice.notFinished"))
            .Ensure(i => i.DocumentId is not null, Error.Conflict("documents.invoice.noDocument", "documents:invoice.noDocument"))
            .BindAsync(i => db.InTransactionAsync(session =>
                repository.FindAsync(i.DocumentId!.Value, ct)
                    .BindAsync(d => d.Status == "archived"
                        ? Task.FromResult(Result.Success())
                        : repository.UpdateDocumentAsync(session, d with { Status = "archived", UpdatedAt = context.Now }, d.RowVersion, ct))
                    .BindAsync(_ => audit.WriteAsync(session, context.User, "invoice.archived", "invoice", i.Id.ToString(), ct: ct))
                    .Map(_ => new JsonObject { ["archived"] = true, ["documentId"] = i.DocumentId.ToString() }), ct: ct));
}

/// <summary>Sends an approved invoice to accounting (command documents.send, renamed to documents.dispatch in 1.1).</summary>
public sealed class DispatchInvoice(DocumentRepository repository, Db db, OutboxWriter outbox, AuditLog audit, RequestContext context)
{
    public Task<Result<JsonObject>> ExecuteAsync(Guid invoiceId, string? channel, CancellationToken ct) =>
        Steps.Authorize(context.User, DocumentPermissions.Edit)
            .BindAsync(_ => repository.FindInvoiceAsync(invoiceId, ct))
            .Ensure(i => i.Status is InvoiceStatus.Approved or InvoiceStatus.Paid, Error.Conflict("documents.invoice.notApproved", "documents:invoice.notApproved"))
            .BindAsync(i => db.InTransactionAsync(session =>
                outbox.AddAsync(session, outbox.Create(EventTypes.DocumentUpdated, new JsonObject
                    {
                        ["invoiceId"] = i.Id.ToString(),
                        ["number"] = i.Number,
                        ["dispatchedTo"] = channel ?? "accounting",
                    }), ct)
                    .BindAsync(_ => audit.WriteAsync(session, context.User, "invoice.dispatched", "invoice", i.Id.ToString(), ct: ct))
                    .Map(_ => new JsonObject { ["dispatched"] = true, ["channel"] = channel ?? "accounting", ["at"] = context.Now.ToString("O") }), ct: ct));
}
