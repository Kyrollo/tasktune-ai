# TaskTune production-readiness runbook

This runbook is an operator checklist, not evidence that production deployment has been completed. Execute it in a staging environment first, then repeat the approved steps for production with a named operator and reviewer.

## 1. Hard safety gates

- Keep `Email:Outbox:Enabled=false` until the SMTP provider, sender identity, recipient policy, retry behavior, bounce handling, monitoring and operator procedures have passed staging verification.
- Do not enable outbound email merely because a communication was approved. Approval creates durable outbox rows; it does not prove delivery.
- Do not load customer production data until tenant-isolation checks, secrets, backups, restore drills, dependency review and security review are complete.
- Use synthetic or masked data outside production. Never copy production secrets into local settings, CI variables that are not required, issue comments or shell history.
- Require a second person to review production migrations, secret rotation, SMTP enablement, storage cutover and restore operations.

## 2. Pre-deployment inventory

Record the deployment environment, application commit, database name/server, API origin, frontend origin, storage roots, SMTP provider, secret-vault references, operator, reviewer and maintenance window.

Confirm the following configuration is supplied by the deployment secret manager or environment (not tracked files):

- SQL Server connection string with least-privileged application credentials.
- JWT signing key with sufficient entropy and a documented rotation procedure.
- `Cors:AllowedOrigins` containing only the exact HTTPS frontend origins.
- `AgentServices:ServiceApiKey` and TLS-only remote agent URLs.
- `AISecrets:EncryptionKey`; back up its recovery material separately. Losing it can make encrypted provider credentials unreadable.
- SMTP host, port, TLS mode, sender identity and credentials.
- HTTPS `Web:BaseUrl` used in invitation and password-reset links.

Rotate any credential that was ever committed to Git history. Removing a secret from the current tree does not revoke the historical value. Review repository collaborators, deploy keys, GitHub Actions secrets and access logs after rotation.

## 3. Database backup and migration sequence

1. Confirm the target database and environment with a second operator.
2. Take a full SQL Server backup and verify that the backup file is readable. Record its location, timestamp, size and checksum.
3. Restore the backup into an isolated verification database and run basic application schema checks before touching the target.
4. Apply reviewed scripts in order, using the exact checked-in files:
   - `20261117_002_account_security.sql`
   - `20261117_003_communication_approval_outbox.sql`
   - `20261117_004_communication_outbox_leases.sql`
5. Re-run each script to verify its documented repeatability. Capture SQL output and schema/index verification queries.
6. Run the API and SQL Server integration tests against a disposable database with the same SQL Server major version as the target.
7. Do not assume that an EF model or CI test automatically applied these hand-authored SQL migration scripts to production.

Never reverse a data migration by blindly dropping tables. Prefer a reviewed forward fix. If rollback is required, stop writes, restore the verified backup or execute the feature-specific rollback procedure approved for that migration.

## 4. Legacy attachment migration

The standalone migration utility defaults to dry-run. Use a least-privileged SQL identity and keep the JSONL manifest outside both upload roots.

1. Confirm the legacy root maps to the former public `/Uploads/` directory and the private root matches the active API's `App_Data/tasktune-private/uploads` configuration.
2. Back up the database, legacy upload root, and private upload root.
3. Run the dry-run and retain its complete output. Investigate every failed or skipped row; do not proceed with unexplained failures.
4. Review the manifest destination and access controls. The manifest can reveal filenames and storage layout, so treat it as operationally sensitive.
5. Run `--apply --manifest <protected-path>` during the approved maintenance window.
6. Reconcile row counts, file sizes and SHA-256 values. Exercise authenticated downloads as users with and without task access.
7. Verify the old public URL paths no longer serve attachment content and that no legacy copies are reachable through static-file middleware.
8. Keep legacy source files and private copies until retention, legal hold and backup policies permit a separately approved cleanup.

For a failed cutover, stop writes and use `--rollback --manifest <protected-path>` only after reviewing the manifest and current database paths. The tool restores metadata only when a row still points at its own private storage key. It deliberately does not delete either copy.

## 5. Communication delivery enablement

Before enabling `Email:Outbox:Enabled`:

