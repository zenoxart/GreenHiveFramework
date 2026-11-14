-- =============================================================================
-- Green Hive - database schema (PostgreSQL 16+)
-- One database "greenhive", one schema per service. Executed once by the postgres container
-- (docker-entrypoint-initdb.d) when the data volume is empty.
-- =============================================================================

create extension if not exists pgcrypto;

-- ---------------------------------------------------------------- platform (shared infrastructure)
create schema if not exists platform;

-- Transactional outbox of every service; the OutboxDispatcher forwards rows to the NotificationPoint.
create table platform.outbox (
    id            uuid primary key,
    source        text        not null,
    type          text        not null,
    envelope      jsonb       not null,
    created_at    timestamptz not null default now(),
    dispatched_at timestamptz
);
create index ix_outbox_pending on platform.outbox (source, created_at) where dispatched_at is null;

-- Who did what (user, workflow engine, AI agent) with the outcome.
create table platform.audit_log (
    id          uuid primary key,
    at          timestamptz not null default now(),
    service     text        not null,
    user_id     uuid,
    user_name   text        not null,
    actor       text,
    action      text        not null,
    entity_type text        not null,
    entity_id   text,
    outcome     text        not null,
    details     jsonb       not null default '{}'
);
create index ix_audit_entity on platform.audit_log (entity_type, entity_id);
create index ix_audit_at on platform.audit_log (at desc);

-- Stored results for repeated idempotency keys (workflow engine, retries).
create table platform.idempotency (
    key        text primary key,
    response   jsonb       not null,
    created_at timestamptz not null default now()
);

-- ---------------------------------------------------------------- identity (IdentityService)
create schema if not exists identity;

create table identity.tenants (
    id   uuid primary key,
    name text not null
);

create table identity.users (
    id             uuid primary key,
    tenant_id      uuid        not null references identity.tenants (id),
    user_name      text        not null unique,
    display_name   text        not null,
    email          text        not null unique,
    password_hash  text        not null,
    is_active      boolean     not null default true,
    approval_limit numeric(14, 2) not null default 0,
    created_at     timestamptz not null default now(),
    last_login_at  timestamptz
);

-- Profile pictures (base64 PNG/JPEG, max 512 KB), shown in the client's title bar and user lists.
create table identity.user_avatars (
    user_id      uuid primary key references identity.users (id) on delete cascade,
    content_type text        not null check (content_type in ('image/png', 'image/jpeg')),
    data_base64  text        not null,
    updated_at   timestamptz not null default now()
);

create table identity.roles (
    id          uuid primary key,
    tenant_id   uuid not null references identity.tenants (id),
    name        text not null,
    description text not null default '',
    unique (tenant_id, name)
);

create table identity.permissions (
    name        text primary key,
    module_id   text not null,
    description text not null default ''
);

create table identity.role_permissions (
    role_id    uuid not null references identity.roles (id) on delete cascade,
    permission text not null references identity.permissions (name) on delete cascade,
    primary key (role_id, permission)
);

create table identity.user_roles (
    user_id uuid not null references identity.users (id) on delete cascade,
    role_id uuid not null references identity.roles (id) on delete cascade,
    primary key (user_id, role_id)
);

-- ---------------------------------------------------------------- usermgmt (UserManagement module)
create schema if not exists usermgmt;

create table usermgmt.departments (
    id          uuid primary key,
    tenant_id   uuid not null references identity.tenants (id),
    name        text not null,
    cost_center text not null
);

create table usermgmt.profiles (
    user_id       uuid primary key references identity.users (id) on delete cascade,
    department_id uuid references usermgmt.departments (id),
    job_title     text not null default '',
    phone         text not null default '',
    location      text not null default '',
    manager_id    uuid references identity.users (id),
    hired_on      date,
    row_version   integer not null default 1
);

-- ---------------------------------------------------------------- config (ConfigurationService)
create schema if not exists config;

create table config.settings (
    tenant_id  uuid        not null,
    scope      text        not null check (scope in ('tenant', 'user')),
    owner      text        not null default '',
    key        text        not null,
    value      jsonb       not null,
    updated_at timestamptz not null default now(),
    updated_by text,
    primary key (tenant_id, scope, owner, key)
);

-- ---------------------------------------------------------------- registry (ModuleRegistryService)
create schema if not exists registry;

