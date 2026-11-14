using System.Text.Json.Nodes;
using GreenHive.NET.Modules.DocumentManagement.Contracts;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Events;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Backend.Core.Security;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.NET.Modules.DocumentManagement.Backend.UseCases;

/// <summary>
/// Approve or reject an invoice - the reference use case of the monad architecture:
/// 1. Validate  : invoiceId present, comment max 500 chars (all errors at once)          -> Validation
/// 2. Authorize : the (RunAs) user has documents.approve                                  -> Forbidden
/// 3. Load      : invoice (locked for update) and the approver's limit                    -> NotFound
/// 4. Decide    : pure rules - status submitted, amount within limit, no self-approval    -> Conflict / Forbidden
/// 5. Persist   : save with optimistic concurrency                                        -> Conflict
/// 6. Publish   : documents.invoice.approved / .rejected into the outbox (same transaction)
/// 7. Project   : the updated invoice
/// The same code runs for the HTTP endpoint, the workflow step types documents.approveInvoice / .rejectInvoice and the
/// AI tool of the same name - identical rules and errors for people, the workflow engine and the AI agent.
/// </summary>
public sealed class DecideInvoice(DocumentRepository repository, Db db, ServiceClient services, OutboxWriter outbox, AuditLog audit, RequestContext context)
{
    public Task<Result<InvoiceDto>> ApproveAsync(Guid invoiceId, string? comment, CancellationToken ct) => ExecuteAsync(invoiceId, comment, approve: true, ct);

    public Task<Result<InvoiceDto>> RejectAsync(Guid invoiceId, string? comment, CancellationToken ct) => ExecuteAsync(invoiceId, comment, approve: false, ct);

    private Task<Result<InvoiceDto>> ExecuteAsync(Guid invoiceId, string? comment, bool approve, CancellationToken ct) =>
        Steps.Validate((invoiceId, comment), r => Validation.Combine(
                Validation.NotEmpty("invoiceId", r.invoiceId),
                approve ? Validation.MaxLength("comment", r.comment, 500) : Validation.RequiredMax("comment", r.comment, 500).Map(c => (string?)c)))
            .Authorize(context.User, DocumentPermissions.Approve)
            .BindAsync(_ => ApproverLimitAsync(ct))
            .BindAsync(limit => db.InTransactionAsync(session =>
                repository.FindInvoiceAsync(session, invoiceId, ct)
                    .Bind(invoice => Decide(invoice, context.User, limit, approve, comment ?? string.Empty, context.Now).Map(decided => (Before: invoice, After: decided)))
                    .BindAsync(x => repository.UpdateInvoiceAsync(session, x.After, x.Before.RowVersion, ct).Map(_ => x.After with { RowVersion = x.Before.RowVersion + 1 }))
                    .TapAsync(decided => outbox.AddAsync(session, Event(decided, approve), ct))
                    .TapAsync(decided => audit.WriteAsync(session, context.User, approve ? "invoice.approved" : "invoice.rejected", "invoice",
                        decided.Id.ToString(), details: new JsonObject { ["amount"] = decided.Amount, ["number"] = decided.Number }, ct: ct)), ct: ct));

    /// <summary>Pure domain rules on the immutable invoice record (no I/O).</summary>
    public static Result<InvoiceDto> Decide(InvoiceDto invoice, GreenHiveUser approver, decimal limit, bool approve, string comment, DateTimeOffset now) =>
        invoice.Status != InvoiceStatus.Submitted
            ? Error.Conflict("documents.invoice.notSubmitted", "documents:invoice.notSubmitted").WithMetadata("status", invoice.Status)
        : invoice.SubmittedBy == approver.Id
            ? Error.Forbidden("documents.invoice.selfApproval", "documents:invoice.selfApproval")
        : approve && invoice.Amount > limit
            ? Error.Forbidden("documents.invoice.overLimit", "documents:invoice.overLimit").WithMetadata("limit", limit).WithMetadata("amount", invoice.Amount)
        : invoice with
        {
            Status = approve ? InvoiceStatus.Approved : InvoiceStatus.Rejected,
            DecidedBy = approver.Id,
            DecidedByName = approver.Actor is null ? approver.DisplayName : $"{approver.DisplayName} ({approver.Actor})",
            DecidedAt = now,
            Comment = comment,
        };

    private Task<Result<decimal>> ApproverLimitAsync(CancellationToken ct) =>
        services.GetAsync<UserInfo>("identity-service", $"/internal/users/{context.User.Id}", null, ct).Map(u => u.ApprovalLimit);

    private NotificationEnvelope Event(InvoiceDto invoice, bool approved) =>
        outbox.Create(approved ? EventTypes.InvoiceApproved : EventTypes.InvoiceRejected, Payload(invoice),
            invoice.SubmittedBy is { } submitter ? NotificationAudience.ForUser(submitter) : NotificationAudience.Everyone) with
        {
            Title = approved ? "documents:notification.invoiceApproved" : "documents:notification.invoiceRejected",
            Message = $"{invoice.Number} - {invoice.Supplier} ({invoice.Amount:N2} {invoice.Currency})",
            Severity = approved ? NotificationSeverity.Success : NotificationSeverity.Warning,
            NavigationTarget = "documents.invoices",
            NavigationParameter = invoice.Id.ToString(),
        };

    public static JsonObject Payload(InvoiceDto invoice) => new()
    {
        ["invoiceId"] = invoice.Id.ToString(),
        ["number"] = invoice.Number,
        ["supplier"] = invoice.Supplier,
        ["amount"] = invoice.Amount,
        ["currency"] = invoice.Currency,
        ["costCenter"] = invoice.CostCenter,
        ["status"] = invoice.Status,
        ["category"] = "invoice",
        ["submittedBy"] = invoice.SubmittedByName,
        ["dueOn"] = invoice.DueOn.ToString("yyyy-MM-dd"),
    };
}
