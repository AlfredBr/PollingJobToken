using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

using System.Data;

namespace PollingJobToken.DataAccess;

#pragma warning disable S3010, S1192, S125, S1185

public abstract class StoredProcExecutionDbContext : DbContext
{
    public StoredProcExecutionDbContext(DbContextOptions<StoredProcExecutionDbContext> options) : base(options)
    {
        // intentionally left blank
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure any model-specific settings here
        // Example: modelBuilder.HasDefaultSchema("api");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Additional configuration if needed
        // Connection string is configured in Program.cs via DI
    }

    // Primary implementation with cancellation support
    public async Task<List<T>> ExecuteStoredProcedureAsync<T>(string procedureName, CancellationToken cancellationToken, params SqlParameter[]? parameters) where T : class, new()
    {
        var results = new List<T>();

        var connection = Database.GetService<IRelationalConnection>().DbConnection;
        using var command = connection.CreateCommand();
        command.CommandText = procedureName;
        command.CommandType = CommandType.StoredProcedure;

        if (parameters is { Length: > 0 })
        {
            command.Parameters.AddRange(parameters);
        }

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var properties = typeof(T).GetProperties();
        var rowCounter = 0;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if ((rowCounter++ & 255) == 0) // every 256 rows
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var item = new T();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var property = properties.FirstOrDefault(p =>
                    string.Equals(p.Name, columnName, StringComparison.OrdinalIgnoreCase));

                if (property != null && property.CanWrite && !await reader.IsDBNullAsync(i, cancellationToken).ConfigureAwait(false))
                {
                    var value = reader.GetValue(i);
                    if (value != null && property.PropertyType.IsInstanceOfType(value))
                    {
                        property.SetValue(item, value);
                    }
                }
            }
            results.Add(item);
        }

        return results;
    }

    // Two-recordset - implementation with cancellation support
    public async Task<(List<T1> First, List<T2> Second)> ExecuteStoredProcedureAsync<T1, T2>(
        string procedureName,
        CancellationToken cancellationToken,
        params SqlParameter[]? parameters)
        where T1 : class, new()
        where T2 : class, new()
    {
        var firstResults = new List<T1>();
        var secondResults = new List<T2>();

        var connection = Database.GetService<IRelationalConnection>().DbConnection;
        using var command = connection.CreateCommand();
        command.CommandText = procedureName;
        command.CommandType = CommandType.StoredProcedure;

        if (parameters is { Length: > 0 })
        {
            command.Parameters.AddRange(parameters);
        }

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var properties1 = typeof(T1).GetProperties();
        var rowCounter = 0;

        // Read first result set
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if ((rowCounter++ & 255) == 0) // every 256 rows
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var item = new T1();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var property = properties1.FirstOrDefault(p =>
                    string.Equals(p.Name, columnName, StringComparison.OrdinalIgnoreCase));

                if (property != null && property.CanWrite && !await reader.IsDBNullAsync(i, cancellationToken).ConfigureAwait(false))
                {
                    var value = reader.GetValue(i);
                    if (value != null && property.PropertyType.IsInstanceOfType(value))
                    {
                        property.SetValue(item, value);
                    }
                }
            }
            firstResults.Add(item);
        }

        // Read second result set (if present)
        if (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
        {
            var properties2 = typeof(T2).GetProperties();
            rowCounter = 0;

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if ((rowCounter++ & 255) == 0) // every 256 rows
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var item = new T2();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var property = properties2.FirstOrDefault(p =>
                        string.Equals(p.Name, columnName, StringComparison.OrdinalIgnoreCase));

                    if (property != null && property.CanWrite && !await reader.IsDBNullAsync(i, cancellationToken).ConfigureAwait(false))
                    {
                        var value = reader.GetValue(i);
                        if (value != null && property.PropertyType.IsInstanceOfType(value))
                        {
                            property.SetValue(item, value);
                        }
                    }
                }
                secondResults.Add(item);
            }
        }

        return (firstResults, secondResults);
    }

    // Two-recordset - Backwards-compatible overload (no explicit token passed)
    public Task<(List<T1> First, List<T2> Second)> ExecuteStoredProcedureAsync<T1, T2>(
        string procedureName,
        params SqlParameter[] parameters)
        where T1 : class, new()
        where T2 : class, new() =>
        ExecuteStoredProcedureAsync<T1, T2>(procedureName, cancellationToken: default, parameters);

    // Two-recordset - Convenience overload allowing null parameter array & optional token
    public Task<(List<T1> First, List<T2> Second)> ExecuteStoredProcedureAsync<T1, T2>(
        string procedureName,
        CancellationToken cancellationToken = default)
        where T1 : class, new()
        where T2 : class, new() =>
        ExecuteStoredProcedureAsync<T1, T2>(procedureName, cancellationToken, Array.Empty<SqlParameter>());
    
    // Helper method for executing stored procedures with single result (token aware)
    public async Task<T?> ExecuteStoredProcedureFirstOrDefaultAsync<T>(string procedureName, CancellationToken cancellationToken, params SqlParameter[]? parameters) where T : class, new()
    {
        var results = await ExecuteStoredProcedureAsync<T>(procedureName, cancellationToken, parameters ?? Array.Empty<SqlParameter>()).ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    // Backwards-compatible overload (no explicit token passed)
    public Task<T?> ExecuteStoredProcedureFirstOrDefaultAsync<T>(string procedureName, params SqlParameter[] parameters) where T : class, new() =>
        ExecuteStoredProcedureFirstOrDefaultAsync<T>(procedureName, cancellationToken: default, parameters);

    // Helper method for executing non-query stored procedures (token aware)
    public async Task<int> ExecuteStoredProcedureNonQueryAsync(string procedureName, CancellationToken cancellationToken, params SqlParameter[]? parameters)
    {
        var connection = Database.GetService<IRelationalConnection>().DbConnection;
        using var command = connection.CreateCommand();
        command.CommandText = procedureName;
        command.CommandType = CommandType.StoredProcedure;

        if (parameters is { Length: > 0 })
        {
            command.Parameters.AddRange(parameters);
        }

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // Backwards-compatible overload (no explicit token passed)
    public Task<int> ExecuteStoredProcedureNonQueryAsync(string procedureName, params SqlParameter[] parameters) =>
        ExecuteStoredProcedureNonQueryAsync(procedureName, cancellationToken: default, parameters);
}
