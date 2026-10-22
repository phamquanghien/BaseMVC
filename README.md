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