- Verify SPF, DKIM and DMARC for the sending domain and validate the provider's TLS configuration.
- Test invitation and password-reset messages with dedicated staging mailboxes.
- Verify that a requester cannot approve their own communication and that all recipients/CC addresses are active members of the same tenant.
- Verify idempotent request submission, duplicate approval attempts, concurrent worker claims, expired lease recovery and dead-letter transitions.
- Send only to an approved internal test allowlist first. Confirm one recipient per outbox row, expected CC behavior, message identifiers and redacted failure logs.
- Test provider throttling, transient SMTP failures, permanent failures, timeouts and a worker crash after provider acceptance.
- Document the at-least-once delivery limitation. A stable Message-ID is not an exactly-once guarantee; ambiguous outcomes require provider-side idempotency or operator reconciliation.
- Configure per-tenant quotas, queue-age alerts, bounce/complaint processing and a staffed dead-letter review process before customer-scale use.

Enable the worker with an explicit reviewed configuration change. Monitor the queue continuously during the initial canary. Keep the kill switch immediately available.

## 6. Observability and incident response

Before customer onboarding, provide:

- Liveness and readiness probes that distinguish process health from SQL Server and required dependency health.
- Structured logs with correlation/trace IDs, environment, severity, tenant-safe identifiers and error codes. Never log passwords, access/refresh tokens, reset/invitation tokens, employee documents, full prompts or message bodies.
- Metrics for request rate, p95/p99 latency, 4xx/5xx, SQL connection saturation, worker claims/failures, oldest pending outbox age, dead-letter count, SMTP outcomes, AI provider latency/cost and per-tenant usage.
- Alerts for login abuse, authorization-denial spikes, elevated 5xx, database saturation, stale queues, dead letters, backup failure, certificate expiry and unusual AI/email spend.
- An incident runbook with severity, incident commander, containment, evidence preservation, customer communications, recovery verification and post-incident corrective actions.

Do not publish availability or recovery objectives until SLIs are measured and RPO/RTO targets have been agreed and demonstrated.

## 7. Backup and restore drill

- Encrypt backups in transit and at rest; store copies off-host and outside the production account or failure domain.
- Restrict backup restore permissions and audit every restore.
- Define retention, legal hold, deletion and key-recovery ownership.
- Restore the database and required private attachment storage into an isolated environment.
- Verify row counts, referential integrity, tenant boundaries, attachment hashes, authentication, invitation/reset workflows and critical user journeys.
- Measure actual recovery time and data loss against the approved RTO/RPO. A successful backup job is not proof of recoverability.
- Record evidence, gaps, owner and due date. Repeat on a scheduled cadence and after material infrastructure changes.

## 8. Go/no-go decision

Production launch requires all critical security findings closed or formally risk-accepted, migrations rehearsed, secrets rotated, attachment access verified, communication delivery safely gated, dependency/license review complete, monitoring and alerts staffed, restore drill passed, support/incident ownership assigned, and an independent security assessment completed.

A green CI run is necessary but is not a production-readiness certificate.


## 10. Tenant queue quotas and queue health

The default per-tenant active backlog limit is 1,000 Pending/Processing rows and the default per-approval recipient cap is 250; both can be configured through Email:Outbox:MaxPendingPerTenant and Email:Outbox:MaxRecipientsPerApproval. Quota failures return HTTP 429 and leave the approval Pending. Relational approval checks use SQL Server SERIALIZABLE isolation to protect the backlog check from concurrent approvals.

CEO/Admin users can inspect GET /api/communications/outbox/metrics for tenant-scoped status counts, oldest queue age and the configured warning threshold. A queue-age warning is raised only when the delivery worker is enabled. This endpoint is an operator visibility signal; production alert routing and on-call notifications still need monitoring-platform integration.


## 11. P2 tenant lifecycle schema

Existing companies migrate to Status = Active; supported lifecycle values are Active and Suspended. Apply 20261118_006_tenant_lifecycle.sql after the account-security and communication migrations using the backup/review process above. Lifecycle state changes require a reason and are recorded in TenantLifecycleEvents in the same transaction as the company update. The API enforcement and platform operator routes are delivered separately; do not treat the schema migration alone as complete tenant lifecycle protection.


## 12. Platform administrator identity

Configure the initial platform administrator user IDs only through trusted deployment configuration at PlatformAdmin:UserIdsCsv (environment variable PlatformAdmin__UserIdsCsv). The value is intentionally empty by default. A user ID in this list receives a platform_admin JWT claim at login/refresh; the PlatformAdmin authorization policy requires that claim. Do not expose a tenant-editable setting for this allowlist.


