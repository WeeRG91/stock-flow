# Background jobs and low-stock monitoring

## Local setup

Hangfire Server runs inside the API process with two workers.
Job storage uses the hangfire schema in stockflow_dev.

Background processing is enabled only in Development and can be disabled
with BackgroundJobs:Enabled=false. API tests explicitly disable it.

The dashboard is available at http://localhost:5290/hangfire.
Hangfire's default local-request authorization remains enabled.

## Monitoring

The recurring job ID is low-stock-monitoring.
It runs daily at 07:00 in the Europe/Luxembourg timezone.

The API must be running to schedule and process work.

Only existing inventories with configured thresholds and active products
and warehouses are monitored. A null threshold disables monitoring.
Stock is low when quantity is strictly below its threshold.

LowStockAlerts stores one reusable alert per inventory.
Repeated low-stock detection preserves an active alert.
Recovery, disabled monitoring, or inactive resources resolve it.
A later shortage reopens the same alert.

Reopening replaces the detection timestamp. This is current alert state,
not a complete history of every shortage episode.

## Reliability

Monitoring has three configured retries after its initial attempt:
30 seconds, 120 seconds, and 300 seconds.
Exhausted failures remain visible in Hangfire.

Alert reconciliation uses a transaction-level PostgreSQL advisory lock,
a unique InventoryId index, and an explicit transaction.
All cooperating alert writers must use the same locking convention.

A failed synchronization requires a fresh scope and DbContext.
Retries rerun the complete scan.

Jobs may execute more than once. These mechanisms do not provide
exactly-once execution.

The inventory scan happens before the reconciliation transaction.
Stock can change after that scan, and overlapping scans may be stale.
Alerts are scheduled observations, not real-time stock guarantees.

No transactional outbox is implemented. Hangfire does not make the
existing SignalR notifications durable.

## Verification

Unit, API, and EF model tests cover code behavior.
Local manual checks covered job execution and alert creation,
repeated execution, resolution, and reopening.

PostgreSQL integration tests remain deferred.
Concurrent executions, rollback failures, and ambiguous commit outcomes
have not been verified by automated database tests.

The development monitoring setup intentionally recalculates one demo
inventory's threshold to its current quantity plus five.
It must be invoked explicitly.