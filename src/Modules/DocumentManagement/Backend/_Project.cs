// GreenHive.NET.Modules.DocumentManagement.Backend - Docker container "document-service", route /api/documents.
// Documents with versions and invoices with the approval process. Every operation is a use case (Result pipeline) and is
// authorized server-side (documents.view / .edit / .approve). Domain events go through the outbox to the NotificationPoint.
// Headless workflow step endpoints (/api/documents/workflow-steps/{stepType}) call the same use cases as the UI, so people,
// the workflow engine and the AI agent get identical rules and errors.
