using System.Data.Common;
using GreenHive.NET.Modules.DocumentManagement.Contracts;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Contracts;
using GreenHive.NET.Monads;

namespace GreenHive.NET.Modules.DocumentManagement.Backend;

/// <summary>Data access of the documents schema (adapter only).</summary>
public sealed class DocumentRepository(Db db)
{
    public Task<Result<PagedResult<DocumentDto>>> SearchAsync(Guid tenantId, string? search, string? category, string? status, int skip, int take, CancellationToken ct) =>
        db.QueryAsync(
                """
                select *, count(*) over () as total from documents.documents
                where tenant_id = @tenantId
                  and (@category::text is null or category = @category)
                  and (@status::text is null or status = @status)
                  and (@search::text is null or title ilike '%' || @search || '%' or file_name ilike '%' || @search || '%' or @search = any(tags))
                order by updated_at desc offset @skip limit @take
                """,
                r => (Doc: MapDocument(r), Total: (int)r.Get<long>("total")),
                new { tenantId, search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(), category, status, skip, take }, ct)
            .Map(rows => new PagedResult<DocumentDto>(rows.Select(r => r.Doc).ToList(), rows.FirstOrDefault().Total, skip, take));

    public Task<Result<DocumentDto>> FindAsync(Guid id, CancellationToken ct) =>
        db.FindAsync("select * from documents.documents where id = @id", MapDocument, new { id }, NotFound("documents.document", id), ct);

    public Task<Result<IReadOnlyList<DocumentVersionDto>>> VersionsAsync(Guid id, CancellationToken ct) =>
        db.QueryAsync("select * from documents.document_versions where document_id = @id order by version desc",
            r => new DocumentVersionDto(r.Get<int>("version"), r.Get<string>("file_name"), r.Get<long>("size_bytes"), r.Get<string>("comment"),
                r.Get<string>("created_by"), r.GetTime("created_at")), new { id }, ct);

    public Task<Result<Unit>> InsertDocumentAsync(DbSession session, DocumentDto d, Guid tenantId, Guid ownerId, CancellationToken ct) =>
        session.ExecuteAsync(
                """
                insert into documents.documents (id, tenant_id, title, category, status, owner_id, owner_name, file_name, content_type, size_bytes, tags,
                    current_version, created_at, updated_at, row_version)
                values (@id, @tenantId, @title, @category, @status, @ownerId, @ownerName, @fileName, @contentType, @size, @tags, 1, @now, @now, 1)
                """,
                new
                {
                    id = d.Id, tenantId, title = d.Title, category = d.Category, status = d.Status, ownerId, ownerName = d.OwnerName,
                    fileName = d.FileName, contentType = d.ContentType, size = d.SizeBytes, tags = d.Tags.ToArray(), now = d.CreatedAt,
                }, ct)
            .BindAsync(_ => session.ExecuteAsync(
                """
                insert into documents.document_versions (document_id, version, file_name, size_bytes, comment, created_by, created_at)
                values (@id, 1, @fileName, @size, 'Initial upload', @ownerName, @now)
                """, new { id = d.Id, fileName = d.FileName, size = d.SizeBytes, ownerName = d.OwnerName, now = d.CreatedAt }, ct))
            .ToUnit();

    /// <summary>Optimistic concurrency: the update only succeeds for the expected row version.</summary>
    public Task<Result<Unit>> UpdateDocumentAsync(DbSession session, DocumentDto d, int expectedRowVersion, CancellationToken ct) =>
        session.ExecuteExpectingAsync(
            """
            update documents.documents set title = @title, category = @category, status = @status, tags = @tags, file_name = @fileName,
                size_bytes = @size, current_version = @version, updated_at = @now, row_version = row_version + 1
            where id = @id and row_version = @expected
            """,
            new
            {
                id = d.Id, title = d.Title, category = d.Category, status = d.Status, tags = d.Tags.ToArray(), fileName = d.FileName,
                size = d.SizeBytes, version = d.CurrentVersion, now = d.UpdatedAt, expected = expectedRowVersion,
            }, 1, Error.Conflict("documents.document.concurrency", "documents:document.changedByOthers"), ct);

    public Task<Result<int>> InsertVersionAsync(DbSession session, Guid id, DocumentVersionDto v, CancellationToken ct) =>
        session.ExecuteAsync(
            """
            insert into documents.document_versions (document_id, version, file_name, size_bytes, comment, created_by, created_at)
            values (@id, @version, @fileName, @size, @comment, @createdBy, @createdAt)
            """, new { id, version = v.Version, fileName = v.FileName, size = v.SizeBytes, comment = v.Comment, createdBy = v.CreatedBy, createdAt = v.CreatedAt }, ct);

    public Task<Result<DocumentStats>> StatsAsync(Guid tenantId, CancellationToken ct) =>
        db.FindAsync(
            """
            select
              (select count(*)::int from documents.documents where tenant_id = @tenantId) as documents,
              (select count(*)::int from documents.documents where tenant_id = @tenantId and status = 'draft') as drafts,
              (select count(*)::int from documents.documents where tenant_id = @tenantId and status = 'archived') as archived,
              (select count(*)::int from documents.invoices where tenant_id = @tenantId and status = 'submitted') as submitted,
              (select count(*)::int from documents.invoices where tenant_id = @tenantId and status = 'approved') as approved,
              (select coalesce(sum(amount), 0) from documents.invoices where tenant_id = @tenantId and status in ('submitted', 'approved')) as open_amount,
              (select count(*)::int from documents.invoices where tenant_id = @tenantId and status in ('submitted', 'approved') and due_on < current_date) as overdue
            """,
            r => new DocumentStats(r.Get<int>("documents"), r.Get<int>("drafts"), r.Get<int>("archived"), r.Get<int>("submitted"), r.Get<int>("approved"),
                r.Get<decimal>("open_amount"), r.Get<int>("overdue")),
            new { tenantId }, Error.Unexpected("documents.stats", "errors:unexpected"), ct);