create table registry.modules (
    module_id    text        not null,
    version      text        not null,
    major        integer     not null,
    minor        integer     not null,
    patch        integer     not null,
    channel      text        not null default 'stable',
    status       text        not null default 'published' check (status in ('published', 'withdrawn')),
    manifest     jsonb       not null,
    published_at timestamptz not null default now(),
    published_by text,
    primary key (module_id, version)
);

-- ---------------------------------------------------------------- delivery (ModuleDeliveryService)
create schema if not exists delivery;

create table delivery.packages (
    module_id   text        not null,
    version     text        not null,
    file_name   text        not null,
    sha256      text        not null,
    size_bytes  bigint      not null,
    signature   text        not null,
    uploaded_at timestamptz not null default now(),
    primary key (module_id, version)
);

-- ---------------------------------------------------------------- updates (UpdateOrchestrator)
create schema if not exists updates;

create table updates.rollouts (
    id               uuid primary key,
    module_id        text        not null,
    from_version     text,
    to_version       text        not null,
    stage            text        not null check (stage in ('Pilot', 'Broad', 'Complete', 'Held', 'Cancelled')),
    hold_reason      text,
    impact_report_id uuid,
    broken_workflows integer     not null default 0,
    pilot_users      uuid[]      not null default '{}',
    created_at       timestamptz not null default now(),
    updated_at       timestamptz not null default now()
);
create index ix_rollouts_module on updates.rollouts (module_id, created_at desc);

-- ---------------------------------------------------------------- notify (NotificationPoint)
create schema if not exists notify;

create table notify.events (
    sequence       bigserial primary key,
    id             uuid        not null unique,
    type           text        not null,
    source         text        not null,
    tenant_id      uuid        not null,
    audience_kind  text        not null,
    audience_value text,
    severity       text        not null,
    envelope       jsonb       not null,
    created_at     timestamptz not null default now()
);
create index ix_events_tenant on notify.events (tenant_id, sequence);

-- WNS push channels of clients (for users without a live connection).
create table notify.channels (
    user_id     uuid        not null,
    channel_uri text        not null,
    updated_at  timestamptz not null default now(),
    primary key (user_id, channel_uri)
);

-- ---------------------------------------------------------------- workflow (WorkflowService)
create schema if not exists workflow;

-- Every row is one version of a definition; published versions are immutable (status is metadata).
create table workflow.definitions (
    id            uuid        not null,
    version       integer     not null,
    tenant_id     uuid        not null,
    name          text        not null,
    status        text        not null,
    origin        text        not null,
    owning_module text,
    owner_id      uuid,
    definition    jsonb       not null,
    created_at    timestamptz not null default now(),
    updated_at    timestamptz not null default now(),
    primary key (id, version)
);

-- Dependency index: "which workflows use command X / schema Y / module Z".
create table workflow.dependencies (
    workflow_id uuid    not null,
    version     integer not null,
    kind        text    not null,
    dep_id      text    not null,
    dep_version text    not null,
    steps       text[]  not null default '{}',
    primary key (workflow_id, version, kind, dep_id),
    foreign key (workflow_id, version) references workflow.definitions (id, version) on delete cascade
);
create index ix_dependencies_lookup on workflow.dependencies (kind, dep_id);

create table workflow.instances (
    id                  uuid primary key,
    tenant_id           uuid        not null,
    workflow_id         uuid        not null,
    workflow_version    integer     not null,
    workflow_name       text        not null,
    state               text        not null,
    current_step_id     text,
    executor            text        not null,
    run_as              uuid        not null,
    run_as_name         text        not null,
    approver_permission text,
    waiting_event       text,
    wait_until          timestamptz,
    retry_at            timestamptz,
    attempts            integer     not null default 0,
    parent_instance_id  uuid,
    instance            jsonb       not null,
    created_at          timestamptz not null default now(),
    updated_at          timestamptz not null default now()
);
create index ix_instances_state on workflow.instances (state);
create index ix_instances_runas on workflow.instances (run_as, state);

-- De-duplication of trigger runs.
create table workflow.trigger_runs (
    workflow_id uuid        not null,
    trigger_id  text        not null,
    dedup_key   text        not null,
    instance_id uuid        not null,
    created_at  timestamptz not null default now(),
    primary key (workflow_id, trigger_id, dedup_key)
);

