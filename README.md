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

## Authentication

Sign in at `/Account/Login` with an Identity account. The application does not
expose public self-registration. Sign-out is a protected POST request, and
login and logout forms include ASP.NET Core Anti-Forgery tokens. Cookie
challenges use `/Account/Login`; denied authorization uses
`/Account/AccessDenied`.

Authenticated users can change their password at `/Account/ChangePassword`.
The operation requires the current password, enforces the configured Identity
password policy, and refreshes the current sign-in after a successful change.

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
