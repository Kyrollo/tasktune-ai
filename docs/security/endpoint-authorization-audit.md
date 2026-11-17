# TaskTune endpoint authorization and tenant-filter audit

**Reviewed baseline:** `main-reindex` at `6450884b59de11f64b791d49d02185cdbe78a5e1`  
**Purpose:** record the intended public endpoint boundary and establish automated metadata checks. This document is an engineering inventory, not a penetration-test report.

## Public API endpoints

Only the following actions are intentionally anonymous. Each carries an explicit `[AllowAnonymous]` attribute and is included in a regression-test allowlist:

| Controller action | Route | Controls |
|---|---|---|
| AuthController.Login | `POST /api/auth/login` | IP rate limit; password verification; session creation |
| AuthController.RefreshToken | `POST /api/auth/refresh-token` | IP rate limit; refresh-token digest and rotation/replay checks |
| AuthController.ForgotPassword | `POST /api/auth/forgot-password` | IP rate limit; generic account-existence response |
| AuthController.ResetPassword | `POST /api/auth/reset-password` | IP rate limit; single-use, expiring token |
| AuthController.Register | `POST /api/auth/register` | IP rate limit; invitation token and email binding; no self-service company creation |
| HealthCheckController.Live | `GET /api/health/live` | Liveness only; no dependency call or tenant data |
| HealthCheckController.Ready | `GET /api/health/ready` | Dependency-aware readiness; no connection details or tenant payload |
| HealthCheckController.Check | `GET /api/health` | Backward-compatible readiness response; no tenant payload |

Logout and device-session listing/revocation require authentication. No controller is intended to inherit anonymous access.

## Controller policy layers

- Every routed action must have effective `[Authorize]` metadata on its controller or method, unless it is on the explicit anonymous allowlist above. `ControllerAuthorizationMetadataTests` enforces this against the compiled API assembly.
- Role-specific restrictions are layered on top of authentication. Examples include Admin-only diagnostics, CEO/Admin company administration and permission management, and HR/CEO/Admin user and communications administration.
- Dynamic page permissions are evaluated by `BaseApiController.HasPageAccessAsync`. Explicit tenant-role denies override privileged-role shortcuts.
- Tenant membership is revalidated by `TenantMembershipMiddleware` on authenticated requests before controller execution.
- Service and resource checks remain mandatory; an authorized role or visible UI control does not replace task/object/record-level authorization.

## Tenant-owned persistence boundary

The following model types are required to have EF Core global query filters: Company, User, Department, Position, Project, RolePermission, Report, CommunicationLog, CommunicationApproval, CommunicationOutbox, Task, Subtask, TaskComment, TaskAccessControl, TaskDependency, TaskProgress, TaskAttachment, Evaluation, Bug, Notification, AuditLog, AIActionsLog, ChatThread, ChatMessage, UserSetting, UserSession and AccountSecurityToken.

`TenantQueryFilterMetadataTests` fails if any listed type is absent from the EF model or loses its filter. `SqlServerTenantIsolationTests` additionally forces SQL Server to translate the relationship-owned filters; these checks do not replace end-to-end attempts to access another tenant's records through every route, export, agent tool, background job, cache or storage path.

## Deliberate boundaries and remaining review

- Cross-tenant identity lookup (for installation-wide email uniqueness and token/invitation verification) must return only the minimum result needed and must never expose the other tenant's record.
- Outbox claims and lease-state transitions use narrowly scoped SQL Server queue operations. They must validate status, lease ownership and idempotency key; the worker is disabled by default.
- The attachment-migration utility is an offline operator tool with an explicit dry-run default, application lock, fixed legacy path grammar, digest checks and compare-and-set metadata updates. It is not an HTTP endpoint.
- The exhaustive endpoint-to-entity-to-action matrix is still outstanding, especially for raw SQL, bulk operations, exports, queued work, caches, webhooks, agent tools and field-level data classifications. Independent adversarial testing remains a release gate.