create table workflow.schedule_state (
    workflow_id uuid        not null,
    trigger_id  text        not null,
    last_run_at timestamptz not null,
    primary key (workflow_id, trigger_id)
);

create table workflow.impact_reports (
    id           uuid primary key,
    module_id    text        not null,
    from_version text        not null,
    to_version   text        not null,
    analyzed_at  timestamptz not null default now(),
    report       jsonb       not null
);

-- ---------------------------------------------------------------- ai (AiAgentService)
create schema if not exists ai;

create table ai.audit (
    id            uuid primary key,
    tenant_id     uuid        not null,
    instance_id   uuid        not null,
    workflow_id   uuid        not null,
    step_id       text        not null,
    at            timestamptz not null default now(),
    provider      text        not null,
    model         text        not null,
    model_profile text        not null,
    prompt_hash   text        not null,
    proposal      jsonb,
    reasoning     text,
    confidence    double precision,
    decision      text        not null,
    policy_code   text,
    tokens        integer     not null default 0
);
create index ix_ai_audit_instance on ai.audit (instance_id, at);
create index ix_ai_audit_day on ai.audit (tenant_id, at);

create table ai.switches (
    scope   text primary key,
    enabled boolean     not null,
    changed_at timestamptz not null default now(),
    changed_by text
);

-- ---------------------------------------------------------------- documents (DocumentManagement module)
create schema if not exists documents;

create table documents.documents (
    id              uuid primary key,
    tenant_id       uuid        not null,
    title           text        not null,
    category        text        not null check (category in ('invoice', 'contract', 'report', 'other')),
    status          text        not null check (status in ('draft', 'active', 'archived')),
    owner_id        uuid        not null,
    owner_name      text        not null,
    file_name       text        not null,
    content_type    text        not null,
    size_bytes      bigint      not null,
    tags            text[]      not null default '{}',
    current_version integer     not null default 1,
    created_at      timestamptz not null default now(),
    updated_at      timestamptz not null default now(),
    row_version     integer     not null default 1
);
create index ix_documents_category on documents.documents (tenant_id, category, status);

create table documents.document_versions (
    document_id uuid        not null references documents.documents (id) on delete cascade,
    version     integer     not null,
    file_name   text        not null,
    size_bytes  bigint      not null,
    comment     text        not null default '',
    created_by  text        not null,
    created_at  timestamptz not null default now(),
    primary key (document_id, version)
);

create table documents.invoices (
    id                uuid primary key,
    tenant_id         uuid        not null,
    document_id       uuid references documents.documents (id),
    number            text        not null unique,
    supplier          text        not null,
    amount            numeric(14, 2) not null check (amount >= 0),
    currency          text        not null default 'EUR',
    issued_on         date        not null,
    due_on            date        not null,
    cost_center       text        not null,
    status            text        not null check (status in ('draft', 'submitted', 'approved', 'rejected', 'paid')),
    submitted_by      uuid,
    submitted_by_name text,
    submitted_at      timestamptz,
    decided_by        uuid,
    decided_by_name   text,
    decided_at        timestamptz,
    comment           text        not null default '',
    created_at        timestamptz not null default now(),
    row_version       integer     not null default 1
);
create index ix_invoices_status on documents.invoices (tenant_id, status);

-- ---------------------------------------------------------------- reporting (Reporting module)
create schema if not exists reporting;

create table reporting.sales (
    id       bigserial primary key,
    sold_on  date           not null,
    region   text           not null,
    product  text           not null,
    channel  text           not null,
    quantity integer        not null,
    revenue  numeric(14, 2) not null
);
create index ix_sales_date on reporting.sales (sold_on);

create table reporting.report_definitions (
    id          text primary key,
    name        text  not null,
    description text  not null,
    category    text  not null,
    chart       text  not null default 'bar',
    parameters  jsonb not null default '[]',
    permission  text  not null default 'reports.run'
);

create table reporting.report_runs (
    id                uuid primary key,
    report_id         text        not null references reporting.report_definitions (id),
    parameters        jsonb       not null,
    requested_by      uuid        not null,
    requested_by_name text        not null,
    status            text        not null,
    result            jsonb,
    created_at        timestamptz not null default now(),
    finished_at       timestamptz
);
create index ix_report_runs_user on reporting.report_runs (requested_by, created_at desc);
