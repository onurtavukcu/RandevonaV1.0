# Workspace UI

The application shell follows the Chatopya dark sidebar / light header layout. Only the vendor Tabler 1.4 stylesheet and its source map were copied. The Randevona sidebar, header, pages and icons are independent; no old-company settings, logos, subscription gating, marketplaces, advertising or AI features were copied.

## Files

- Views/Shared/_Layout.cshtml: shell, header, tenant/organization labels and sign out.
- Views/Shared/_Sidebar.cshtml: menu groups, routes and active selection.
- Views/Shared/_NavIcon.cshtml: local SVG icons.
- wwwroot/css/workspace.css and wwwroot/js/workspace.js: desktop/mobile layout and navigation.
- Each menu action has its own controller action and view. _EmptyPage renders the blank content panel until that page is implemented.

## Pages

Home; Livechat; WhatsApp / Template (Template List, New Template); Send Template (Sent Templates, Job Status); Contact (Contact List, Import Contacts, Export Contacts); Chat (Chat History); Flow (Create Flow, All Flows, Flow Operation, Data Sources); Welcome Messages; Connections; Usage; Billing; Management (Platform Settings, Package Settings, Reports).

No send/import/export/billing operations are implemented yet. Home and Management contain navigation shortcuts, not sample business metrics. Existing login/register layouts remain separate.

## Access and context

Management remains protected with PlatformAdminAttribute. Its links are visible only to SuperAdmin. The new shared empty GET pages use WorkspacePageAttribute: platform administrators may view these without tenant context, but are validated against central Users on every request. This marker must not be used on future tenant data operations. Other protected endpoints still require tenant context. Customer requests continue through tenant/organization validation even for shared pages.

TenantWorkContext now includes optional display names from the already validated tenant and selected organization. The header displays these names, not arbitrary claims or an additional database query. SuperAdmin sees Platform administration / No business selected. Administrator tenant/branch selection is implemented through Management. Ordinary customer branch switching is still separate work.

Static assets are anonymous short-circuit endpoints, so CSS/JS requests do not run tenant resolution. This fixes the unstyled admin pages without removing authentication from MVC pages.

## Verification — 2026-10-02

Build: no warnings or errors. 208 offline checks passed, including shared-page authorization and role revocation. 82 HTTP checks passed using in-memory Mongo: all menu pages, customer management denial, real tenant/organization header labels, cookie login/logout and CSS/JS loading for anonymous/customer/admin sessions. Desktop and 390px mobile navigation were inspected in the browser. No real Atlas data was modified.

## Tenant/user management follow-up

The formerly empty tenant/user management area now includes lists, details, pending review, approve/reject and reviewer audit. See Management.md for routes, service/repository responsibilities and verification. Settings/package/report pages are still shells.

## Connections and selected workspaces — 2026-10-06

The header shows the selected business/branch for administrator workspace pages, with an explicit Stop managing action. Central Management screens still show Platform administration. Shared GET pages can now render scoped connection data after server validation; they do not authorize writes. TenantOperationAttribute is reserved for explicit implemented operations with their own service checks and anti-forgery protection.

Connections now has a number list, manual setup/edit form and Meta account-access check. Only safe read models reach the view; tokens are write-only fields. See WhatsApp-Connections.md for the precise limits of the access check and upcoming onboarding work. Desktop Connections was visually inspected. Existing mobile navigation verification predates this form; the full new form has not yet had a mobile browser pass.
