using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Storage;

/// <summary>SQLite implementation of <see cref="IResourceStore"/>.</summary>
public sealed class SqliteResourceStore : IResourceStore
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS Resources (
            TypeId TEXT NOT NULL,
            Id TEXT NOT NULL,
            Key TEXT NULL,
            Json TEXT NOT NULL,
            PRIMARY KEY (TypeId, Id));
        CREATE UNIQUE INDEX IF NOT EXISTS IX_Resources_Key ON Resources (TypeId, Key) WHERE Key IS NOT NULL;
        CREATE TABLE IF NOT EXISTS Messages (
            Id TEXT NOT NULL PRIMARY KEY,
            ResourceId TEXT NOT NULL,
            SequenceNumber INTEGER NOT NULL,
            CorrelationId TEXT NULL,
            Json TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            PublishedAt TEXT NULL);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_Messages_Sequence ON Messages (ResourceId, SequenceNumber);
        """;

    private readonly string _connectionString;
    private readonly Lazy<Task> _initialization;

    /// <summary>Initializes a new instance of the <see cref="SqliteResourceStore"/> class.</summary>
    /// <param name="settings">The stub settings.</param>
    public SqliteResourceStore(IOptions<CommerceToolsStubSettings> settings)
    {
        var databasePath = Path.GetFullPath(settings.Value.DatabasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();
        _initialization = new Lazy<Task>(InitializeAsync);
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string typeId, string id, CancellationToken cancellationToken = default)
        where T : class =>
        await QuerySingleAsync<T>("SELECT Json FROM Resources WHERE TypeId = $typeId AND Id = $value", typeId, id, cancellationToken);

    /// <inheritdoc />
    public async Task<T?> GetByKeyAsync<T>(string typeId, string key, CancellationToken cancellationToken = default)
        where T : class =>
        await QuerySingleAsync<T>("SELECT Json FROM Resources WHERE TypeId = $typeId AND Key = $value", typeId, key, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> ListAsync<T>(string typeId, CancellationToken cancellationToken = default)
        where T : class
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Json FROM Resources WHERE TypeId = $typeId";
        command.Parameters.AddWithValue("$typeId", typeId);
        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(Deserialize<T>(reader.GetString(0)));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<bool> AnyAsync(string typeId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM Resources WHERE TypeId = $typeId)";
        command.Parameters.AddWithValue("$typeId", typeId);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> CommitAsync(
        IReadOnlyList<ResourceWrite> writes,
        IReadOnlyList<PendingMessage> messages,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (var write in writes)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT OR REPLACE INTO Resources (TypeId, Id, Key, Json) VALUES ($typeId, $id, $key, $json)";
            command.Parameters.AddWithValue("$typeId", write.TypeId);
            command.Parameters.AddWithValue("$id", write.Id);
            command.Parameters.AddWithValue("$key", (object?)write.Key ?? DBNull.Value);
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(write.Resource, write.Resource.GetType(), StubJson.Options));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var committed = new List<OutboxMessage>();
        foreach (var pending in messages)
        {
            var message = pending.Message;
            message.SequenceNumber = await NextSequenceNumberAsync(connection, transaction, message.Resource.Id!, cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Messages (Id, ResourceId, SequenceNumber, CorrelationId, Json, CreatedAt)
                VALUES ($id, $resourceId, $sequence, $correlationId, $json, $createdAt)
                """;
            command.Parameters.AddWithValue("$id", message.Id);
            command.Parameters.AddWithValue("$resourceId", message.Resource.Id);
            command.Parameters.AddWithValue("$sequence", message.SequenceNumber);
            command.Parameters.AddWithValue("$correlationId", (object?)pending.CorrelationId ?? DBNull.Value);
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(message, StubJson.Options));
            command.Parameters.AddWithValue("$createdAt", message.CreatedAt.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
            committed.Add(new OutboxMessage(message, pending.CorrelationId));
        }

        await transaction.CommitAsync(cancellationToken);
        return committed;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string typeId, string id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Resources WHERE TypeId = $typeId AND Id = $id";
        command.Parameters.AddWithValue("$typeId", typeId);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> GetUnpublishedMessagesAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Json, CorrelationId FROM Messages WHERE PublishedAt IS NULL ORDER BY CreatedAt, SequenceNumber LIMIT $limit";
        command.Parameters.AddWithValue("$limit", maxCount);
        var results = new List<OutboxMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new OutboxMessage(
                Deserialize<CommerceMessage>(reader.GetString(0)),
                reader.IsDBNull(1) ? null : reader.GetString(1)));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task MarkPublishedAsync(string messageId, DateTimeOffset publishedAt, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Messages SET PublishedAt = $publishedAt WHERE Id = $id";
        command.Parameters.AddWithValue("$publishedAt", publishedAt.ToString("O"));
        command.Parameters.AddWithValue("$id", messageId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> NextSequenceNumberAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string resourceId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COALESCE(MAX(SequenceNumber), 0) + 1 FROM Messages WHERE ResourceId = $resourceId";
        command.Parameters.AddWithValue("$resourceId", resourceId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, StubJson.Options)
        ?? throw new InvalidOperationException($"Stored {typeof(T).Name} document was empty.");

    private async Task<T?> QuerySingleAsync<T>(string sql, string typeId, string value, CancellationToken cancellationToken)
        where T : class
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$typeId", typeId);
        command.Parameters.AddWithValue("$value", value);
        return await command.ExecuteScalarAsync(cancellationToken) is string json ? Deserialize<T>(json) : null;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        await _initialization.Value;
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = Schema;
        await command.ExecuteNonQueryAsync();
    }
}
