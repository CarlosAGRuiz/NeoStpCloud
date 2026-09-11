# Notification Outbox SQL Verification

Runs the real EF migration chain against a uniquely named LocalDB database and verifies:

- tenant-scoped idempotent enqueue under concurrency;
- the physical unique index;
- single provider invocation with two competing workers;
- rollback of the business write when enqueue fails.

The database and all rows are synthetic and are deleted in `finally`. Override the default
LocalDB instance with `NEOSTP_SQLSERVER_TEST_CONNECTION` when needed.

```powershell
dotnet run --project tools/NotificationOutboxSqlVerification/NotificationOutboxSqlVerification.csproj -c Release
```
