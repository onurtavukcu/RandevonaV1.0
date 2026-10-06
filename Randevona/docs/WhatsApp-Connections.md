# WhatsApp connections — 2026-10-06

## Use

1. Sign in as an approved customer and open Connections. A platform administrator first opens Management > Tenants > tenant detail > Manage this business and explicitly selects an active branch.
2. Enter Phone number (international format), Phone number ID, WABA ID, Meta app ID and access token. UTC token expiry is optional. Only use credentials for your own/test/customer-authorized account.
3. Save. Status is Configured, not verified. The number is not registered or subscribed with Meta by this operation.
4. To check account access, set the GraphApiVersion enabled for your Meta application in WhatsAppApiSettings in the appropriate appsettings/environment configuration. Its value is deliberately blank initially. The ISettings registration mechanism is unchanged; retain this section even when disabled.
5. Click Check access. This reads the WABA phone-number list and checks the stored ID and displayed number. The successful check time is shown in UTC.
6. Edit an existing number to rotate its token. Leave the token blank to keep it. IDs/account/app cannot be silently moved. Any manual save resets that number's successful access check.

A successful access check only proves the supplied token could read the number in the WABA at that time. It does not verify ownership of the supplied Meta app ID, Cloud API registration, webhook subscription, approval of templates, customer consent, the 24-hour window or messaging readiness. A failed later check leaves the previous successful check timestamp as historical information; it must never authorize sending.

## Layers

- Domain/Entities/Whatsapp/ProviderData: one branch-scoped provider record, familiar Numbers collection; currently WhatsApp only.
- Domain/Models/Meta/WhatsAppProviderData: IDs, token, optional token expiry and access-check metadata.
- Domain/Entities/Whatsapp/ProviderNumberDirectory: central number ownership, with no token or customer conversation data.
- Data/Repositories/Whatsapp: scoped persistence and transactional directory updates.
- BussinessServices/ProviderService: validation, actual actor, stale-form guard, read DTOs and access-check orchestration.
- BussinessServices/ProviderNumberService: resolves a configured number in the current validated tenant/branch. More than one number requires explicit PhoneNumberId. Its internal result contains decrypted credentials and must never be serialized as an HTTP response. Resolution is not authorization to send.
- IntegrationServices/Whatsapp: bounded read-only Meta HTTP client.
- Web/ConnectionsController and views: form/HTTP handling only.

## Data and concurrency

ProviderData is stored in the selected tenant DB and filtered by both TenantId and OrganizationId. A unique (TenantId, OrganizationId) index permits one provider container per branch. The central ProviderNumberDirectory has a unique PhoneNumberId index, so a number cannot be independently claimed by another tenant or branch. The stored tenant DB mapping is resolved and validated before accessing it.

Provider and a new directory entry are written in the same Mongo session/transaction. An insert/update/directory failure rolls back the whole write. Registration already requires transaction-capable Mongo; this operation also requires a replica set/Atlas, not a standalone Mongo server. Collections/indexes are created before the transaction. Existing tenants get the provider index on their first connection write; the central directory index is created at startup.

Version is a compare-and-set concurrency field. A successful write increments it. Concurrent or repeated stale forms return a conflict and cannot overwrite a newer token or number list. If an external check returns after a concurrent edit, its old version prevents it from saving success for obsolete credentials. Unknown commit outcomes return a safe error asking the operator to refresh.

ExpectedTenantId and ExpectedOrganizationId are form preconditions, never the authority for routing. The server's validated context is authoritative. Changing a browser-wide admin selection invalidates old forms from other tabs. ConnectedByUserId/UpdatedByUserId retain the real operator, including a SuperAdmin.

Manual setup currently allows up to 20 numbers per branch. A phone number belongs to one branch in this step. Sharing one number across several appointment branches needs a separate branch-selection/routing model; do not duplicate its credentials into another branch. Number deletion, transfer, disconnect and external webhook routing are not implemented yet.

## Credentials and HTTP

Access tokens use the existing EncryptedField/Mongo mapping before persistence, including nested Numbers entries. Views receive no secret. Submitted tokens and raw Meta responses are not rendered or logged, including on validation failure. The central directory contains no credentials. Encryption-key rotation and overall retention/security review remain in TODO.

The HTTP integration fixes the destination to https://graph.facebook.com, validates version/IDs, uses the Authorization bearer header, disables redirects and HTTP client logging, bounds response size to 256 KiB and each request to 20 seconds. Pagination is bounded to 10 pages; only an encoded cursor is used, never the returned next URL. Raw provider errors are replaced with English application errors. No automatic message sending, registration or external writes occur.

The endpoint and fields follow [Meta's official Get Phone Numbers collection](https://www.postman.com/meta/whatsapp-business-platform/request/e9ady51/get-phone-numbers), consulted 2026-10-06. The API version is selected by the application owner rather than inferred from an old Chatopya version.

## Authorization

Shared GET pages use WorkspacePageAttribute. Explicit tenant operations use TenantOperationAttribute, plus MVC anti-forgery validation and service-side workspace/version checks. A SuperAdmin without a selected business cannot call these operations. Management endpoints remain central even while a business is selected. Roles and branch availability are revalidated; tenant selection is not customer impersonation.

## Verification

Solution build: zero warnings/errors. 294 offline checks and 141 HTTP checks passed (82 existing auth/UI, 27 management, 9 workspace selection, 23 connections). The fake Mongo tests cover scope, encrypted BSON, directory uniqueness, rollback, concurrent/stale forms and operator identity. The fake HTTP tests cover successful and denied Meta access, safe pagination, malformed/oversized responses, configuration, cancellation and secret redaction. MVC tests verify anti-forgery, customer/admin operation paths, safe token editing and blank Graph settings.

No real Atlas documents or Meta accounts were read or modified by these tests. Server transaction/retry behaviour still requires a real Mongo replica-set integration test before pilot. Live Meta access must be tested with the owner's own credentials and configured API version.

## Next work

1. Embedded Signup settings/state/callback and durable authorized WABA/phone linkage.
2. Meta app/token identity checks, number registration and webhook subscription as separate explicit operations.
3. Signed webhook host/handler and safe central-number-to-tenant routing (no unauthenticated directory endpoint).
4. File storage and Hangfire foundation, then templates, contacts/import/export, livechat and Flows.
5. Appointment availability/reservation and reminders, followed by package/usage/reporting.
6. Deferred CRM, first-contact consent, retention/security and email topics remain in TODO.md.

