# TaskTune implementation progress

**Repository:** Kyrollo/tasktune-ai  
**Target branch:** main-reindex  
**Phase 1 baseline:** `0874d70c332b0e4220bca36f2a099ba9a9d42785`  
**Phase 2 working branch:** `work/phase2-tenant-isolation`  
**Workstream:** SaaS foundation — runtime, authentication, tenant isolation, resource authorization, and verification

This record tracks implementation against the product and engineering plan. It separates implemented protections from unfinished production controls.

## Completed — phase 1: runtime and authentication foundation

- Moved the active ASP.NET Core API, Application, and Infrastructure projects to .NET 10 and aligned EF Core and IdentityModel dependencies.
- Added repository-level SDK selection and GitHub Actions checks for API build, authentication regression tests, frontend lint/build, and Python syntax.
- Removed plaintext-password comparison. Login accepts BCrypt hashes only; no automatic plaintext migration is performed.
- Replaced raw refresh-token persistence with SHA-256 digests. Tokens are generated with a cryptographically secure random source and rotated after successful refresh.
- Returned the configured access-token expiry instead of a hard-coded one-hour value.
- Removed JWT claim-value logging and stopped returning exception details to API clients.
- Removed database credentials and the JWT signing key from tracked appsettings files. Startup fails closed if the connection string or signing key is absent or invalid.
- Disabled public self-service registration until tenant-scoped invitations and email verification can be enforced.
- Added auth regression tests for plaintext-password rejection, refresh-token digest storage, and refresh rotation.
- Corrected the setup guide and root README to describe the active project paths and reproducible checks.

## Completed — phase 2: tenant and resource access controls

- Added a request-scoped tenant context sourced from the authenticated JWT `companyId` claim and registered it with the active API.
- Added middleware that revalidates the authenticated user and active membership against the tenant claim on each authenticated request. Invalid or inactive memberships are rejected.
- Added fail-closed EF Core query filters for companies, users, departments, positions, projects, permissions, reports, communications, tasks, subtasks, comments, task access controls, dependencies, progress, attachments, evaluations, bugs, notifications, audit logs, AI action logs, chat threads/messages, and user settings. A missing tenant context maps to no visible tenant rows.
- Kept explicit query-filter bypasses only for identity lookup during login/refresh and cross-tenant email uniqueness checks during user provisioning. These bypasses must remain narrowly reviewed.
- Scoped task reads, updates, deletes, progress, AI guidance and subresource operations to the calling user’s applicable task relationship and tenant. Manager/supervisor-only actions now have explicit role checks.
- Added `CanAccessTaskAsync` as a shared task visibility check and applied it to subtasks, comments, attachments, bug reports and evaluation flows.
- Validated task create/update project and assignee references against the authenticated tenant.
- Reworked user administration so company identity comes from the signed claim, not request data; department/position references are checked; listing and record visibility are role-restricted; deactivation clears refresh credentials.
- Scoped project CRUD to the authenticated tenant and validated assigned managers against active tenant membership.
- Added role and record-scope checks to search, supervisor dashboards, report retrieval, AI report task inputs, and evaluations. Supervisors and managers see scoped tasks and their own reports; company-wide executive reporting is restricted to CEO/Admin, while HR remains authorized for HR reports.
- Ensured explicit permission denies take precedence over privileged-role page access shortcuts.
- Removed the chatbot’s model-generated SQL execution path; the model receives structured context assembled by the application instead.
- Moved new task attachments out of public webroot storage. Downloads now require authentication and task-scope authorization, validate storage paths, and return files as generic binary content with `nosniff`.
- Added cross-tenant and missing-tenant regression tests, plus same-tenant task-scope assertions.
- Restricted AI configuration, audit-log access and role-permission administration to elevated company roles.
- Disabled direct SMTP delivery from the communications endpoint until recipient approval, an auditable allowlist and idempotent delivery tracking exist. The endpoint records the communication history and responds with PendingApproval, but a persistent approval state/workflow is not yet implemented. CC entries are validated against active users in the same tenant.
- Restricted raw test/debug data endpoints to Development environment only.

