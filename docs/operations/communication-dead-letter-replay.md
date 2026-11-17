# Communication dead-letter replay runbook

## Purpose and authority

A delivery enters `DeadLetter` after the bounded retry budget is exhausted or an expired worker lease leaves the result uncertain. Only a CEO or Admin can view the tenant's dead-letter queue and request a manual replay. Every replay requires a recorded reconciliation reason and an audit event.

## Before replay

1. Inspect the communication approval, recipient and subject in the dead-letter list.
2. Check SMTP/provider logs using the recipient, time window and stable Message-ID derived from the existing idempotency key.
3. Confirm the delivery was not accepted by the provider. If acceptance is ambiguous, do not replay until the external provider or recipient state has been reconciled.
4. Enter the evidence/reason for replay and confirm the duplicate-delivery warning.
5. The HTTP API also requires `providerConfirmedNotAccepted=true`; a reason alone cannot requeue the item. This is an operator attestation, not automated verification of the SMTP provider's logs.

## What replay does

- Requires the outbox row to belong to the authenticated tenant, remain in `DeadLetter`, and reference a parent approval that is still `Approved`.
- Resets the attempt counter and lease metadata, clears the previous redacted failure reason, and returns the same row to `Pending`.
- Preserves the outbox ID and idempotency key so the stable Message-ID remains the same.
- Writes the operator, reason, prior attempt count and state transition to the tenant audit log in the same database save as the requeue.
- Does not send inline. Delivery occurs only if the separately configured `Email:Outbox:Enabled` worker is enabled.

## Delivery semantics and cautions

SMTP delivery is at-least-once. A worker or network failure after the provider accepted a message but before TaskTune recorded `Sent` may result in duplicate delivery. The stable Message-ID is only a deduplication hint; SMTP itself does not guarantee exactly-once semantics. A manual replay must therefore follow provider-log reconciliation and may still require direct provider investigation.

Production enablement still requires the ordered SQL migrations, real SMTP staging tests, verified secret configuration, queue-age alerts, per-tenant quotas and an operator/support procedure for ambiguous provider acceptance.
