# BaseMVC

BaseMVC is an ASP.NET Core MVC foundation targeting .NET 10. Identity data is
stored in SQLite; the default local database is `basemvc.db`.

## Initial Identity setup

At startup, the application applies pending EF Core migrations and ensures the
`Administrator` and `SuperAdmin` roles exist. To create the initial SuperAdmin,
provide both settings through a secret provider before the first application
startup:

- `SeedAdmin:Email`
- `SeedAdmin:Password`

For environment-variable configuration, use `SeedAdmin__Email` and
`SeedAdmin__Password`. In development, use ASP.NET Core User Secrets; in
deployed environments, use the platform's secret store. Do not put the admin
password in `appsettings.json`, source control, or deployment manifests.

If neither setting is configured, the roles are still seeded and no admin
account is created. Providing only one setting fails startup with an explicit
configuration error. If an account with the configured email already exists,
it is assigned the `SuperAdmin` role without changing its password.

The connection string can be overridden with
`ConnectionStrings__DefaultConnection`.

## Health checks

`/health/live` is an anonymous liveness endpoint that reports whether the
application process is running. `/health/ready` is an anonymous readiness
endpoint that also checks connectivity to the configured SQLite database.
Both endpoints return a plain-text health status without exposing dependency
details.

## Authentication

Sign in at `/Account/Login` with an Identity account. The application does not
expose public self-registration. Sign-out is a protected POST request, and
login and logout forms include ASP.NET Core Anti-Forgery tokens. Cookie
challenges use `/Account/Login`; denied authorization uses
`/Account/AccessDenied`.

Authenticated users can change their password at `/Account/ChangePassword`.
The operation requires the current password, enforces the configured Identity
password policy, and refreshes the current sign-in after a successful change.

Users can request a one-time reset link at `/Account/ForgotPassword`. Reset
requests use the configured canonical `Email:PublicBaseUrl`, require a
confirmed email address, and do not reveal whether an account exists. Reset
tokens use the ASP.NET Core Identity token provider and its configured
expiration.

Configure SMTP through a secret provider or environment variables:

- `Email__SmtpHost`
- `Email__SmtpPort` (587 by default; uses required STARTTLS)
- `Email__SmtpUsername` and `Email__SmtpPassword` (both set or both omitted)
- `Email__UseSslOnConnect` (`true` for implicit TLS, commonly port 465)
- `Email__FromAddress` and optional `Email__FromName`
- `Email__PublicBaseUrl` (canonical public application URL; use HTTPS in production)

SMTP credentials must not be committed to source control or placed in
`appsettings.json`.

Login and password-reset POST requests are rate-limited by the remote client IP.
Defaults are 5 login attempts per 300 seconds and 3 reset requests per 900
seconds. Tune the limits through configuration keys
`RateLimits__Account__LoginPermitLimit`,
`RateLimits__Account__LoginWindowSeconds`,
`RateLimits__Account__PasswordResetPermitLimit`, and
`RateLimits__Account__PasswordResetWindowSeconds`. Limits must be positive
integers; permit limits are capped at 1000 and windows at 86400 seconds.
Rejected requests receive HTTP 429 and a `Retry-After` header. When hosting
behind a reverse proxy, configure ASP.NET Core Forwarded Headers with only
trusted proxies/networks so the remote IP reflects the actual client; do not
trust arbitrary forwarded headers.

## Dynamic permissions

Protect an MVC action with a policy such as
`[Authorize(Policy = "Permissions.Users.Read")]`. Permission names have the
form `Permissions.{Module}.{Action}`. The policy provider creates valid
permission policies at runtime; malformed permission policies fail closed.

The authorization handler checks both direct user permission claims and role
permission claims in SQLite for every authorization request. Permission grants
and revocations therefore take effect on the next authorization check instead
of waiting for a user cookie to be refreshed.

The initial catalog is defined in
`BaseMVC.Infrastructure.Authorization.PermissionCatalog`. `SuperAdmin` is
seeded with every catalog permission. `Administrator` receives user and role
management permissions plus read access to the permission catalog. Seeding is
additive and retains additional role permission claims. Use the claim type
`Permission` with values such as `Permissions.Reports.Export` to grant
additional permissions.

