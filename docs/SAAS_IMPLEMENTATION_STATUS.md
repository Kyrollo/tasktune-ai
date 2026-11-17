# TaskTune HR SaaS — implementation status

## Added on `implementation/secure-saas-foundation`

- Employee-specific permission overrides, effective-dated reporting lines, and tenant-scoped employee visibility.
- Leave types/balances/requests/holidays and company time-zone settings.
- Work shifts, shift assignments, scheduled attendance, clock-in/out, timesheets, team attendance and controlled adjustments.
- Tenant plan/usage records, active-seat limits, module gates, AI request metering and storage usage metering.
- HR workforce operations: applicant pipeline, onboarding checklist tasks, performance goals, employee expense claims and review, company asset register/assignment/return, and benefit plans/enrollments.
- Payroll periods and payslip records with gross/net arithmetic, draft/approve/paid lifecycle, and employee payslip history.
- Per-agent AI configuration with Groq, OpenAI, Anthropic and OpenAI-compatible model adapters, provider base URL, agent service URL/endpoint, temperature/token configuration, and encrypted provider API keys.
- Tests for attendance scheduling, HR workflow rules, payroll arithmetic, AI adapter configuration, and SQL Server workforce schema migration.

## Payroll boundary

The payroll module computes:

`Gross = BaseSalary + Allowances + Bonus`

`Net = Gross - IncomeTaxDeduction - SocialInsuranceDeduction - OtherDeductions`

All component amounts must be non-negative and deductions cannot exceed gross pay. TaskTune does **not** calculate Egyptian or other national tax/social-insurance statutory amounts. Those values must be determined by validated local rules or a qualified payroll provider and entered by authorised staff.

## Database migration order

Apply the migration sequence in `HR_SAAS_DEPLOYMENT_NOTES.md`, including the new final scripts:

1. `20261117_AddHRWorkforceLifecycle.sql`
2. `20261117_AddPayrollFoundation.sql`

Back up and test on a restored database before production.

## Verification boundaries

The CI workflow is configured to:
- Build the .NET 10 API and run the .NET test project.
- Start a disposable SQL Server service and exercise the HR workforce migration integration test.
- Install Python agent dependencies, compile agent modules, and run mocked AI-adapter tests.
- Run frontend ESLint and the Vite production build.

A green CI run does not replace end-to-end tests against a representative customer database, real AI provider accounts, browser automation, penetration testing, disaster-recovery testing, or a live deployment.

## Still required before commercial production

- Payment processor checkout/webhooks, invoices/receipts, subscription self-service, failed-payment handling and dunning.
- Complete recruitment/ATS, onboarding templates/document workflows, formal performance review cycles, benefit-provider integrations, accounting/expense exports, payroll payment/bank exports and jurisdiction-specific statutory compliance.
- Real provider smoke tests, agent retry/timeout/circuit-breaker monitoring and more extensive multi-provider failover/credential-pool behavior.
- Full SQL Server E2E, tenant isolation and role matrix tests, frontend browser E2E, load testing, security review, production secrets, backups, observability and deploy/rollback drills.
- Review the repository's current open-source licence and all third-party licences before any proprietary/commercial distribution.

## Important release state

This is an implementation foundation on a feature branch. It is not advertised as a fully production-certified or ready-to-sell HR SaaS service. Database migrations are scripts and must be applied/verified by the deployment operator. Payment, statutory payroll and third-party integrations require their own credentials, agreements and validation.