## Verification evidence

- Phase 1 GitHub Actions run on `0874d70c332b0e4220bca36f2a099ba9a9d42785`: all three workflow jobs passed.
- Latest phase 2 GitHub Actions run on `0e2292be0c032042e5167ea0ca258e9c13a19408` (run [#44100782429](https://github.com/Kyrollo/tasktune-ai/actions/runs/44100782429)): backend build and regression tests passed; frontend lint and production build passed; Python agent syntax validation passed.
- Tenant-isolation and task-scope tests include EF Core InMemory regression coverage and a real SQL Server integration test in CI. The SQL Server test verifies that company and user query filters are translated and enforced by the SQL Server provider. It does not yet prove every relationship filter, deployment migration, rollback, concurrency path, or production data integrity.
- Phase 3 run [#44129659808](https://github.com/Kyrollo/tasktune-ai/actions/runs/44129659808) on the phase 3 tree: all three jobs passed, including 5 backend tests with the SQL Server integration test.
- Removed tracked local `.dotnet` and Vite cache artifacts and added ignore rules.
- Phase 4 agent-boundary run [#44132448022](https://github.com/Kyrollo/tasktune-ai/actions/runs/44132448022): all three CI jobs passed.
- Phase 5 encrypted-provider-secrets run [#44136064215](https://github.com/Kyrollo/tasktune-ai/actions/runs/44136064215): API build, all 6 backend tests (including encrypted secret handling and SQL Server query-filter translation), frontend lint/build, and Python tests passed.
- Local .NET execution was not available in the implementation environment; GitHub Actions is the build/test evidence.
- Frontend install reported 19 dependency advisories (1 low, 4 moderate, 14 high). No blind `npm audit fix` was applied; affected packages still need targeted triage and upgrades.

## Remaining release blockers and next implementation phases

1. Complete a full endpoint, SQL query, export, background-job and agent-tool authorization matrix. Phase 2 covered core people/work resources but should not be treated as proof that every route and downstream data path is fully scoped.
2. Expand SQL Server integration coverage to relationship-owned filters, foreign-key constraints, transactions and concurrency; add reviewed, versioned schema migrations and rehearse both forward and rollback paths.
3. Migrate legacy attachment rows/files from any previous public `wwwroot/uploads` location into private storage, verify metadata/path integrity, and confirm no public static copies remain.
4. Implement tenant-scoped invitations, email verification and secure tenant provisioning before re-enabling registration.
5. Implement password reset with expiring, single-use, hashed tokens and rate limiting.
6. Add refresh-token session-family tracking and reuse detection, device/session revocation and account recovery hardening.
7. Complete the communication approval workflow, authorized-recipient policy, delivery outbox/idempotency and provider credential isolation before re-enabling outbound SMTP.
8. Continue the full `IgnoreQueryFilters()`/raw-SQL audit and authorize every remaining downstream agent/tool path. Core API-to-agent requests now use a private service key and TLS for remote destinations; move provider keys to a dedicated managed secret vault and service-side references for larger multi-instance production deployments.
9. Configure production `Cors:AllowedOrigins`, `AgentServices:ServiceApiKey`, and `AISecrets:EncryptionKey` in the deployment secret manager; rotate all existing provider keys and protect/backup the encryption key. The API and agents now reject missing service authentication and arbitrary non-TLS remote agent URLs.
10. Triage the 19 npm dependency advisories (1 low, 4 moderate, 14 high), complete full dependency/license ownership review, secret rotation (previously committed secrets remain in Git history), telemetry, backups/restore drills, and independent security review.

## Release posture

This phase improves important security boundaries and has passing CI evidence on the working branch. It does **not** establish production readiness. Do not load customer production data until the remaining isolation, SQL Server integration, invitations/recovery, email approval, dependency, backup/restore, operations and independent security gates have passed.
