-- =============================================================================
-- Green Hive - demo data
-- Tenant "Green Hive Demo GmbH" with users, roles, departments, documents, invoices, sales figures and reports.
-- Every demo user has the password  GreenHive!2026
-- =============================================================================

-- ---------------------------------------------------------------- tenant
insert into identity.tenants (id, name) values ('00000000-0000-0000-0000-000000000001', 'Green Hive Demo GmbH');

-- ---------------------------------------------------------------- permissions (normally registered from the module manifests)
insert into identity.permissions (name, module_id, description) values
    ('*',                 'shell',               'All permissions'),
    ('modules.view',      'shell',               'See the module catalogue'),
    ('modules.publish',   'shell',               'Publish module versions'),
    ('updates.manage',    'shell',               'Manage module rollouts'),
    ('settings.edit',     'shell',               'Change tenant settings'),
    ('users.view',        'user-management',     'See users and departments'),
    ('users.edit',        'user-management',     'Create and edit users'),
    ('roles.edit',        'user-management',     'Assign roles and permissions'),
    ('workflows.view',    'shell',               'See workflows and instances'),
    ('workflows.edit',    'shell',               'Design workflows'),
    ('workflows.run',     'shell',               'Start workflows and work on inbox tasks'),
    ('workflows.approve', 'shell',               'Approve workflow steps'),
    ('ai.run',            'shell',               'Hand workflows over to the AI agent'),
    ('ai.approve',        'shell',               'Approve AI proposals'),
    ('ai.admin',          'shell',               'Switch the AI agent on or off'),
    ('documents.view',    'document-management', 'See documents and invoices'),
    ('documents.edit',    'document-management', 'Create and edit documents and invoices'),
    ('documents.approve', 'document-management', 'Approve or reject invoices'),
    ('reports.view',      'reporting',           'See reports and results'),
    ('reports.run',       'reporting',           'Run reports');

