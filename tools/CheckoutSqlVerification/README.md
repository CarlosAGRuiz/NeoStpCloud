# GL1H checkout SQL verification

Runs only against `(localdb)\NeoStpAuthAudit_20260904` and a new `CheckoutAudit_<GUID>` database. The parent controls the dedicated instance lifecycle. The executable applies the real migration chain and requires a GL1H migration. Identities, provider responses and email are synthetic; no application configuration or network provider is used.

After a fresh build and migration review, set `NEOSTP_CHECKOUT_SQL_EVIDENCE` to the JSON artifact path and run this project. Exact server/database identity is verified before deleting its own database in `finally`. Evidence is written only after successful checks and cleanup, and includes applied migration IDs and assertion descriptions.

Coverage: deterministic overlapping reservations, intent visibility from a separate SQL context before the provider effect, same-key and different-key exclusion, durable ACK/replay, timeout without redispatch, injected ACK-write failure, account-scoped external-session uniqueness, and checkout/cancellation exclusion. The synthetic provider exposes the explicit checkout capability and rejects legacy methods. Checkout ACK must never create a payment or license.

This does not validate a real Wompi adapter, credentials, HTTP, capture/webhooks, deployment, restored client data, or production SQL Server behavior.