## 13. Platform tenant console API

The platform tenant inventory and lifecycle endpoints require the PlatformAdmin policy, which in turn requires a platform_admin=true claim minted only for IDs in trusted deployment configuration. The tenant inventory returns limited operational fields and does not return SettingsJson. Suspend/reactivate operations must include a 10–1,000 character reason and generate an immutable TenantLifecycleEvents record. Review access logs and lifecycle audit events regularly.


## 14. P2 plan and entitlement schema

Apply `20261118_007_subscription_entitlements.sql` after the tenant lifecycle migration. Existing tenants are assigned the Legacy plan to preserve current AI chat and task-guidance behavior. The migration does not create paid plans or enable payment collection. New tenants without an active/trial subscription are denied feature-gated actions by default. Plan capacity fields are not hard quotas until the usage-metering slice is implemented.


## 15. Server-side feature gate behavior

AI chat creation/message sending requires the `ai.chat` plan feature; task-guidance generation requires `ai.task_guidance`. The API checks the tenant's active/trial subscription, start/end period, plan active state and explicit feature switch for each protected action. Missing subscription or feature rows are denied with HTTP 403. Reading previous chats and guidance remains available when write/generation access is not entitled.


## 17. Plan administration regression coverage

CI tests cover plan creation and duplicate-code rejection, feature switch normalization and limits, initial tenant subscription assignment, plan changes, immutable event history, and invalid actor/tenant/plan/status/period/reason rejection. Platform plan and subscription controllers are metadata-tested to require the PlatformAdmin policy.


## 16. Tenant entitlement self-service

Tenant users may view their current plan and enabled modules through GET /api/tenant/entitlements. The endpoint derives the tenant from the authenticated token and returns no data for another company. Treat NULL capacity values as unlimited only when an active plan is present; an inactive/expired subscription has no usable plan limits and no enabled features.


## 19. Seat-capacity transaction and operating rules

A seat is reserved by each active user and each unexpired, unconsumed invitation. The seat check and reservation write must remain inside one SERIALIZABLE transaction. On SQL Server, TenantSeatCapacityService takes UPDLOCK/HOLDLOCK on the tenant subscription row so two simultaneous user/invitation requests serialize on one tenant-specific lock point. Do not move the seat count to a frontend-only check or release the transaction before the user/invitation insert. The invitation completion transaction consumes the invitation while creating the user, preserving one seat reservation. NULL MaxUsers represents an unlimited plan capacity. A tenant without a current Active/Trial subscription cannot provision additional users.


## 20. Attachment storage quota enforcement

Task attachment uploads are staged in private storage, then checked against the current subscription's MaxStorageBytes while a SERIALIZABLE transaction holds the tenant subscription row lock. The service sums persisted TaskAttachment.FileSize values and includes the actual staged upload size. The final file move and metadata insert occur before the transaction is committed. Failed or rejected uploads remove their staged file; database failures remove the uncommitted final file. A 409 response means the plan storage cap would be exceeded. NULL MaxStorageBytes means unlimited. This meter covers TaskAttachment objects in the configured private upload root; it does not yet account for other object-store categories or historical orphan files.


## 22. Platform usage console

The platform console's tenant detail view now requests the selected tenant's usage alongside its lifecycle and subscription details. The displayed period is the current UTC month. Operators should interpret “Not reported” AI tokens as missing provider telemetry, not as zero. The platform-only endpoint uses explicit company filters for every cross-tenant aggregate.


## 23. Provider-reported AI token telemetry

Agent 1 chatbot and Agent 2 task guidance return an optional token_usage object with input, output and total tokens when the provider response includes those values. The .NET API persists only a positive reported total into TenantUsageEvents using the current tenant context and a fresh opaque idempotency key, in the same EF save as the corresponding AI action/chat record. Missing metadata means "not reported", never zero. Monthly token-cap enforcement remains disabled until reservation/settlement semantics are implemented and concurrency-tested.


## 26. Email recommendation token telemetry

The email-recommendation agent now returns provider-reported token usage when available. The API records only a positive total under the `email_assistant` source, in the same save as the associated AI action log. No token count is derived from prompt size or configured maximum tokens.
