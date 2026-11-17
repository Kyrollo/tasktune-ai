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


## Completed — phase 6: invitations, password recovery, and revocable sessions

- Added SQL Server tables and the idempotent migration script `tasktune-backend/Script Database/Migrations/20261117_002_account_security.sql` for `UserSessions` and `AccountSecurityTokens`, including row-version concurrency control and tenant query filters.
- Replaced single-refresh-token storage with per-device session families. Refresh tokens are stored only as SHA-256 digests; rotation consumes the previous token and records its replacement.
- A consumed refresh token replay revokes its whole session family. Access tokens include a session ID and middleware rejects revoked, expired, consumed, missing, or foreign-user sessions on each authenticated request.
- Added per-session logout, device/session listing, and individual device-family revocation. Password reset and account deactivation revoke every active session.
- Added one-time, email-bound, 48-hour company invitations. The token is random, only its hash is stored, account creation takes company/role/department/position from the invitation record, and the invitee must open the email link to verify the address.
- Disabled public company self-provisioning. The admin Users page now sends an invitation rather than creating a password for another person; the invitee selects their own password.
- Added 30-minute single-use password-reset tokens, hashed at rest, generic account-existence responses, 12-character minimum passwords, BCrypt password storage, and rate limiting for login, refresh, registration, password-reset, and reset-token endpoints.
- Added TLS-required SMTP delivery configuration and HTTPS frontend-link configuration. Invitation and reset email delivery failures are handled without disclosing account existence.
- Updated Settings with current-session visibility and the ability to revoke other device sessions.
- Added integration-style regression tests covering invitation email binding/single use, password-reset single use, password hashing, session-family rotation/replay revocation, per-family logout, and password-reset session revocation.

## Verification evidence

