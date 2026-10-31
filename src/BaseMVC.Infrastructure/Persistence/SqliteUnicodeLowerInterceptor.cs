using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BaseMVC.Infrastructure.Persistence;

/// <summary>
/// SQLite's built-in lower() only handles ASCII. Overriding it per connection lets provider-agnostic
/// <c>ToLower().Contains(...)</c> queries match Unicode (e.g. Vietnamese) case-insensitively.
/// Not needed for providers such as SQL Server whose collations are already case-insensitive.
/// </summary>
public sealed class SqliteUnicodeLowerInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Register(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Register(connection);
        return base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void Register(DbConnection connection)
    {
        if (connection is SqliteConnection sqlite)
            sqlite.CreateFunction("lower", (string? value) => value?.ToLowerInvariant(), isDeterministic: true);
    }
}
