using System.Text.Json.Nodes;
using GreenHive.NET.Modules.DocumentManagement.Backend.UseCases;
using GreenHive.NET.Modules.DocumentManagement.Contracts;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Monads;

namespace GreenHive.NET.Modules.DocumentManagement.Backend;

/// <summary>
/// Headless endpoints of the module's workflow step types and workflow-capable commands. Inputs and outputs are JSON
/// objects; every step calls the same use case as the UI (the caller is the RunAs user, possibly delegated).
/// </summary>
public sealed class WorkflowSteps(DocumentRepository repository, DecideInvoice decide, SubmitInvoice submit, ArchiveInvoice archive,
    DispatchInvoice dispatch, RequestContext context)
{
    public Task<Result<JsonObject>> ExecuteAsync(string stepType, JsonObject inputs, CancellationToken ct)
    {
        var invoiceId = Guid.TryParse(inputs["invoiceId"]?.ToString(), out var id) ? id : Guid.Empty;
        var comment = inputs["comment"]?.ToString() ?? inputs["note"]?.ToString();
        return stepType switch
        {
            "documents.lookupInvoice" => Steps.Authorize(context.User, DocumentPermissions.View)
                .BindAsync(_ => repository.FindInvoiceAsync(invoiceId, ct))
                .Map(DecideInvoice.Payload),
            "documents.approveInvoice" => decide.ApproveAsync(invoiceId, comment, ct).Map(Decided),
            "documents.rejectInvoice" => decide.RejectAsync(invoiceId, comment, ct).Map(Decided),
            "documents.submitInvoice" => submit.ExecuteAsync(invoiceId, ct).Map(i => new JsonObject { ["status"] = i.Status }),
            "documents.archiveInvoice" => archive.ExecuteAsync(invoiceId, ct),
            "documents.send" or "documents.dispatch" => dispatch.ExecuteAsync(invoiceId, inputs["channel"]?.ToString(), ct),
            _ => Task.FromResult(Result<JsonObject>.Failure(Error.NotFound("documents.stepType.unknown", "workflow:stepType.unknown").WithMetadata("stepType", stepType))),
        };
    }

    private static JsonObject Decided(InvoiceDto invoice) => new()
    {
        ["status"] = invoice.Status,
        ["decidedBy"] = invoice.DecidedByName,
        ["invoiceId"] = invoice.Id.ToString(),
    };
}