- Phase 1 GitHub Actions run on `0874d70c332b0e4220bca36f2a099ba9a9d42785`: all three workflow jobs passed.
- Latest phase 2 GitHub Actions run on `0e2292be0c032042e5167ea0ca258e9c13a19408` (run [#44100782429](https://github.com/Kyrollo/tasktune-ai/actions/runs/44100782429)): backend build and regression tests passed; frontend lint and production build passed; Python agent syntax validation passed.
- Tenant-isolation and task-scope tests include EF Core InMemory regression coverage and a real SQL Server integration test in CI. The SQL Server test verifies that company and user query filters are translated and enforced by the SQL Server provider. It does not yet prove every relationship filter, deployment migration, rollback, concurrency path, or production data integrity.
- Phase 3 run [#44129659808](https://github.com/Kyrollo/tasktune-ai/actions/runs/44129659808) on the phase 3 tree: all three jobs passed, including 5 backend tests with the SQL Server integration test.
- Removed tracked local `.dotnet` and Vite cache artifacts and added ignore rules.
- Phase 4 agent-boundary run [#44132448022](https://github.com/Kyrollo/tasktune-ai/actions/runs/44132448022): all three CI jobs passed.
- Phase 5 encrypted-provider-secrets run [#44136064215](https://github.com/Kyrollo/tasktune-ai/actions/runs/44136064215): API build, all 6 backend tests (including encrypted secret handling and SQL Server query-filter translation), frontend lint/build, and Python tests passed.
- Phase 6 account-security run [#44160588342](https://github.com/Kyrollo/tasktune-ai/actions/runs/44160588342): build and all 9 backend tests passed, including SQL Server integration tests, session replay/logout, invitation single-use, and password-reset revocation.
- Final phase 6 UI/branch run [#44161645592](https://github.com/Kyrollo/tasktune-ai/actions/runs/44161645592): backend, frontend lint/production build, and Python agent validation all passed.
- Local .NET execution was not available in the implementation environment; GitHub Actions is the build/test evidence.
- Frontend install reported 19 dependency advisories (1 low, 4 moderate, 14 high). No blind `npm audit fix` was applied; affected packages still need targeted triage and upgrades.

## Remaining release blockers and next implementation phases

1. Complete a full endpoint, SQL query, export, background-job and agent-tool authorization matrix. Phase 2 covered core people/work resources but should not be treated as proof that every route and downstream data path is fully scoped.
2. Expand SQL Server integration coverage to relationship-owned filters, foreign-key constraints, transactions and concurrency; add reviewed, versioned schema migrations and rehearse both forward and rollback paths.
3. Migrate legacy attachment rows/files from any previous public `wwwroot/uploads` location into private storage, verify metadata/path integrity, and confirm no public static copies remain.
4. Configure production SMTP and HTTPS frontend origins, apply the account-security migration, and run end-to-end invitation delivery/verification in a deployment environment before re-enabling invitation flows for customers. Self-service company creation remains intentionally disabled.
5. Password reset and recovery are implemented with expiring, single-use, hashed tokens and rate limiting; validate end-to-end delivery with the production SMTP provider.
6. Refresh-token session-family tracking, replay detection, per-device revocation, and access-token session checks are implemented and covered by regression tests.
7. Complete the persistent communication approval workflow, authorized-recipient policy, delivery outbox/idempotency and approval UI before enabling external communications.
8. Continue the full `IgnoreQueryFilters()`/raw-SQL audit and authorize every remaining downstream agent/tool path. Core API-to-agent requests now use a private service key and TLS for remote destinations; move provider keys to a dedicated managed secret vault and service-side references for larger multi-instance production deployments.
9. Configure production `Cors:AllowedOrigins`, `AgentServices:ServiceApiKey`, and `AISecrets:EncryptionKey` in the deployment secret manager; rotate all existing provider keys and protect/backup the encryption key. The API and agents now reject missing service authentication and arbitrary non-TLS remote agent URLs.
10. Triage the 19 npm dependency advisories (1 low, 4 moderate, 14 high), complete full dependency/license ownership review, secret rotation (previously committed secrets remain in Git history), telemetry, backups/restore drills, and independent security review.

## Release posture

This repository has passing CI evidence for the implemented phases, but CI does **not** establish production readiness. The account-security SQL migration must be applied and verified on a backed-up deployment database; production SMTP, stable encryption keys, legacy file migration, communication approval, dependency remediation, backup/restore, telemetry, and independent security review remain release gates.


## Completed — phase 7 foundation: communication approvals and durable outbox staging

- Added tenant-scoped communication approval records with requester, recipient/CC snapshots, content, status, timestamps, rowversion and caller-supplied idempotency key.
- Enforced same-tenant recipient selection, unique recipient IDs, maximum audience size, valid recipient email addresses and active same-tenant CC allowlisting.
- Added HR/CEO/Admin approval and rejection endpoints. Requesters cannot approve or reject their own messages; rejection requires a reason.
- Approval state and all recipient outbox rows are committed in one EF Core SaveChanges transaction. Outbox records use deterministic SHA-256 idempotency keys with a database unique index.
- Added the SQL Server migration `20261117_003_communication_approval_outbox.sql`, EF entities, indexes and tenant query filters.
- Updated the Communication Center to submit for approval, inspect the pending queue, approve or reject requests, and accurately state that submission does not mean delivery.
- **Not enabled:** this change does not run an outbox dispatcher and does not send external email. Delivery retries, atomic claim/lease logic, dead-letter handling, reconciliation and deployment configuration remain release gates.

## Verification for phase 7 foundation

- Added regression tests for maker-checker separation, tenant scoping, one-time approval/rejection, per-recipient outbox creation and deterministic idempotency keys.
- GitHub Actions verification is triggered by the grouped implementation commit; results will be recorded only after the run completes.
- SQL Server migration application and rowversion concurrency must be verified against the CI SQL Server and then rehearsed in a backed-up deployment environment.

## Remaining implementation and production gates

1. Finish the exhaustive authorization matrix covering endpoints, raw SQL, exports, scheduled tasks, caches, uploads/downloads, webhooks and agent tools. Add negative tests for each privileged boundary and cross-tenant resource.
2. Expand SQL Server schema and migration coverage: relationship-owned query filters, composite foreign keys, transactions, rowversion races, migration repeatability, forward compatibility and backup/restore rehearsal.
3. Build a private legacy attachment migration utility with inventory, checksum validation, traversal/symlink protection, transactional metadata updates and a rollback manifest. Do not delete old files before verification and backup.
4. Implement the outbox dispatcher with atomic SQL Server claim/lease semantics, retry/backoff with jitter, max-attempt dead-letter handling, idempotent provider delivery, reconciliation, redacted logs and per-tenant quotas. Keep it disabled by default.
5. Complete outbound recipient policy, approval notifications, bounce handling and delivery audit. Production SMTP must remain disabled until end-to-end staging tests pass.
6. Finish AI tool authorization, RAG document ACL enforcement, provider capability registry, usage budgets, egress allowlisting and managed secret-vault integration; verify prompt-injection and cross-tenant adversarial scenarios.
7. Add enterprise MFA/SSO, frontend BFF/HttpOnly cookie migration, CSRF controls and privileged-account access reviews.
8. Triage dependency advisories with targeted upgrades, lockfile/SBOM/provenance checks, license ownership review and secret-history rotation.
9. Add OpenTelemetry traces/metrics, security and cost alerts, health probes, status/support runbooks, encrypted off-host backups and proven restore drills.
10. Complete privacy/retention and first-country payroll legal/accounting reviews, then run independent penetration testing and a risk-based E2E release campaign.
11. Only after critical gates pass, complete P8 launch readiness. P9 expansion (additional countries, dedicated tenancy, customer-hosted deployment, offline mobile and data warehouse) remains staged.

## Release posture

Phase 7 provides durable approval and delivery staging, not actual delivery. CI success alone is not production readiness. Do not enable outbound SMTP or load customer production data until dispatcher, migration/restore, tenant isolation, recipient policy, dependency, monitoring, backup and independent security gates are verified.
