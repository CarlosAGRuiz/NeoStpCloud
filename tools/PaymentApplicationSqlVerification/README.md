# Payment application SQL verification

Targets only `(localdb)\NeoStpAuthAudit_20260904` and a new `PaymentApplicationAudit_<GUID>` database. Parent controls instance start/stop. The executable applies real migrations, checks the exact connection identity before its database cleanup, and writes `NEOSTP_PAYMENT_APPLICATION_SQL_EVIDENCE` only after successful checks and deletion.

The internal application gate is enabled only in the harness options object. Captured-production evidence is fabricated exclusively in its temporary SQL database. No configuration files, real provider, network, host, credentials, client database or external financial effect is involved. This does not open the H04 production webhook gate.

Coverage: same-receipt and two-receipt concurrency with deterministic SQL lock barriers; one ledger/payment/finite period; commercial/module snapshots; rollback of ledger, payment, license, customer, subscription, modules and checkout completion; receipt retry; cancellation serialized under the identical company lock; cancellation replay without revival; SQL uniqueness for intent/receipt/payment; and the disabled default gate.

Cancellation is a controlled SQL fixture under the same application lock, not an API/provider cancellation. The production-mode inputs are test truth, not verified real captures. This verifies local transaction behavior, not production readiness, payment certification or a restore rehearsal.