-- ---------------------------------------------------------------- roles
insert into identity.roles (id, tenant_id, name, description) values
    ('20000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', 'Administrator', 'Full access to every module and setting'),
    ('20000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'Approver',      'Approves invoices and workflow steps'),
    ('20000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', 'Clerk',         'Captures documents and invoices'),
    ('20000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000001', 'Analyst',       'Runs reports'),
    ('20000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000001', 'Viewer',        'Read-only access');

insert into identity.role_permissions (role_id, permission)
select '20000000-0000-0000-0000-000000000001'::uuid, '*'
union all
select '20000000-0000-0000-0000-000000000002'::uuid, p from unnest(array[
    'documents.view', 'documents.approve', 'workflows.view', 'workflows.run', 'workflows.approve', 'ai.approve', 'ai.run',
    'reports.view', 'users.view', 'modules.view']) p
union all
select '20000000-0000-0000-0000-000000000003'::uuid, p from unnest(array[
    'documents.view', 'documents.edit', 'workflows.view', 'workflows.run', 'ai.run', 'reports.view', 'users.view', 'modules.view']) p
union all
select '20000000-0000-0000-0000-000000000004'::uuid, p from unnest(array[
    'reports.view', 'reports.run', 'documents.view', 'workflows.view', 'modules.view']) p
union all
select '20000000-0000-0000-0000-000000000005'::uuid, p from unnest(array[
    'documents.view', 'reports.view', 'users.view', 'modules.view']) p;

-- ---------------------------------------------------------------- users (password: GreenHive!2026)
insert into identity.users (id, tenant_id, user_name, display_name, email, password_hash, is_active, approval_limit, created_at, last_login_at) values
    ('10000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', 'admin',         'Alex Admin',    'admin@greenhive.demo',         'pbkdf2$100000$Tt2q0e4Jhv8ZB0stjvTNfQ==$Q7NxjmUvhMjY6Prjmg8XldQXocvlUrbebDYunr87PkA=', true,  1000000, now() - interval '400 days', now() - interval '1 day'),
    ('10000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'maria.huber',   'Maria Huber',   'maria.huber@greenhive.demo',   'pbkdf2$100000$Tt2q0e4Jhv8ZB0stjvTNfQ==$Q7NxjmUvhMjY6Prjmg8XldQXocvlUrbebDYunr87PkA=', true,  10000,   now() - interval '380 days', now() - interval '2 days'),
    ('10000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', 'lukas.berger',  'Lukas Berger',  'lukas.berger@greenhive.demo',  'pbkdf2$100000$Tt2q0e4Jhv8ZB0stjvTNfQ==$Q7NxjmUvhMjY6Prjmg8XldQXocvlUrbebDYunr87PkA=', true,  0,       now() - interval '300 days', now() - interval '3 hours'),
    ('10000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000001', 'sophie.wagner', 'Sophie Wagner', 'sophie.wagner@greenhive.demo', 'pbkdf2$100000$Tt2q0e4Jhv8ZB0stjvTNfQ==$Q7NxjmUvhMjY6Prjmg8XldQXocvlUrbebDYunr87PkA=', true,  0,       now() - interval '250 days', now() - interval '5 days'),
    ('10000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000001', 'david.novak',   'David Novak',   'david.novak@greenhive.demo',   'pbkdf2$100000$Tt2q0e4Jhv8ZB0stjvTNfQ==$Q7NxjmUvhMjY6Prjmg8XldQXocvlUrbebDYunr87PkA=', true,  0,       now() - interval '200 days', null),
    ('10000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000001', 'elena.rossi',   'Elena Rossi',   'elena.rossi@greenhive.demo',   'pbkdf2$100000$Tt2q0e4Jhv8ZB0stjvTNfQ==$Q7NxjmUvhMjY6Prjmg8XldQXocvlUrbebDYunr87PkA=', true,  50000,   now() - interval '500 days', now() - interval '6 hours'),
    ('10000000-0000-0000-0000-000000000007', '00000000-0000-0000-0000-000000000001', 'tom.fischer',   'Tom Fischer',   'tom.fischer@greenhive.demo',   'pbkdf2$100000$Tt2q0e4Jhv8ZB0stjvTNfQ==$Q7NxjmUvhMjY6Prjmg8XldQXocvlUrbebDYunr87PkA=', false, 0,       now() - interval '700 days', now() - interval '120 days');

insert into identity.user_roles (user_id, role_id) values
    ('10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001'),
    ('10000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000002'),
    ('10000000-0000-0000-0000-000000000003', '20000000-0000-0000-0000-000000000003'),
    ('10000000-0000-0000-0000-000000000004', '20000000-0000-0000-0000-000000000004'),
    ('10000000-0000-0000-0000-000000000005', '20000000-0000-0000-0000-000000000005'),
    ('10000000-0000-0000-0000-000000000006', '20000000-0000-0000-0000-000000000002'),
    ('10000000-0000-0000-0000-000000000006', '20000000-0000-0000-0000-000000000004'),
    ('10000000-0000-0000-0000-000000000007', '20000000-0000-0000-0000-000000000003');

-- ---------------------------------------------------------------- departments and profiles (UserManagement)
insert into usermgmt.departments (id, tenant_id, name, cost_center) values
    ('30000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', 'Finance',     'CC-1000'),
    ('30000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'Accounting',  'CC-1100'),
    ('30000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', 'Controlling', 'CC-1200'),
    ('30000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000001', 'Sales',       'CC-2000'),
    ('30000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000001', 'IT',          'CC-3000');

insert into usermgmt.profiles (user_id, department_id, job_title, phone, location, manager_id, hired_on) values
    ('10000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000005', 'IT Administrator',      '+43 1 234 5601', 'Vienna',   null,                                   '2021-03-01'),
    ('10000000-0000-0000-0000-000000000006', '30000000-0000-0000-0000-000000000001', 'Head of Finance',       '+43 1 234 5606', 'Vienna',   null,                                   '2019-09-15'),
    ('10000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000001', 'Finance Manager',       '+43 1 234 5602', 'Vienna',   '10000000-0000-0000-0000-000000000006', '2022-01-10'),
    ('10000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000002', 'Accounts Payable Clerk','+43 1 234 5603', 'Graz',     '10000000-0000-0000-0000-000000000002', '2023-02-01'),
    ('10000000-0000-0000-0000-000000000004', '30000000-0000-0000-0000-000000000003', 'Controller',            '+43 1 234 5604', 'Linz',     '10000000-0000-0000-0000-000000000006', '2023-06-01'),
    ('10000000-0000-0000-0000-000000000005', '30000000-0000-0000-0000-000000000004', 'Sales Representative',  '+43 1 234 5605', 'Salzburg', null,                                   '2024-04-15'),
    ('10000000-0000-0000-0000-000000000007', '30000000-0000-0000-0000-000000000002', 'Accounting Assistant',  '+43 1 234 5607', 'Graz',     '10000000-0000-0000-0000-000000000002', '2020-05-01');

-- ---------------------------------------------------------------- settings
insert into config.settings (tenant_id, scope, owner, key, value, updated_by) values
    ('00000000-0000-0000-0000-000000000001', 'tenant', '', 'ai.enabled',            'true',      'admin'),
    ('00000000-0000-0000-0000-000000000001', 'tenant', '', 'ai.modelProfile',       '"default"', 'admin'),
    ('00000000-0000-0000-0000-000000000001', 'tenant', '', 'workflows.autoMigrate', 'true',      'admin'),
    ('00000000-0000-0000-0000-000000000001', 'tenant', '', 'updates.pilotUsers',
        '["10000000-0000-0000-0000-000000000001", "10000000-0000-0000-0000-000000000002"]', 'admin'),
    ('00000000-0000-0000-0000-000000000001', 'tenant', '', 'company.name',          '"Green Hive Demo GmbH"', 'admin'),
    ('00000000-0000-0000-0000-000000000001', 'user', '10000000-0000-0000-0000-000000000001', 'shell.language', '"en"', 'admin');

insert into ai.switches (scope, enabled, changed_by) values ('global', true, 'admin');

-- ---------------------------------------------------------------- documents
insert into documents.documents (id, tenant_id, title, category, status, owner_id, owner_name, file_name, content_type, size_bytes, tags, current_version, created_at, updated_at)
select
    ('40000000-0000-0000-0000-' || lpad(i::text, 12, '0'))::uuid,
    '00000000-0000-0000-0000-000000000001',
    (array['Framework agreement', 'Service contract', 'NDA', 'Quarterly report', 'Project plan', 'Board minutes',
           'Supplier contract', 'Price list', 'Market analysis', 'Security policy'])[1 + (i % 10)] || ' ' || (2024 + i % 3)::text || '-' || lpad((1 + i % 12)::text, 2, '0'),
    (array['contract', 'contract', 'contract', 'report', 'other', 'report', 'contract', 'other', 'report', 'other'])[1 + (i % 10)],
    case when i % 9 = 0 then 'archived' when i % 7 = 0 then 'draft' else 'active' end,
    (array['10000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000002'])[1 + (i % 3)]::uuid,
    (array['Lukas Berger', 'Sophie Wagner', 'Maria Huber'])[1 + (i % 3)],
    'document-' || i || '.pdf',
    'application/pdf',
    40000 + (i * 7919) % 900000,
    case when i % 4 = 0 then array['important'] when i % 4 = 1 then array['2026', 'q' || (1 + i % 4)] else '{}'::text[] end,
    1 + (i % 3),
    now() - (i * 5 || ' days')::interval,
    now() - (i * 2 || ' days')::interval
from generate_series(1, 30) as i;

insert into documents.document_versions (document_id, version, file_name, size_bytes, comment, created_by, created_at)
select d.id, v, replace(d.file_name, '.pdf', '-v' || v || '.pdf'), d.size_bytes - (d.current_version - v) * 1000,
       case when v = 1 then 'Initial upload' else 'Revised version ' || v end, d.owner_name, d.created_at + (v || ' days')::interval
from documents.documents d, generate_series(1, 3) v
where v <= d.current_version;

-- invoices: every invoice has its scanned document
insert into documents.documents (id, tenant_id, title, category, status, owner_id, owner_name, file_name, content_type, size_bytes, tags, current_version, created_at, updated_at)
select
    ('41000000-0000-0000-0000-' || lpad(i::text, 12, '0'))::uuid,
    '00000000-0000-0000-0000-000000000001',
    'Invoice INV-2026-' || lpad(i::text, 4, '0'),
    'invoice', 'active',
    '10000000-0000-0000-0000-000000000003', 'Lukas Berger',
    'INV-2026-' || lpad(i::text, 4, '0') || '.pdf', 'application/pdf', 60000 + i * 731,
    array['invoice'], 1,
    now() - ((40 - i) || ' days')::interval, now() - ((40 - i) || ' days')::interval
from generate_series(1, 28) as i;

insert into documents.document_versions (document_id, version, file_name, size_bytes, comment, created_by, created_at)
select id, 1, file_name, size_bytes, 'Scanned invoice', owner_name, created_at from documents.documents where category = 'invoice';

insert into documents.invoices (id, tenant_id, document_id, number, supplier, amount, currency, issued_on, due_on, cost_center, status,
                                submitted_by, submitted_by_name, submitted_at, decided_by, decided_by_name, decided_at, comment, created_at)
select
    ('42000000-0000-0000-0000-' || lpad(i::text, 12, '0'))::uuid,
    '00000000-0000-0000-0000-000000000001',
    ('41000000-0000-0000-0000-' || lpad(i::text, 12, '0'))::uuid,
    'INV-2026-' || lpad(i::text, 4, '0'),
    (array['ACME Office Supplies', 'Bluewave Logistics', 'Cloudnine Hosting', 'Danube Consulting', 'Edelweiss Catering',
           'Fjord Software', 'Granite Facility Services', 'Helix Marketing'])[1 + (i % 8)],
    case when i in (5, 17) then 24500.00 + i * 10 when i = 11 then 62000.00 else round(((i * 7919) % 9000 + 180 + (i * 13 % 100) / 100.0)::numeric, 2) end,
    'EUR',
    current_date - (45 - i),
    current_date - (45 - i) + 30,
    (array['CC-1000', 'CC-1100', 'CC-1200', 'CC-2000', 'CC-3000'])[1 + (i % 5)],
    case
        when i <= 8 then 'paid'
        when i <= 14 then 'approved'
        when i in (15, 16) then 'rejected'
        when i <= 23 then 'submitted'
        else 'draft'
    end,
    case when i <= 23 then '10000000-0000-0000-0000-000000000003'::uuid end,
    case when i <= 23 then 'Lukas Berger' end,
    case when i <= 23 then now() - ((40 - i) || ' days')::interval + interval '2 hours' end,
    case when i <= 16 then (case when i % 2 = 0 then '10000000-0000-0000-0000-000000000002' else '10000000-0000-0000-0000-000000000006' end)::uuid end,
    case when i <= 16 then (case when i % 2 = 0 then 'Maria Huber' else 'Elena Rossi' end) end,
    case when i <= 16 then now() - ((38 - i) || ' days')::interval end,
    case when i in (15, 16) then 'Duplicate of an earlier invoice' when i <= 14 then 'Checked against the order' else '' end,
    now() - ((40 - i) || ' days')::interval
from generate_series(1, 28) as i;

-- ---------------------------------------------------------------- sales figures (Reporting)
insert into reporting.sales (sold_on, region, product, channel, quantity, revenue)
select d::date, r.region, p.product, (array['Online', 'Retail', 'Partner'])[1 + abs(hashtext(d::text || r.region || p.product)) % 3],
       q.quantity,
       round((q.quantity * p.price * (1 + 0.15 * sin(extract(doy from d)::double precision / 58.0)) * r.factor)::numeric, 2)
from generate_series(date '2025-01-01', current_date - 1, interval '1 day') d
cross join (values ('North', 1.10), ('South', 0.95), ('East', 0.85), ('West', 1.20)) as r(region, factor)
cross join (values ('Hive Basic', 49.0), ('Hive Pro', 129.0), ('Hive Enterprise', 499.0), ('Honey Sensor', 29.0),
                   ('Queen Gateway', 249.0), ('Comb Storage', 89.0)) as p(product, price)
cross join lateral (select 1 + abs(hashtext(d::text || r.region || p.product)) % 12 as quantity) q
where abs(hashtext(d::text || p.product || r.region || 'x')) % 3 <> 0;

insert into reporting.report_definitions (id, name, description, category, chart, parameters, permission) values
    ('sales-by-region', 'Sales by region', 'Revenue and units per region in a period.', 'Sales', 'bar',
     '[{"name":"from","type":"date","label":"From","default":"2026-01-01","required":true},
       {"name":"to","type":"date","label":"To","required":true}]', 'reports.run'),
    ('sales-by-month', 'Sales by month', 'Monthly revenue of a year.', 'Sales', 'line',
     '[{"name":"year","type":"number","label":"Year","default":"2026","required":true,"min":2025,"max":2030}]', 'reports.run'),
    ('top-products', 'Top products', 'Best-selling products by revenue.', 'Sales', 'bar',
     '[{"name":"from","type":"date","label":"From","default":"2026-01-01","required":true},
       {"name":"to","type":"date","label":"To","required":true},
       {"name":"top","type":"number","label":"Number of products","default":"5","min":1,"max":20}]', 'reports.run'),
    ('invoice-aging', 'Invoice aging', 'Open invoices grouped by days until due.', 'Finance', 'bar', '[]', 'reports.run'),
    ('approval-throughput', 'Approval throughput', 'Approved and rejected invoices per approver.', 'Finance', 'bar', '[]', 'reports.run');
