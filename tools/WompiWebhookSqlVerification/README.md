# Wompi webhook SQL verification

Uses only the dedicated `(localdb)\NeoStpAuthAudit_20260904` instance and a newly generated `WompiWebhookAudit_<GUID>` database. The parent owns start/stop; the executable migrates and deletes only its own database, validating exact server/catalog/GUID before cleanup. No application settings, real credentials, network, hosts or customer data are used.

After the H04 migration is available and a fresh Release build passes, set `NEOSTP_WOMPI_WEBHOOK_SQL_EVIDENCE` to the JSON artifact path and run the executable. Evidence is written only after checks and exact cleanup complete, with applied migration IDs and all assertion labels.

Coverage includes durable inbox before verifier, deterministic concurrent same-event reservations, active-delivery pending response, semantic replay/conflict, unknown checkout quarantine, verifier read retry, final transaction commit failure and recovery, SQL transaction identity uniqueness by account and mode, HMAC rejection, and sandbox verification without payment/license application or cancellation revival.

The verifier is a controlled fake. Service error codes corresponding to HTTP 503/409 are asserted; this executable does not exercise HTTP/controller mappings or real remote GETs. Production-mode rows appear only as isolated schema uniqueness fixtures; production configuration stays disabled. This is not real Wompi certification or a deployment/restore rehearsal.
