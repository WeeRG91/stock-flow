# Supplier management

Supplier management supports listing, searching, status filtering,
pagination, details, creation, contact updates, activation, and
deactivation.

Supplier IDs and normalized codes remain unchanged during updates.
Codes are unique across active and inactive suppliers.
Deactivation preserves the supplier record and contact details.
Permanent deletion is not supported.

## Local development security

Authentication and authorization are not implemented.
Supplier mutation endpoints are currently unprotected and intended
for local development only.

## Verification

Application and API tests use fake dependencies.
EF Core model tests check mapping configuration.

Manual local checks cover creation, duplicate codes, validation,
search, pagination, details, contact updates, and status changes.

Automated PostgreSQL integration tests remain deferred.
The current tests do not establish database transaction or
concurrency guarantees.

Frontend behavior has been checked manually.
Frontend automated tests remain deferred by project agreement.