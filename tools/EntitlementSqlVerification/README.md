# Purchased entitlement SQL verification

Targets only `(localdb)\NeoStpAuthAudit_20260904` and a new `EntitlementAudit_<GUID>` database. Parent controls instance start/stop; executable applies the existing real migration chain and validates exact server/catalog/GUID before deleting only its own database. Set `NEOSTP_ENTITLEMENT_SQL_EVIDENCE` to save the successful assertion list after cleanup.

Paid fixtures are created through the actual H05 application processor using synthetic captured-production evidence and an ephemeral enabled gate. No application configuration, real credentials, provider, network, host or customer database is used.

Checks cover paid quotas/module identities, prepaid renewal without false expiry, immutable acquired terms after catalog edits, renewal rejection for changed terms, explicit legacy fallback, global module disabling, module code substitution, malformed snapshots, quota guard/dashboard/resolver behavior, and adopted-license replacement without legacy fallback. No new migration is required by this harness.

This verifies local SQL behavior only, not real payment verification, provider certification, deployment or restore readiness.

Administrative coverage also exercises BillingCompanyTransaction on real SQL: paid-license reassignment and extra-module activation reject without changes; a held company application lock blocks the module action before its queries; acquired-module deactivation/reactivation commit after release, before the renewal snapshot is captured.
