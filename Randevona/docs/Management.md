# Management: tenants, users and application review

## Scope

- /management/tenants: business-name search, paginated tenants, owner application state and provisioning state.
- /management/tenants/{id}: business details, initial branch, database name, owner application and first page of related users; links to the complete user list.
- /management/users: paginated accounts, email search, status and tenant filters.
- /management/users/{id}: safe identity details, access scope and review audit.
- POST /management/users/{id}/review: approve or reject a pending customer application.

ManagementController handles HTTP/forms only. IManagementService / ManagementService in IdentityService validates the operator and applies business rules. IManagementRepository / ManagementRepository in Data handles central reads and the conditional decision update. Views receive safe DTOs, never Users.PasswordHash or reset tokens.

## Decision rules

Only active SuperAdmins may read or decide. Both endpoint middleware and the service validate the current central account. Forms require anti-forgery tokens; GET never performs a decision. Route user ID identifies the target, while the operator ID always comes from the authenticated principal.

Only enabled, nondeleted, normal User accounts in PendingApproval can be reviewed. Approving prepares/retries the existing tenant using its existing IDs, checks business readiness and an accessible active branch, then changes the account to Active. Secondary users cannot bypass an unapproved business owner. A preparation failure leaves the account pending and can be retried by approving again.

Rejecting requires a trimmed reason of 1–1,000 characters and sets Rejected. It does not provision or delete business data. Tenant provisioning status and account approval are separate: a ready database does not mean an approved application. The tenant list displays its owner's application status.

The single-document update requires PendingApproval, expected TenantId, normal role and enabled/nondeleted account. It writes UserStatus, ReviewedByUserId, ReviewedAtUtc, RejectionReason and UpdatedAt together. Concurrent/stale decisions cannot overwrite a completed review. Existing passwords, role, tenant identity and memberships are preserved. There is no reapproval/suspension/delete/role-edit action in this step; approved/rejected applications are read-only here.

The existing login service permits approved accounts and refuses rejected/pending accounts. No approval/rejection email is sent yet. Old accounts without review fields still load; their audit is shown as Not reviewed.

## Verification — 2026-10-02

Build: 0 warnings/errors. 239 offline checks passed, including access denial, bounded pagination/search, decision validation, audit preservation, preparation retry and competing decisions. 82 existing HTTP checks and 27 new management HTTP checks passed using the real MVC/auth/services with in-memory Mongo. New registration -> approval -> successful login and registration -> rejection -> denied login were verified. Tenant list and review forms were inspected in the browser. These tests do not replace a real Mongo server concurrency/integration test. No real Atlas documents were changed.

Platform Settings, Package Settings and Reports remain page shells. Selecting a customer tenant for WhatsApp operations, account suspension/reactivation, emails and broader audit history are separate work.
