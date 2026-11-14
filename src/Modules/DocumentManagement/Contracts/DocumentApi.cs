// Shared by GreenHive.NET.Modules.DocumentManagement.Backend and .Client (compiled into both - one wire format).
namespace GreenHive.NET.Modules.DocumentManagement.Contracts;

public static class DocumentPermissions
{
    public const string View = "documents.view";
    public const string Edit = "documents.edit";
    public const string Approve = "documents.approve";
}

public static class DocumentCategories
{
    public static readonly string[] All = ["invoice", "contract", "report", "other"];
}

public static class InvoiceStatus
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Paid = "paid";
}

public sealed record DocumentDto(Guid Id, string Title, string Category, string Status, string OwnerName, string FileName)
{
    public string ContentType { get; init; } = "application/pdf";

    public long SizeBytes { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public int CurrentVersion { get; init; } = 1;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public int RowVersion { get; init; } = 1;
}

public sealed record DocumentVersionDto(int Version, string FileName, long SizeBytes, string Comment, string CreatedBy, DateTimeOffset CreatedAt);

public sealed record CreateDocumentRequest(string Title, string Category, string FileName)
{
    public string ContentType { get; init; } = "application/pdf";

    public long SizeBytes { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }
}

public sealed record UpdateDocumentRequest(string Title, string Category, int RowVersion)
{
    public IReadOnlyList<string>? Tags { get; init; }
}

public sealed record AddVersionRequest(string FileName, long SizeBytes, string? Comment);

public sealed record InvoiceDto(Guid Id, string Number, string Supplier, decimal Amount, string Currency, string Status)
{
    public DateOnly IssuedOn { get; init; }

    public DateOnly DueOn { get; init; }

    public string CostCenter { get; init; } = string.Empty;

    public Guid? DocumentId { get; init; }

    public Guid? SubmittedBy { get; init; }

    public string? SubmittedByName { get; init; }

    public DateTimeOffset? SubmittedAt { get; init; }

    public Guid? DecidedBy { get; init; }

    public string? DecidedByName { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    public string Comment { get; init; } = string.Empty;

    public int RowVersion { get; init; } = 1;
}

public sealed record CreateInvoiceRequest(string Number, string Supplier, decimal Amount, string Currency, DateOnly IssuedOn, DateOnly DueOn, string CostCenter)
{
    public Guid? DocumentId { get; init; }
}

public sealed record InvoiceDecisionRequest(string? Comment);

public sealed record DocumentStats(int Documents, int Drafts, int Archived, int InvoicesSubmitted, int InvoicesApproved, decimal OpenAmount, int OverdueInvoices);
