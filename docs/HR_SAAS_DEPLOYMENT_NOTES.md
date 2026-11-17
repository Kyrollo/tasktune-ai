# HR, SaaS and AI usage deployment notes

This branch adds leave management, tenant subscription entitlements, usage metering, private attachment storage, and individual employee permission/reporting records. The changes are additive; they do not replace the existing database or collect payments.

## 1. Before deployment

1. Take a full SQL Server backup and test the migrations against a restored copy.
2. Deploy the .NET 10 API build and Python/React services from the same release commit.
3. Configure the required secrets in the API host's secret store. Do not commit real values:
   - `ConnectionStrings__DefaultConnection`
   - `JWT_SECRET` (at least 32 bytes)
   - `AI_PROVIDER_ENCRYPTION_KEY` (a separate key, at least 32 bytes; preserve it across restarts and backups)
   - `PLATFORM_ADMIN_API_KEY` (a separate key, at least 32 bytes; platform subscription administration)
   - `AGENT_SERVICE_TOKEN` (same value for the API and the internal Python agent services)
   - `JwtSettings__Issuer`, `JwtSettings__Audience`
   - `Cors__AllowedOrigins__0` and additional exact frontend origins as needed
4. Configure private persistent storage with enough capacity for `App_Data/private-uploads`. This directory must not be exposed as static web content and must be backed up according to the customer's retention policy.
5. Ensure agent services are reachable only through the private application network. Do not expose the agent ports directly to the public internet.

## 2. SQL migration order

Run these scripts in order, once per database. They are intended to be additive and re-runnable, but every production run still requires a backup and review.

1. `20261116_AddEmployeeAccessAndReporting.sql`
   - Employee-specific permission overrides and effective-dated reporting lines.
2. `20261117_AddLeaveManagement.sql`
   - Leave types, annual balances, requests, company holidays, and annual balance allocation ledger.
   - Seeds generic Annual, Sick and Unpaid leave types with zero allowance. HR must configure entitlement according to the employment contract and jurisdiction.
3. `20261117_AddSaaSSubscriptionsAndUsage.sql`
   - Subscription plans, company subscriptions, usage event ledger and Starter assignment for existing companies.
4. `20261117_AllowStorageUsageDeltas.sql`
   - Allows negative `storage_bytes` events when a metered file is deleted. Other usage meters must remain positive.
5. `20261117_AlignReportModuleEntitlements.sql`
   - Normalizes older plan records to the canonical `reports` module code.
6. `20261117_AddAttendanceAndShiftManagement.sql`
   - Work shifts, effective-dated employee shift assignments and UTC attendance punches.
7. `20261117_AllowSystemAttendanceCaptureSource.sql`
   - Allows server-generated schedule rows to be marked with capture source `system`.

Do not run the old destructive database setup script against a production database.

## 3. First-time company setup

1. Log in as a company Admin or CEO and open **Leave Management**.
2. Configure the company's weekend days, company time zone (for example `Africa/Cairo`), add company holidays, create leave types and allocate annual balances to employees.
3. In **Attendance & Shifts**, create work shifts and assign them to employees with the correct weekdays and effective dates. HR can generate scheduled records for selected periods and reconcile missed shifts after the shift end plus grace period.
4. Open **Subscription & Usage** as a company Admin/CEO to view the assigned plan and current measured usage.
5. The platform operator assigns or changes a tenant plan through the secret-protected API:
   - `GET /api/platform/plans`
   - `PUT /api/platform/tenants/{companyId}/subscription`
   - Send the `X-Platform-Admin-Key` header using the `PLATFORM_ADMIN_API_KEY` secret.
6. Keep each AI provider encryption key stable. Changing `AI_PROVIDER_ENCRYPTION_KEY` makes existing encrypted provider keys unreadable unless the original key is restored or a controlled key-rotation migration is performed.

## 4. Current enforcement and metering behavior

- Server-side module entitlements are enforced for Core HR, Leave, Tasks, Projects, Reports and AI endpoints; frontend navigation is not the security boundary.
- New employee creation checks the company's Core HR entitlement and active-seat limit.
- Chatbot, task guidance/recommendation, progress-analysis and AI report-generation requests reserve one monthly AI request against the tenant before the agent is called. A request attempt can count even when the downstream provider/agent later fails; this avoids quota bypass through concurrent retries.
- Task attachments are stored outside the public web root. Uploads check the storage limit and record a positive usage event; deletion records the matching negative storage event after metadata deletion.
- Usage event idempotency keys prevent a retry with the same key from being counted twice. Clients should send a unique `Idempotency-Key` header when retrying an AI request.
- The monthly AI meter uses the UTC calendar month. Storage is accumulated as signed events. Active employee count is read from current active users.

## 5. Not yet connected

The subscription foundation does not yet initiate or receive real payments. Payment-provider checkout, verified webhook processing, invoice/receipt generation, plan self-service upgrades and dunning are still separate work. The platform operator endpoint is an internal control, not a payment gateway.

Country-specific payroll calculations, attendance hardware integrations, biometric capture, absence/no-show generation and biometric imports, recruitment lifecycle, benefits and asset/expense management are not included in this release slice.
