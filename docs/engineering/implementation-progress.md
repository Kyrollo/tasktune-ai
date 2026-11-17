# TaskTune implementation progress

**Repository:** Kyrollo/tasktune-ai  
**Branch:** main-reindex  
**Baseline commit:** 0fa3025c4c0ffef48c126ce4f431eae956376b73  
**Workstream:** SaaS foundation — runtime, authentication, and verification

This record tracks implementation against the product and engineering plan. It distinguishes completed changes from work that remains a release blocker.

## Completed in this implementation batch

- Moved the modern ASP.NET Core API, Application, and Infrastructure projects to .NET 10 and aligned their Entity Framework Core and IdentityModel dependencies.
- Added repository-level SDK selection and GitHub Actions checks for API build, authentication regression tests, frontend lint/build, and Python syntax.
- Removed plaintext-password comparison. Login now accepts BCrypt hashes only; no automatic plaintext migration is performed.
- Replaced raw refresh-token persistence with SHA-256 digests. Tokens are generated with a cryptographically secure random source and rotated after successful refresh.
- Returned the configured access-token expiry instead of a hard-coded one-hour value.
- Removed JWT claim-value logging and stopped returning exception details to API clients.
- Removed database credentials and the JWT signing key from tracked appsettings files. Startup fails closed if the connection string or signing key is absent or invalid.
- Disabled public self-service registration until tenant-scoped invitations and email verification can be enforced.
- Added regression tests for plaintext-password rejection, refresh-token digest storage, refresh rotation, and access-token expiry.
- Corrected the setup guide and root README to describe the actual project paths and checks.

## Verification status

- Local .NET execution was not available in the implementation environment because the .NET SDK was not installed there.
- GitHub Actions is the authoritative build and test gate for this batch. Its result must be checked on the exact commit before the batch is treated as verified.
- The regression tests use EF Core InMemory; they do not replace SQL Server integration tests or a production security review.

## Remaining P1 / release blockers

1. Implement tenant-scoped invitations, email verification, and secure tenant provisioning before re-enabling registration.
2. Add tenant context and enforce tenant and record ownership in every controller, service, export, background job, and AI tool.
3. Add authorization negative tests, including cross-tenant read/write/export attempts.
4. Implement password reset with expiring, single-use, hashed reset tokens and rate limiting.
5. Add refresh-token session-family tracking and reuse detection before supporting multiple devices or sessions.
6. Add SQL Server integration tests and a reviewed migration process; test rollback and backup/restore.
7. Replace development-only CORS origins with a deployment-specific allowlist and harden agent-to-API network authentication.
8. Complete secret rotation, dependency/license review, production telemetry, and an independent security review.

No production-readiness or tenant-isolation claim is made by this workstream.
