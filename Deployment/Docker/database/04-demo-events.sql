-- =============================================================================
-- Green Hive - demo events
-- The submitted demo invoices are put into the outbox of the document-service as "documents.invoice.submitted".
-- When the services start, the outbox dispatcher delivers them to the NotificationPoint, the trigger of the workflow
-- "Invoice approval" starts one instance per invoice and the AI agent pre-checks them - so the inboxes fill by themselves.
-- =============================================================================

insert into platform.outbox (id, source, type, envelope, created_at)
select
    gen_random_uuid() as id,
    'document-service',
    'documents.invoice.submitted',
    jsonb_build_object(
        'id', gen_random_uuid(),
        'sequence', 0,
        'type', 'documents.invoice.submitted',
        'source', 'document-service',
        'timestamp', to_char(i.submitted_at at time zone 'utc', 'YYYY-MM-DD"T"HH24:MI:SS"Z"'),
        'tenantId', i.tenant_id,
        'audience', jsonb_build_object('kind', 'permission', 'value', 'documents.approve'),
        'severity', 'info',
        'title', 'documents:notification.invoiceSubmitted',
        'message', i.number || ' - ' || i.supplier || ' (' || to_char(i.amount, 'FM999G999G990D00') || ' ' || i.currency || ')',
        'navigationTarget', 'documents.invoices',
        'navigationParameter', i.id::text,
        'payload', jsonb_build_object(
            'invoiceId', i.id::text,
            'number', i.number,
            'supplier', i.supplier,
            'amount', i.amount,
            'currency', i.currency,
            'costCenter', i.cost_center,
            'status', i.status,
            'category', 'invoice',
            'submittedBy', i.submitted_by_name,
            'dueOn', to_char(i.due_on, 'YYYY-MM-DD'))),
    now()
from documents.invoices i
where i.status = 'submitted'
order by i.submitted_at;
