# Real-time inventory updates

After ReceiveStock saves successfully, Application calls
IInventoryChangeNotifier. The API implementation broadcasts an
InventoryChanged event through SignalR.

The event contains no inventory details. Connected Inventory pages
invalidate their TanStack Query inventory queries and fetch current data.

Notification delivery is best effort:
- Delivery failures are logged and do not fail a committed receipt.
- A process crash between database commit and broadcast can lose an event.
- Successful sending does not guarantee every browser received the event.
- No transactional outbox or durable event replay is implemented.

Clients refresh inventory after connecting and reconnecting to recover
missed changes. A notification lost while a client remains connected may
require another event or an ordinary inventory refetch.

The hub exposes no stock-mutation methods. Notifications currently go to
all connected clients. Future warehouse-specific groups must enforce
authorization; group names alone are not a security boundary.

Hub connections require the Inventory.Read policy and use the
authenticated cookie session. Connections close when their
authentication ticket expires. React stops its connection when the
Inventory page unmounts, including after logout or session loss.

Security-stamp changes do not immediately terminate an already-open
connection. Notifications contain no inventory details; subsequent
HTTP reads remain subject to API authorization.

Backend tests use fake persistence and SignalR transport.
Frontend behavior was checked manually; automated frontend tests and
PostgreSQL integration tests remain deferred. Live database atomicity
and concurrency guarantees have not been verified by these tests.