    // ---------------------------------------------------------------- invoices

    public Task<Result<IReadOnlyList<InvoiceDto>>> InvoicesAsync(Guid tenantId, string? status, string? search, CancellationToken ct) =>
        db.QueryAsync(
            """
            select * from documents.invoices
            where tenant_id = @tenantId and (@status::text is null or status = @status)
              and (@search::text is null or number ilike '%' || @search || '%' or supplier ilike '%' || @search || '%')
            order by case status when 'submitted' then 0 when 'draft' then 1 when 'approved' then 2 else 3 end, due_on
            """, MapInvoice, new { tenantId, status, search = string.IsNullOrWhiteSpace(search) ? null : search.Trim() }, ct);

    public Task<Result<InvoiceDto>> FindInvoiceAsync(Guid id, CancellationToken ct) =>
        db.FindAsync("select * from documents.invoices where id = @id", MapInvoice, new { id }, NotFound("documents.invoice", id), ct);

    public Task<Result<InvoiceDto>> FindInvoiceAsync(DbSession session, Guid id, CancellationToken ct) =>
        session.FindAsync("select * from documents.invoices where id = @id for update", MapInvoice, new { id }, NotFound("documents.invoice", id), ct);

    public Task<Result<Unit>> InsertInvoiceAsync(DbSession session, InvoiceDto i, Guid tenantId, CancellationToken ct) =>
        session.ExecuteAsync(
            """
            insert into documents.invoices (id, tenant_id, document_id, number, supplier, amount, currency, issued_on, due_on, cost_center, status, created_at)
            values (@id, @tenantId, @documentId, @number, @supplier, @amount, @currency, @issuedOn, @dueOn, @costCenter, @status, now())
            """,
            new
            {
                id = i.Id, tenantId, documentId = i.DocumentId, number = i.Number, supplier = i.Supplier, amount = i.Amount, currency = i.Currency,
                issuedOn = i.IssuedOn, dueOn = i.DueOn, costCenter = i.CostCenter, status = i.Status,
            }, ct).ToUnit();

    public Task<Result<Unit>> UpdateInvoiceAsync(DbSession session, InvoiceDto i, int expectedRowVersion, CancellationToken ct) =>
        session.ExecuteExpectingAsync(
            """
            update documents.invoices set status = @status, submitted_by = @submittedBy, submitted_by_name = @submittedByName, submitted_at = @submittedAt,
                decided_by = @decidedBy, decided_by_name = @decidedByName, decided_at = @decidedAt, comment = @comment, row_version = row_version + 1
            where id = @id and row_version = @expected
            """,
            new
            {
                id = i.Id, status = i.Status, submittedBy = i.SubmittedBy, submittedByName = i.SubmittedByName, submittedAt = i.SubmittedAt,
                decidedBy = i.DecidedBy, decidedByName = i.DecidedByName, decidedAt = i.DecidedAt, comment = i.Comment, expected = expectedRowVersion,
            }, 1, Error.Conflict("documents.invoice.concurrency", "documents:invoice.changedByOthers"), ct);

    /// <summary>Cost centres for the EntityRef field (read-only lookup of the organisation's departments).</summary>
    public Task<Result<IReadOnlyList<LookupItemRow>>> CostCentersAsync(CancellationToken ct) =>
        db.QueryAsync("select cost_center, name from usermgmt.departments order by cost_center",
            r => new LookupItemRow(r.Get<string>("cost_center"), $"{r.Get<string>("cost_center")} - {r.Get<string>("name")}"), null, ct);

    private static Error NotFound(string entity, Guid id) =>
        Error.NotFound($"{entity}.notFound", $"documents:{entity.Split('.').Last()}.notFound").WithMetadata("id", id);

    private static DocumentDto MapDocument(DbDataReader r) =>
        new(r.Get<Guid>("id"), r.Get<string>("title"), r.Get<string>("category"), r.Get<string>("status"), r.Get<string>("owner_name"), r.Get<string>("file_name"))
        {
            ContentType = r.Get<string>("content_type"),
            SizeBytes = r.Get<long>("size_bytes"),
            Tags = r.GetTextArray("tags"),
            CurrentVersion = r.Get<int>("current_version"),
            CreatedAt = r.GetTime("created_at"),
            UpdatedAt = r.GetTime("updated_at"),
            RowVersion = r.Get<int>("row_version"),
        };

    private static InvoiceDto MapInvoice(DbDataReader r) =>
        new(r.Get<Guid>("id"), r.Get<string>("number"), r.Get<string>("supplier"), r.Get<decimal>("amount"), r.Get<string>("currency"), r.Get<string>("status"))
        {
            IssuedOn = r.Get<DateOnly>("issued_on"),
            DueOn = r.Get<DateOnly>("due_on"),
            CostCenter = r.Get<string>("cost_center"),
            DocumentId = r.GetNullable<Guid>("document_id"),
            SubmittedBy = r.GetNullable<Guid>("submitted_by"),
            SubmittedByName = r.GetOrNull<string>("submitted_by_name"),
            SubmittedAt = r.GetTimeOrNull("submitted_at"),
            DecidedBy = r.GetNullable<Guid>("decided_by"),
            DecidedByName = r.GetOrNull<string>("decided_by_name"),
            DecidedAt = r.GetTimeOrNull("decided_at"),
            Comment = r.Get<string>("comment"),
            RowVersion = r.Get<int>("row_version"),
        };
}

public sealed record LookupItemRow(string Value, string Label);