Run the infrastructure and user-management integration tests with:

```bash
dotnet test tests/BaseMVC.Infrastructure.Tests/BaseMVC.Infrastructure.Tests.csproj
```

## Role management

Users with `Permissions.Roles.Read` can open `/Admin/Roles`. Search, paging,
create, edit, and delete use JSON endpoints and update the table without a page
reload. Write requests require the ASP.NET Core Anti-Forgery token. The
`Administrator` and `SuperAdmin` system roles cannot be renamed or deleted,
and a role assigned to users cannot be deleted.

## User management

Users with `Permissions.Users.Read` can open `/Admin/Users`. Search and paging
use JSON endpoints; authorized operators can create accounts with an initial
password, update email and role assignments, lock/unlock accounts, and delete
accounts without reloading the page. All write requests require an
ASP.NET Core Anti-Forgery token. Editing does not change an existing password.
Operators cannot lock or delete their own account, and the last SuperAdmin
cannot be locked, removed from the SuperAdmin role, or deleted. Assigning the
SuperAdmin role additionally requires `Permissions.Access.Manage`.

## Role permissions

Users with `Permissions.Access.Read` can open `/Admin/Permissions` to inspect
the permission catalog. Changing grants requires `Permissions.Access.Manage`;
updates use JSON POST requests protected by an Anti-Forgery token.
SuperAdmin retains both `Permissions.Access.Read` and
`Permissions.Access.Manage` so its permission manager cannot lock itself out.
Grant and revoke operations update role claims in SQLite immediately.

## Audit log

Authentication outcomes, password reset/change events, and successful
administrative user, role, and permission mutations are recorded in the
append-only `AuditLogEntries` table. Records contain a UTC timestamp, fixed
event/outcome names, and optional internal actor/target IDs. They do not store
email addresses, IP addresses, passwords, reset tokens, URLs, or request
payloads. Audit rows deliberately have no foreign keys to Identity records so
they remain available after a user or role is deleted. Define and enforce an
operational retention policy appropriate to the deployment's privacy and
compliance requirements.

Users granted `Permissions.Audit.Read` can open `/Admin/Audit` to search and
filter records by event, outcome, and internal IDs with server-side paging.
Results are ordered newest-first by append-only entry ID. Only `SuperAdmin`
receives this permission by default; it can be granted to other roles through
the dynamic permission manager.

Audit records are retained for 90 days. A background service deletes records
older than 90 days at application startup and then every 24 hours. If cleanup
fails, the failure is logged and the next scheduled run retries it.

## SQLite backups and restore

Automated SQLite backups are enabled by default. The application creates an
online, consistent database backup at startup and every 24 hours, verifies
each backup with SQLite `integrity_check`, and removes backups older than
seven days. Configure the service with `SqliteBackup__Enabled`,
`SqliteBackup__Directory`, `SqliteBackup__IntervalHours`, and
`SqliteBackup__RetentionDays`. The directory must be dedicated to BaseMVC
backup files; on Unix-like systems, backup directories and files are restricted
to the application owner. In production, point the directory to a durable,
access-controlled, encrypted volume outside the web root. The default local
directory is `src/BaseMVC.WebUI/App_Data/Backups` and is excluded from Git.

To restore, stop every application instance using the database, preserve the
current database file for investigation, then place a verified
`basemvc-*.db` backup at the configured `ConnectionStrings__DefaultConnection`
data source path and ensure the application owner can read and write it.
Restart the application and verify `/health/ready` and representative
application data before reopening traffic. Periodically rehearse this process
using an isolated copy; backups stored on the same host are not protection
against host or volume loss. When multiple application instances run, use a
single designated backup worker or external coordination and shared storage
to avoid redundant backup sets. With automated backups enabled,
`/health/ready` also checks backup-directory write access and requires a
verified backup newer than two backup intervals (48 hours with the default
schedule). A failing check returns HTTP 503 and logs a diagnostic message;
the latest backup attempt failing also marks readiness unhealthy immediately.
Configure external monitoring to alert on non-200 readiness responses. If
automated backups are deliberately disabled, this backup readiness check is
not registered.
