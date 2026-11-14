using System.Text.Json.Nodes;
using GreenHive.NET.Modules.DocumentManagement.Contracts;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Events;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.NET.Modules.DocumentManagement.Backend.UseCases;

/// <summary>Creates a document (metadata; the first version is recorded).</summary>
public sealed class CreateDocument(DocumentRepository repository, Db db, OutboxWriter outbox, AuditLog audit, RequestContext context)
{
    public Task<Result<DocumentDto>> ExecuteAsync(CreateDocumentRequest request, CancellationToken ct) =>
        Steps.Validate(request, r => Validation.Combine(
                Validation.RequiredMax("title", r.Title, 200),
                Validation.OneOf("category", r.Category, DocumentCategories.All),
                Validation.RequiredMax("fileName", r.FileName, 260),
                Validation.Range("sizeBytes", r.SizeBytes, 0L, 500L * 1024 * 1024),
                (title, category, fileName, size) => new DocumentDto(Guid.NewGuid(), title, category, "active", context.User.DisplayName, fileName)
                {
                    ContentType = string.IsNullOrWhiteSpace(r.ContentType) ? "application/octet-stream" : r.ContentType,
                    SizeBytes = size,
                    Tags = r.Tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim().ToLowerInvariant()).Distinct().ToList() ?? [],
                    CreatedAt = context.Now,
                    UpdatedAt = context.Now,
                }))
            .Authorize(context.User, DocumentPermissions.Edit)
            .BindAsync(document => db.InTransactionAsync(session =>
                repository.InsertDocumentAsync(session, document, context.User.TenantId, context.User.Id, ct)
                    .BindAsync(_ => outbox.AddAsync(session, outbox.Create(EventTypes.DocumentCreated, new JsonObject
                    {
                        ["documentId"] = document.Id.ToString(),
                        ["title"] = document.Title,
                        ["category"] = document.Category,
                        ["owner"] = document.OwnerName,
                    }) with
                    {
                        Title = "documents:notification.documentCreated",
                        Message = document.Title,
                        NavigationTarget = "documents.list",
                        NavigationParameter = document.Id.ToString(),
                    }, ct))
                    .BindAsync(_ => audit.WriteAsync(session, context.User, "document.created", "document", document.Id.ToString(), ct: ct))
                    .Map(_ => document), ct: ct));
}

/// <summary>Updates title, category and tags (optimistic concurrency with the row version).</summary>
public sealed class UpdateDocument(DocumentRepository repository, Db db, OutboxWriter outbox, AuditLog audit, RequestContext context)
{
    public Task<Result<DocumentDto>> ExecuteAsync(Guid id, UpdateDocumentRequest request, CancellationToken ct) =>
        Steps.Validate(request, r => Validation.Combine(Validation.RequiredMax("title", r.Title, 200), Validation.OneOf("category", r.Category, DocumentCategories.All)))
            .Authorize(context.User, DocumentPermissions.Edit)
            .BindAsync(valid => repository.FindAsync(id, ct).Map(document => (document, valid)))
            .Ensure(x => x.document.Status != "archived", Error.Conflict("documents.document.archived", "documents:document.archived"))
            .Map(x => x.document with
            {
                Title = x.valid.Item1,
                Category = x.valid.Item2,
                Tags = request.Tags?.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().ToList() ?? x.document.Tags,
                UpdatedAt = context.Now,
            })
            .BindAsync(document => db.InTransactionAsync(session =>
                repository.UpdateDocumentAsync(session, document, request.RowVersion, ct)
                    .BindAsync(_ => outbox.AddAsync(session, outbox.Create(EventTypes.DocumentUpdated, new JsonObject
                    {
                        ["documentId"] = document.Id.ToString(),
                        ["title"] = document.Title,
                    }), ct))
                    .BindAsync(_ => audit.WriteAsync(session, context.User, "document.updated", "document", document.Id.ToString(), ct: ct))
                    .Map(_ => document with { RowVersion = request.RowVersion + 1 }), ct: ct));
}

/// <summary>Adds a new version of a document.</summary>
public sealed class AddDocumentVersion(DocumentRepository repository, Db db, AuditLog audit, RequestContext context)
{
    public Task<Result<DocumentDto>> ExecuteAsync(Guid id, AddVersionRequest request, CancellationToken ct) =>
        Steps.Validate(request, r => Validation.Combine(Validation.RequiredMax("fileName", r.FileName, 260), Validation.MaxLength("comment", r.Comment, 500)))
            .Authorize(context.User, DocumentPermissions.Edit)
            .BindAsync(_ => repository.FindAsync(id, ct))
            .Ensure(d => d.Status != "archived", Error.Conflict("documents.document.archived", "documents:document.archived"))
            .BindAsync(d =>
            {
                var next = d with { CurrentVersion = d.CurrentVersion + 1, FileName = request.FileName, SizeBytes = request.SizeBytes, UpdatedAt = context.Now };
                return db.InTransactionAsync(session =>
                    repository.InsertVersionAsync(session, id, new DocumentVersionDto(next.CurrentVersion, request.FileName, request.SizeBytes,
                            request.Comment ?? string.Empty, context.User.DisplayName, context.Now), ct)
                        .BindAsync(_ => repository.UpdateDocumentAsync(session, next, d.RowVersion, ct))
                        .BindAsync(_ => audit.WriteAsync(session, context.User, "document.versionAdded", "document", id.ToString(), ct: ct))
                        .Map(_ => next with { RowVersion = d.RowVersion + 1 }), ct: ct);
            });
}

/// <summary>Archives a document.</summary>
public sealed class ArchiveDocument(DocumentRepository repository, Db db, AuditLog audit, RequestContext context)
{
    public Task<Result<DocumentDto>> ExecuteAsync(Guid id, CancellationToken ct) =>
        Steps.Authorize(context.User, DocumentPermissions.Edit)
            .BindAsync(_ => repository.FindAsync(id, ct))
            .Ensure(d => d.Status != "archived", Error.Conflict("documents.document.archived", "documents:document.archived"))
            .BindAsync(d => db.InTransactionAsync(session =>
                repository.UpdateDocumentAsync(session, d with { Status = "archived", UpdatedAt = context.Now }, d.RowVersion, ct)
                    .BindAsync(_ => audit.WriteAsync(session, context.User, "document.archived", "document", id.ToString(), ct: ct))
                    .Map(_ => d with { Status = "archived", RowVersion = d.RowVersion + 1 }), ct: ct));
}
