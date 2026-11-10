# Supplier management

Supplier management supports listing, searching, status filtering,
pagination, details, creation, contact updates, activation, and
deactivation.

Supplier IDs and normalized codes remain unchanged during updates.
Codes are unique across active and inactive suppliers.
Deactivation preserves the supplier record and contact details.
Permanent deletion is not supported.

## Security

Supplier reads require the Suppliers.Read policy.
Creation, contact updates, activation, and deactivation require
the Suppliers.Manage policy.

Administrator, Purchasing Officer, and Purchasing Manager roles
can manage suppliers. All six configured business roles can read them.

State-changing requests require a valid antiforgery token.
React hides mutation controls according to server-provided capabilities.
API authorization enforces access independently of the interface.

Accounts are explicitly provisioned for local development.
Public self-registration and deployment are outside the current scope.

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
