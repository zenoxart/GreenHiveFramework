namespace GreenHive.NET.NotificationContracts;

/// <summary>Catalogue of known event types.</summary>
public static class EventTypes
{
    // System
    public const string ModuleVersionAvailable = "module.version.available";
    public const string ModuleUpdateStatus = "module.update.status";
    public const string ModulePublished = "module.published";
    public const string PermissionsChanged = "permissions.changed";
    public const string MaintenanceAnnounced = "maintenance.announced";
    public const string CommandFailed = "command.failed";
    public const string JobProgress = "job.progress";

    // Domain
    public const string DocumentCreated = "documents.created";
    public const string DocumentUpdated = "documents.updated";
    public const string InvoiceSubmitted = "documents.invoice.submitted";
    public const string InvoiceApproved = "documents.invoice.approved";
    public const string InvoiceRejected = "documents.invoice.rejected";
    public const string UserCreated = "users.created";
    public const string UserUpdated = "users.updated";
    public const string ReportReady = "reports.ready";

    // Workflow
    public const string WorkflowInstanceStarted = "workflow.instance.started";
    public const string WorkflowInstanceWaiting = "workflow.instance.waiting";
    public const string WorkflowInstanceCompleted = "workflow.instance.completed";
    public const string WorkflowInstanceFailed = "workflow.instance.failed";
    public const string WorkflowApprovalRequested = "workflow.approval.requested";
    public const string WorkflowApprovalDecided = "workflow.approval.decided";
    public const string WorkflowImpactDetected = "workflow.impact.detected";
    public const string WorkflowTriggerSuspended = "workflow.trigger.suspended";
}
