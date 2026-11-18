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

## Route-to-resource authorization matrix

| Area / action | Route | Required access | Resource / row boundary |
|---|---|---|---|
| NotificationsController.GetNotifications | `GET /api/notifications` | Authenticated session | Only rows where `Notification.UserId` equals the subject in the validated JWT/session |
| NotificationsController.MarkRead | `PUT /api/notifications/{id}/mark-read` | Authenticated session | Notification must belong to the current user; foreign-user IDs return Not Found |
| NotificationsController.MarkAllRead | `POST /api/notifications/mark-all-read` | Authenticated session | Bulk update predicate includes current user ID; never tenant-wide |
| NotificationsController.DeleteNotification | `DELETE /api/notifications/{id}` | Authenticated session | Notification must belong to the current user; foreign-user IDs return Not Found |
| CommunicationsController.GetOutboxMetrics | `GET /api/communications/outbox/metrics` | CEO/Admin | Current tenant only; content-free status and queue-age aggregates |
| CommunicationsController.GetDeadLetterOutbox | `GET /api/communications/outbox/dead-letter` | CEO/Admin | Current tenant only; dead-letter rows only |
| CommunicationsController.ReplayDeadLetter | `POST /api/communications/outbox/{id}/replay` | CEO/Admin | Current tenant; approved parent, DeadLetter state, explicit provider non-acceptance attestation and reason |

## Tenant usage summary route

| Controller action | Route | Required access | Data and tenant boundary |
|---|---|---|---|
| TenantUsageController.GetCurrentMonth | `GET /api/tenant/usage?year=&month=` | Authenticated session | Uses ITenantContext derived from validated JWT; does not accept CompanyId; reports current or selected UTC month |

## Tenant entitlement summary route

| Controller action | Route | Required access | Data and tenant boundary |
|---|---|---|---|
| TenantEntitlementsController.GetMyEntitlements | `GET /api/tenant/entitlements` | Authenticated session | Tenant ID is read from the validated JWT; returns plan status, capacities and feature flags visible only within the current tenant context |

## Platform plan and subscription routes

| Controller action | Route | Required access | Data and safety boundary |
|---|---|---|---|
| PlatformPlansController.GetPlans | `GET /api/platform/plans` | PlatformAdmin claim | Global plan catalogue and feature switches |
| PlatformPlansController.CreatePlan | `POST /api/platform/plans` | PlatformAdmin claim | Validated plan fields; create audit log |
| PlatformPlansController.SetFeature | `PUT /api/platform/plans/{id}/features/{featureCode}` | PlatformAdmin claim | Explicit feature switch and non-negative limit; audit log |
| PlatformSubscriptionsController.GetTenantSubscription | `GET /api/platform/subscriptions/tenants/{companyId}` | PlatformAdmin claim | One tenant subscription and latest 100 assignment events |
| PlatformSubscriptionsController.AssignTenantSubscription | `PUT /api/platform/subscriptions/tenants/{companyId}` | PlatformAdmin claim | Active plan, supported status and period, mandatory reason, transactional event history |

## Platform tenant lifecycle routes

| Controller action | Route | Required access | Resource / audit boundary |
|---|---|---|---|
| PlatformTenantsController.GetTenants | `GET /api/platform/tenants` | PlatformAdmin claim | Cross-tenant inventory; returns only identifiers, names, status and user counts |
| PlatformTenantsController.GetTenant | `GET /api/platform/tenants/{id}` | PlatformAdmin claim | One tenant summary plus last 100 lifecycle events |
| PlatformTenantsController.SuspendTenant | `POST /api/platform/tenants/{id}/suspend` | PlatformAdmin claim | Active → Suspended; reason required; immutable lifecycle event |
| PlatformTenantsController.ReactivateTenant | `POST /api/platform/tenants/{id}/reactivate` | PlatformAdmin claim | Suspended → Active; reason required; immutable lifecycle event |

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
- Direct controller regression tests now cover notification listing and single-row mutation ownership, including negative attempts against another user in the same tenant. Metadata tests pin CEO/Admin access to outbox metrics, dead-letter listing and replay. This is a testable authorization slice, not the complete application matrix.

The exhaustive endpoint-to-entity-to-action matrix is still outstanding, especially for raw SQL, exports, queued work, caches, webhooks, agent tools and field-level data classifications. Independent adversarial testing remains a release gate.


## Seat reservation authorization boundary

| Action | Required access | Capacity / row boundary |
|---|---|---|
| UsersController.CreateUser | Admin in current tenant | Active users + pending invitations; checks the current subscription seat cap while holding its row lock through user insert |
| UsersController.CreateInvitation | Admin/CEO/HR in current tenant, plus existing role restrictions | Active users + pending invitations; old invite for the same email is consumed and new reservation is inserted under the same locked transaction |
| AccountSecurityWorkflowService.CompleteInvitation | Anonymous invitation token, email-bound and single-use | Converts one pending invitation reservation into one active user atomically; must not reserve a second seat |



## Attachment storage quota

- AttachmentsController.UploadAttachment: authenticated user with task access; current JWT tenant must match the task's tenant; storage capacity is checked against the current tenant subscription while holding a SERIALIZABLE transaction and subscription-row lock.
- The upload is staged privately, checked using actual bytes, and only moved to its final private key with attachment metadata inserted inside the transaction. Plan-cap failure returns Conflict without creating an attachment record.


## Platform usage reporting

| Controller action | Route | Required access | Data and boundary |
|---|---|---|---|
| PlatformTenantsController.GetTenantUsage | `GET /api/platform/tenants/{companyId}/usage?year=&month=` | PlatformAdmin claim | Aggregates active users, invitations, task attachments, AI actions and recorded provider token events using explicit company predicates. |
