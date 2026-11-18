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
- Added a hosted outbox dispatcher with atomic SQL Server `UPDLOCK`/`READPAST` claims, expiring leases, bounded retries with jitter, dead-letter state, redacted failure records and stable RFC Message-ID values.
- The dispatcher is controlled by `Email:Outbox:Enabled` and defaults to off. It sends one recipient per outbox row and supports the validated CC snapshot.
- SMTP is at-least-once, not exactly-once: a worker crash after provider acceptance but before the Sent update can result in a retry. The stable Message-ID is a deduplication aid only; provider-side idempotency or manual reconciliation is still required for ambiguous delivery outcomes.
- Hardened concurrent idempotency-key insert races and concurrent maker-checker transitions so database uniqueness/concurrency conflicts resolve to the already-persisted result instead of producing duplicate outbox entries or an unhandled 500.
- Added a terminal retry-budget transition: expired Processing leases or due Pending rows at the maximum attempts are moved to DeadLetter and excluded from further claims until operator reconciliation.

## Verification for phase 7 foundation

- Added regression tests for maker-checker separation, tenant scoping, one-time approval/rejection, per-recipient outbox creation and deterministic idempotency keys.
- Approval/outbox foundation commit `65652dceb4af68744d8490f86ca91e11deae1dbd` passed all three GitHub Actions jobs in run [#44182304265](https://github.com/Kyrollo/tasktune-ai/actions/runs/44182304265): API build and regression tests, frontend lint/build, and Python syntax/tests.
- Dispatcher commit initially exposed a compile-time query-chain error in the atomic claim method. The follow-up fix moves `FromSqlInterpolated` to the `DbSet` root before applying `IgnoreQueryFilters`; the corrected commit is now under CI verification. The SQL Server concurrency test covers competing worker claims, expired-lease reclamation and retry-budget exhaustion to DeadLetter.
- The SQL Server lease migration must still be applied and rehearsed in a backed-up deployment environment before enabling SMTP.

## Remaining implementation and production gates

1. Complete the exhaustive endpoint-to-entity-to-action authorization matrix covering raw SQL, exports, scheduled tasks, caches, uploads/downloads, webhooks and agent tools. Controller authorization metadata and the 27-entity global-filter presence are now regression-tested; object/field-level and downstream negative tests remain open.
2. Expand SQL Server schema and migration coverage: relationship-owned query filters, composite foreign keys, transactions, rowversion races, migration repeatability, forward compatibility and backup/restore rehearsal.
3. Build and validate the private legacy attachment migration utility. The standalone tool now defaults to dry-run, validates flat legacy /Uploads paths, rejects traversal and reparse-point paths, checks file sizes and SHA-256, copies into the active private storage-key layout, updates metadata with a compare-and-set SQL transaction, and writes a prepared/committed JSONL rollback manifest. It never deletes legacy or private files. Deployment-specific dry-run, backup, apply and rollback rehearsal remain required.
4. Complete provider-side idempotency or manual reconciliation for ambiguous SMTP outcomes; add per-tenant delivery quotas, queue-age metrics/alerts and bounce processing. Retry exhaustion and the CEO/Admin manual dead-letter replay screen are implemented; the dispatcher remains disabled by default.
5. Complete outbound recipient policy, approval notifications, bounce handling and delivery audit. Production SMTP must remain disabled until end-to-end staging tests pass.
6. Finish AI tool authorization, RAG document ACL enforcement, provider capability registry, usage budgets, egress allowlisting and managed secret-vault integration; verify prompt-injection and cross-tenant adversarial scenarios.
7. Add enterprise MFA/SSO, frontend BFF/HttpOnly cookie migration, CSRF controls and privileged-account access reviews.
8. Triage dependency advisories with targeted upgrades, lockfile/SBOM/provenance checks, license ownership review and secret-history rotation.
9. Add OpenTelemetry traces/metrics, security and cost alerts, health probes, status/support runbooks, encrypted off-host backups and proven restore drills.
10. Complete privacy/retention and first-country payroll legal/accounting reviews, then run independent penetration testing and a risk-based E2E release campaign.
11. Only after critical gates pass, complete P8 launch readiness. P9 expansion (additional countries, dedicated tenancy, customer-hosted deployment, offline mobile and data warehouse) remains staged.

## Release posture

Phase 7 provides durable approval, delivery staging and an opt-in SMTP dispatcher. The dispatcher is disabled by default and SMTP remains at-least-once. CI success alone is not production readiness. Do not enable outbound SMTP or load customer production data until the lease migration, provider behavior, tenant isolation, recipient policy, dependency, monitoring, backup and independent security gates are verified.


## Completed — legacy attachment migration tooling foundation

- Added standalone .NET 10 utility `tasktune-backend/TaskTuneAPI/TaskTune.AttachmentMigration` with the SQL connection and physical storage roots supplied only through environment variables.
- Dry-run is the default; `--apply` is required for any file copy or metadata update. A SQL Server application lock prevents simultaneous invocations.
- Only legacy `/Uploads/{singleFileName}` records are accepted. Traversal, nested paths, unsupported extensions and symbolic-link/reparse-point paths are rejected.
- File copies are streamed and SHA-256 verified before a compare-and-set SQL transaction updates `TaskAttachments.FilePath`.
- An append-only JSONL manifest records prepared and committed states. `--rollback` restores metadata only if the row still points at this tool's private key and the private copy verifies.
- Neither apply nor rollback deletes files. Actual deployment roots, backups, real migration execution and post-migration application validation remain deployment-specific and have not been run from CI.


## Operational readiness checkpoint — 18 November 2026

- The communication approval/outbox foundation and private legacy-attachment migration utility are present on `main-reindex`. The outbox worker remains opt-in and the attachment utility remains dry-run by default.
- Added `docs/engineering/production-readiness-runbook.md` covering the ordered SQL migration sequence, backup/restore evidence, private attachment cutover, SMTP enablement gates, observability, incident response and go/no-go criteria.
- This checkpoint is documentation and operational sequencing only. No production database, SMTP provider, legacy upload root, secret manager, backup target or restore environment was accessed or changed.
- Current branch head at checkpoint: `6450884b59de11f64b791d49d02185cdbe78a5e1`. The GitHub connector did not return a CI run for this head; do not infer a passing build from earlier phase runs.
- Next implementation work remains: finish the exhaustive authorization matrix and adversarial tests; validate migrations and attachment migration against a disposable SQL Server/deployment-like environment; implement operator-facing dead-letter reconciliation and delivery audit; add telemetry/health probes and backup automation; remediate dependency and secret-history findings; then complete independent security and launch review.


## Completed — authorization metadata and tenant-filter regression gate

- Marked the five intended anonymous AuthController actions and the coarse health endpoint explicitly with `[AllowAnonymous]`; authenticated session actions remain protected.
- Added reflection-based API metadata tests requiring every routed API action to declare class- or method-level `[Authorize]` or explicit `[AllowAnonymous]`.
- Added an exact anonymous-action allowlist test, so adding a new public endpoint requires a deliberate test/documentation change.
- Added tenant model metadata tests for all 27 tenant-owned EF entities and fail-closed behavior when no tenant context is present.
- Added `docs/security/endpoint-authorization-audit.md` with the reviewed public endpoint boundary, authorization layers and known remaining audit scope.
- These checks cover controller metadata and global-filter presence; they do not prove all object-level, field-level, agent, raw-SQL, export, cache or background-job paths. The exhaustive matrix and adversarial tests remain open.


## Authorization metadata CI correction

- Run #76 caught a compile-time issue in the generic test helper that attempted to call the non-generic EF `DbContext.Set(Type)` API. The redundant dynamic-set test was removed; existing fail-closed behavior remains covered by `TenantIsolationTests.MissingTenantContext_FailsClosed`, while the new metadata test continues to assert filters on every listed tenant-owned type.
- The corrected grouped fix is being revalidated by the full GitHub Actions workflow before the authorization audit gate is considered green.


## Authorization metadata CI correction — health probes

- Run #77 confirmed the test helper compiles and the EF filter metadata test passes, but the new authorization test correctly exposed three health actions (live, ready, compatibility check) that were not declared anonymous after the health controller had been split into separate probe routes.
- The health controller is intentionally public for orchestrator probes; it now declares class-level `[AllowAnonymous]`, and all three actions are explicitly included in the reviewed allow-list and endpoint documentation.
- CI is being rerun against the updated grouped changes. No health endpoint returns tenant data or infrastructure connection details.


## Completed — outbox dead-letter reconciliation controls

- Added tenant-scoped CEO/Admin endpoints to list dead-lettered outbox rows and manually replay a selected row with a required reconciliation reason.
- Replay is allowed only while the parent approval remains Approved and the outbox row is still DeadLetter. It resets attempts/lease state, returns the row to Pending, and preserves the original idempotency key/Message-ID.
- The requeue and tenant audit event are saved atomically. Concurrent or stale replay attempts return Conflict; cross-tenant identifiers return Not Found.
- Added a Communication Center dead-letter card, provider-log reconciliation warning, mandatory notes and an explicit browser confirmation before replay.
- Added regression tests for successful replay, audit persistence, idempotency-key preservation, repeated replay rejection, cross-tenant denial and rejected-parent denial.
- Added `docs/operations/communication-dead-letter-replay.md` for operator steps and duplicate-delivery cautions.

## Remaining outbox delivery controls

- Provider-side idempotency or manual reconciliation for ambiguous SMTP outcomes remains necessary; the stable RFC Message-ID is not a guarantee of exactly-once delivery.
- Per-tenant delivery quotas, queue-age metrics/alerts, bounce processing and a staging SMTP verification remain open. The worker stays disabled by default.


## Dead-letter replay CI correction

- Run #79 confirmed the API and attachment utility compile, but the new test file imported the TaskTune domain namespace containing an entity named `Task`, making unqualified test method return types ambiguous with `System.Threading.Tasks.Task`.
- Qualified the test method return types explicitly. No production code behavior or outbox schema changed in this correction; the complete workflow is being rerun.


## Dead-letter replay CI correction — ASP.NET HTTP test reference

- Run #80 confirms API build and attachment-tool build, plus frontend/Python jobs, succeeded. The test project itself needed an explicit `Microsoft.AspNetCore.Http` import for `DefaultHttpContext` in the new replay-service tests.
- Added the missing import; production code and replay behavior are unchanged. Full CI is rerunning.


## Replay safety hardening

- The service and HTTP API now require explicit `providerConfirmedNotAccepted=true` as well as a reconciliation reason. Requests without it leave the row in DeadLetter and create no audit event.
- Added a negative backend regression test for replay without provider non-acceptance confirmation. The browser prompt now explicitly asks the operator to confirm provider-log evidence.
- This is an operator attestation, not an automated provider verification. At-least-once SMTP semantics and the possibility of duplicates after ambiguous remote acceptance remain.


## Replay confirmation test correction

- The first replay-confirmation CI run found one repeated-replay test call that had not supplied the new provider non-acceptance attestation argument. Updated that call to test the intended second-replay conflict rather than the missing-confirmation guard.


## Completed — tenant delivery quota and queue visibility foundation

- Added configurable per-approval recipient cap (Email:Outbox:MaxRecipientsPerApproval, default 250; maximum 1,000) and per-tenant active backlog cap (Email:Outbox:MaxPendingPerTenant, default 1,000).
- Approval quota checks count Pending and Processing rows for the same tenant and return HTTP 429 without approving or inserting outbox rows when a limit is exceeded.
- Relational approval transitions use a SQL Server SERIALIZABLE transaction around the backlog check and approval/outbox write to protect the quota from ordinary concurrent approvals. In-memory regression tests verify limits and no-partial-write behavior; SQL Server execution remains a CI integration gate.
- Added a CEO/Admin tenant-scoped queue metrics endpoint reporting Pending, Processing, Sent, Failed, DeadLetter, oldest pending/processing age, worker state and queue-age warning threshold.
- Added safe appsettings defaults. Email:Outbox:Enabled remains false.


## Completed — Communication Center queue health panel

- Added a CEO/Admin queue health panel with Pending, Processing, Sent and DeadLetter totals, oldest pending item age, worker enabled/disabled state and the configured age-warning threshold.
- Metrics refresh with approval/dead-letter data after operator actions. The panel is hidden when the metrics endpoint is not available to the current role.


## Queue metrics CI correction

- The first queue quota/metrics build found that the new metrics service used EF Core query extensions without importing the EF Core namespace. Added the missing import; no runtime behavior changed. Full backend/frontend/Python CI is rerunning.


## Queue quota test dependency correction

- CI confirmed the API and metrics service build, but the quota test required the Microsoft.Extensions.Configuration.Memory provider used by its in-memory configuration setup. Added the test-project package reference at the same .NET 10 package version as the existing configuration dependency.


## Completed — communication approval notifications

- Successful approval now creates an in-app notification for the original requester stating that the request was approved, how many recipients were queued, and that delivery remains pending.
- Rejection creates an in-app notification for the requester with the recorded rejection reason.
- Notification records are saved in the same transaction/save as the approval or rejection state change. Failed/self-review attempts do not create notifications.
- Updated the notifications page to render the communication notification type as informational.
- External email notifications are not sent by this feature; the SMTP worker remains disabled by default.


## CI dependency correction — configuration extension namespace

- The attempted package name Microsoft.Extensions.Configuration.Memory is not a published NuGet package. Removed that invalid dependency.
- Imported Microsoft.Extensions.Configuration in the quota test, which already references Microsoft.Extensions.Configuration 10.0.15, to resolve AddInMemoryCollection without adding another package.
- The API build passed on the failed run; the test project failed during restore before tests started. Full workflow is rerunning.


## Completed — notification object-ownership authorization tests

- Added direct controller regression tests proving notification listing returns only rows for the authenticated user, even when another user belongs to the same company.
- Added negative tests proving a user cannot mark-read or delete another user's notification; foreign-user IDs return Not Found and leave the other record unchanged.
- Added metadata checks pinning CEO/Admin authorization to queue metrics, dead-letter listing and replay routes. NotificationsController remains authenticated and has no anonymous action overrides.
- Expanded the reviewed route-to-resource authorization matrix. The wider raw-SQL, export, cache, storage, webhook and agent-tool audit remains open.


## SQL Server notification fixture correction

- The SQL Server bulk mark-read test now relies on database-generated identity IDs for the company and both users. It binds the simulated JWT and tenant context to those generated values, avoiding identity-insert assumptions in the relational integration test.


## P2 foundation — tenant lifecycle schema and service

- Added tenant status fields (Active/Suspended); existing companies migrate to Active by default with a nullable suspension timestamp.
- Added a lifecycle service for Active → Suspended and Suspended → Active transitions. Every transition requires a reason, rejects duplicate/invalid states, and writes an immutable lifecycle event with the company update in one transaction.
- Added a tenant query filter and SQL Server translation coverage for lifecycle events.
- Added migration 20261118_006_tenant_lifecycle.sql and lifecycle service tests. Authentication enforcement and the platform admin console are the next P2 slices.


## P2 foundation — tenant lifecycle enforcement

- Server-side PlatformAdmin:UserIdsCsv configuration is now the only source of the platform_admin JWT claim; it cannot be granted by a tenant user or request payload.
- Login and refresh reject suspended tenants for ordinary users; a denied refresh revokes the session family. Configured platform administrators retain a recovery path.
- Tenant membership middleware returns HTTP 403 for authenticated regular members of suspended tenants but still validates the member and session before allowing the platform-admin exception.
- Added a PlatformAdmin authorization policy and regression tests for suspended-tenant login/refresh and middleware behavior. The platform console endpoints/UI follow in the next P2 change.


## P2 — platform tenant console API foundation

- Added PlatformTenantsController, protected at controller level by [Authorize(Policy = "PlatformAdmin")].
- GET endpoints expose a paged tenant inventory and tenant detail with active/total user counts and the latest 100 lifecycle events. Results intentionally omit SettingsJson and other tenant configuration secrets.
- Suspend/reactivate routes require a reason and use TenantLifecycleService. The controller never accepts a target CompanyId from the body and records actor, reason and state transition in TenantLifecycleEvents.
- Added tests for cross-tenant inventory access through the platform service, tenant suspension audit, invalid reason and missing tenant responses, plus metadata enforcement of the PlatformAdmin policy.
- This is an API foundation; platform admin dashboard UI, subscription plans/entitlements and usage metering remain outstanding.


## P2 — server-side feature entitlement enforcement

- Added a fail-closed entitlement service: active/trial subscription, valid period, active plan and explicit enabled plan feature are all required.
- Added an authorization filter and applied it to AI chat creation/message sending and task-guidance generation. Existing read access to prior chat/task records remains available.
- Added tests for allowed, missing-subscription, disabled-feature, past-due, cancelled and expired subscription cases.

## P2 — plan/feature entitlement schema

- Added a global subscription-plan catalogue, plan feature switches, a single current subscription per tenant and append-only subscription assignment history.
- The migration creates an idempotent Legacy compatibility plan and assigns it to pre-existing companies, preserving current AI chat and task-guidance behavior.
- New tenants without an active/trial subscription will be denied feature-gated actions by default. Seat, storage and monthly AI-token limit fields are included for upcoming usage enforcement; no metering or billing processor is claimed yet.



## P2 — platform plan and subscription administration tests

- Added regression coverage for plan code normalization/uniqueness, feature switches and limits, tenant assignment/update history, and invalid actors, tenants, plans, statuses, periods and reasons.
- Added controller metadata tests requiring the PlatformAdmin policy on both plan and subscription endpoints.


## Entitlement authorization test-harness correction

- The API compilation was successful; the first entitlement test run failed compilation because the test helper returns a ServiceProvider directly rather than an IServiceScope. Updated test calls to pass the provider directly to the authorization-filter context.


## Entitlement coverage correction

- Added the same ai.task_guidance plan entitlement requirement to the second task-guidance entry point, POST /api/tasks/ai-recommendation. This prevents the alternate recommendation route from bypassing a tenant's AI feature entitlement.


## P2 — tenant entitlement self-service view

- Added authenticated GET /api/tenant/entitlements, deriving tenant identity only from the validated JWT company claim.
- The summary returns the assigned plan, active/trial/expired/cancelled status, effective dates, current plan capacities and each feature switch. Expired/inactive subscriptions and disabled features are represented as unavailable; capacity values are withheld when the subscription is not active.
- The tenant query filter remains the final boundary if a caller attempts to request another company ID through direct service use; the HTTP endpoint does not accept one.
- Added an API authorization metadata test and a tenant-scoped controller regression test.


## CI correction — entitlement service using directives

- Moved the EF Core and infrastructure model using directives to the top of TenantEntitlementService.cs, before the newly added entitlement summary record. No runtime behavior changed.


## P2 — usage metering v1 foundation

- Added tenant-filtered append-only TenantUsageEvents with a unique (CompanyId, IdempotencyKey) key and usage-period indexes.
- Added real seat metrics: active users plus unexpired pending invitations are counted as reserved seats.
- Added attachment-storage measurements from TaskAttachment.FileSize and AI action counts from tenant-filtered AIActionsLogs.
- Added provider-reported AI token usage ingestion with tenant-context validation, source validation and idempotent event writes. Replaying the same usage key and values is harmless; reusing the key with different values returns an idempotency conflict.
- Token totals are returned as unavailable when no provider token-usage events exist. The current agent response contracts do not report token counts, so no token values are fabricated. Monthly token capacity is reported but not hard-enforced until provider instrumentation and reservation semantics are completed.
- Added migration 20261118_008_usage_metering.sql and tests for seat/invitation counts, attachment bytes, idempotent token recording, monthly usage and missing tenant context.


## P2 — tenant usage summary API

- Exposed authenticated GET /api/tenant/usage for the current UTC month (or a validated requested year/month).
- Tenant identity is taken from the server's tenant context, not from a query/body CompanyId. Invalid periods are rejected; missing tenant context is denied.
- Added controller tests for tenant-context binding and invalid reporting periods, plus authorization metadata coverage.


## P2 — seat capacity enforcement and concurrency boundary

- Added TenantSeatCapacityService for active-user + unexpired pending-invitation counts against the current Active/Trial plan's MaxUsers.
- Both direct Admin user creation and invitations execute the capacity check and insert inside a SERIALIZABLE transaction. On SQL Server the service acquires UPDLOCK/HOLDLOCK on the tenant's unique subscription row; this is the shared lock point for all new seat reservations.
- NULL MaxUsers is unlimited; no active/trial subscription blocks new users/invitations; capacity exhaustion returns HTTP 409 for direct user creation and a non-success invitation result.
- Added tests that prove pending invitations consume seats and direct user creation fails when the plan capacity is already full. SQL Server CI is validating the new locking query and transaction path.


## P2 — tenant storage quota enforcement

- Added TenantStorageCapacityService, which checks the active/trial plan's MaxStorageBytes against existing TaskAttachment.FileSize totals plus the incoming file.
- The storage check uses the same per-tenant subscription-row UPDLOCK/HOLDLOCK serialization point as seat reservations and requires the caller's SERIALIZABLE transaction.
- Attachment uploads now stage in private storage, calculate the actual staged byte size, acquire the subscription lock, and only move the file to its final private key while the attachment metadata insert is inside the same transaction. On failure the staged/final file is cleaned up.
- Requests that exceed the tenant storage allowance return HTTP 409; missing/inactive subscription returns HTTP 403. NULL MaxStorageBytes means unlimited storage.
- Added service tests for within-limit uploads, storage-limit exhaustion, missing tenant context and missing active subscription.


## CI correction — storage quota service namespace

- Added the missing TaskTune.API.Support import to AttachmentsController so the new TenantStorageCapacityService dependency resolves at compile time. No behavior changed.


## CI correction — qualify filesystem operations in MVC controller

- Fully qualified System.IO.File.Move/Exists/Delete in AttachmentsController. The inherited ControllerBase.File method shadows the System.IO.File type name in controller scope.


## CI correction — normalize qualified storage file references

- Corrected the duplicate System.IO namespace prefix introduced when qualifying the MVC controller's filesystem calls.


## P2 — platform usage console integration

- Connected the Platform Console tenant detail view to GET /api/platform/tenants/{companyId}/usage.
- The console now displays seats reserved (active users + pending invitations), attachment storage and plan limit, AI action count, and provider-reported token usage/limit for the current UTC calendar month.
- Cross-tenant reads remain available only under the PlatformAdmin policy. Standard tenant usage remains bound to the authenticated ITenantContext.
- The UI explicitly distinguishes missing provider token reports from zero tokens.


## CI correction — platform tenant controller test fixture

- Updated the existing PlatformTenantsController unit-test fixture to provide the new TenantUsageMeterService dependency introduced by the platform-only tenant usage action.
