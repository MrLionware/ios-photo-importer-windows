using System.Data;
using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Models;
using Microsoft.Data.Sqlite;

namespace IosPhotoImporter.Infrastructure.Data;

public sealed class SqliteImportStateRepository(SqliteRepositoryOptions options) : IImportStateRepository
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = options.DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        ForeignKeys = true
    }.ToString();

    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);

        const string sql = """
            CREATE TABLE IF NOT EXISTS devices (
                device_id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                first_seen_utc TEXT NOT NULL,
                last_seen_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS imported_assets (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                device_id TEXT NOT NULL,
                persistent_id TEXT NULL,
                source_object_id TEXT NOT NULL,
                file_name TEXT NOT NULL,
                size_bytes INTEGER NOT NULL,
                sha256 TEXT NULL,
                local_path TEXT NOT NULL,
                imported_at_utc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_imported_assets_device_persistent
                ON imported_assets(device_id, persistent_id);

            CREATE INDEX IF NOT EXISTS idx_imported_assets_device_sha
                ON imported_assets(device_id, sha256);

            CREATE TABLE IF NOT EXISTS import_jobs (
                job_id TEXT PRIMARY KEY,
                device_id TEXT NOT NULL,
                destination_path TEXT NOT NULL,
                status INTEGER NOT NULL,
                started_at_utc TEXT NOT NULL,
                ended_at_utc TEXT NULL,
                last_checkpoint_utc TEXT NOT NULL,
                last_error TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS import_job_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                job_id TEXT NOT NULL,
                source_object_id TEXT NOT NULL,
                state INTEGER NOT NULL,
                error_code TEXT NULL,
                error_message TEXT NULL,
                UNIQUE(job_id, source_object_id)
            );

            CREATE INDEX IF NOT EXISTS idx_job_items_job_state
                ON import_job_items(job_id, state);
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task UpsertDeviceAsync(DeviceInfo deviceInfo, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        const string sql = """
            INSERT INTO devices(device_id, display_name, first_seen_utc, last_seen_utc)
            VALUES($device_id, $display_name, $first_seen_utc, $last_seen_utc)
            ON CONFLICT(device_id) DO UPDATE SET
                display_name = excluded.display_name,
                last_seen_utc = excluded.last_seen_utc;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$device_id", deviceInfo.DeviceId);
        command.Parameters.AddWithValue("$display_name", deviceInfo.Name);
        command.Parameters.AddWithValue("$first_seen_utc", now.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$last_seen_utc", now.UtcDateTime.ToString("O"));

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task CreateJobAsync(ImportJob job, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);

        const string sql = """
            INSERT INTO import_jobs(
                job_id,
                device_id,
                destination_path,
                status,
                started_at_utc,
                ended_at_utc,
                last_checkpoint_utc,
                last_error)
            VALUES(
                $job_id,
                $device_id,
                $destination_path,
                $status,
                $started_at_utc,
                $ended_at_utc,
                $last_checkpoint_utc,
                $last_error);
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$job_id", job.JobId.ToString());
        command.Parameters.AddWithValue("$device_id", job.DeviceId);
        command.Parameters.AddWithValue("$destination_path", job.DestinationPath);
        command.Parameters.AddWithValue("$status", (int)job.Status);
        command.Parameters.AddWithValue("$started_at_utc", job.StartedAtUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$ended_at_utc", (object?)job.EndedAtUtc?.UtcDateTime.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$last_checkpoint_utc", job.LastCheckpointUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$last_error", (object?)job.LastError ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<ImportJob?> GetJobAsync(ImportJobId jobId, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);

        const string sql = """
            SELECT job_id, device_id, destination_path, status, started_at_utc, ended_at_utc, last_checkpoint_utc, last_error
            FROM import_jobs
            WHERE job_id = $job_id;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$job_id", jobId.ToString());

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        return new ImportJob(
            new ImportJobId(Guid.Parse(reader.GetString(0))),
            reader.GetString(1),
            reader.GetString(2),
            (ImportJobStatus)reader.GetInt32(3),
            DateTimeOffset.Parse(reader.GetString(4)),
            reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5)),
            DateTimeOffset.Parse(reader.GetString(6)),
            reader.IsDBNull(7) ? null : reader.GetString(7));
    }

    public async Task SetJobStatusAsync(ImportJobId jobId, ImportJobStatus status, string? errorMessage, DateTimeOffset? endedAtUtc, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);

        const string sql = """
            UPDATE import_jobs
            SET
                status = $status,
                ended_at_utc = $ended_at_utc,
                last_error = $last_error
            WHERE job_id = $job_id;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$job_id", jobId.ToString());
        command.Parameters.AddWithValue("$status", (int)status);
        command.Parameters.AddWithValue("$ended_at_utc", (object?)endedAtUtc?.UtcDateTime.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$last_error", (object?)errorMessage ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task SetCheckpointAsync(ImportJobId jobId, DateTimeOffset checkpointUtc, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);

        const string sql = """
            UPDATE import_jobs
            SET last_checkpoint_utc = $last_checkpoint_utc
            WHERE job_id = $job_id;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$job_id", jobId.ToString());
        command.Parameters.AddWithValue("$last_checkpoint_utc", checkpointUtc.UtcDateTime.ToString("O"));

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task UpsertJobItemAsync(ImportJobItem item, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);

        const string sql = """
            INSERT INTO import_job_items(job_id, source_object_id, state, error_code, error_message)
            VALUES($job_id, $source_object_id, $state, $error_code, $error_message)
            ON CONFLICT(job_id, source_object_id) DO UPDATE SET
                state = excluded.state,
                error_code = excluded.error_code,
                error_message = excluded.error_message;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$job_id", item.JobId.ToString());
        command.Parameters.AddWithValue("$source_object_id", item.SourceObjectId);
        command.Parameters.AddWithValue("$state", (int)item.State);
        command.Parameters.AddWithValue("$error_code", (object?)item.ErrorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$error_message", (object?)item.ErrorMessage ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ImportJobItem>> GetJobItemsAsync(ImportJobId jobId, CancellationToken ct)
    {
        return await GetJobItemsByStatesAsync(jobId, Array.Empty<ImportItemState>(), ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ImportJobItem>> GetJobItemsByStatesAsync(ImportJobId jobId, IReadOnlyCollection<ImportItemState> states, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);

        var command = connection.CreateCommand();
        if (states.Count == 0)
        {
            command.CommandText = """
                SELECT source_object_id, state, error_code, error_message
                FROM import_job_items
                WHERE job_id = $job_id
                ORDER BY id;
                """;
        }
        else
        {
            var stateParameters = states
                .Select((_, i) => $"$state_{i}")
                .ToArray();

            command.CommandText = $"""
                SELECT source_object_id, state, error_code, error_message
                FROM import_job_items
                WHERE job_id = $job_id
                  AND state IN ({string.Join(",", stateParameters)})
                ORDER BY id;
                """;

            var idx = 0;
            foreach (var state in states)
            {
                command.Parameters.AddWithValue(stateParameters[idx], (int)state);
                idx += 1;
            }
        }

        command.Parameters.AddWithValue("$job_id", jobId.ToString());

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var items = new List<ImportJobItem>();

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            items.Add(new ImportJobItem(
                jobId,
                reader.GetString(0),
                (ImportItemState)reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return items;
    }

    public async Task SetJobItemStateAsync(ImportJobId jobId, string sourceObjectId, ImportItemState state, string? errorCode, string? errorMessage, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);

        const string sql = """
            UPDATE import_job_items
            SET
                state = $state,
                error_code = $error_code,
                error_message = $error_message
            WHERE job_id = $job_id
              AND source_object_id = $source_object_id;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$job_id", jobId.ToString());
        command.Parameters.AddWithValue("$source_object_id", sourceObjectId);
        command.Parameters.AddWithValue("$state", (int)state);
        command.Parameters.AddWithValue("$error_code", (object?)errorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$error_message", (object?)errorMessage ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> IsPersistentIdImportedAsync(string deviceId, string persistentId, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);

        const string sql = """
            SELECT 1
            FROM imported_assets
            WHERE device_id = $device_id
              AND persistent_id = $persistent_id
            LIMIT 1;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$device_id", deviceId);
        command.Parameters.AddWithValue("$persistent_id", persistentId);

        var scalar = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return scalar is not null;
    }

    public async Task<bool> IsHashImportedAsync(string deviceId, string hashHex, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);

        const string sql = """
            SELECT 1
            FROM imported_assets
            WHERE device_id = $device_id
              AND sha256 = $sha256
            LIMIT 1;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$device_id", deviceId);
        command.Parameters.AddWithValue("$sha256", hashHex);

        var scalar = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return scalar is not null;
    }

    public async Task MarkImportedAssetAsync(ImportedAssetRecord importedAsset, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);

        const string sql = """
            INSERT INTO imported_assets(
                device_id,
                persistent_id,
                source_object_id,
                file_name,
                size_bytes,
                sha256,
                local_path,
                imported_at_utc)
            VALUES(
                $device_id,
                $persistent_id,
                $source_object_id,
                $file_name,
                $size_bytes,
                $sha256,
                $local_path,
                $imported_at_utc);
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$device_id", importedAsset.DeviceId);
        command.Parameters.AddWithValue("$persistent_id", (object?)importedAsset.PersistentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$source_object_id", importedAsset.SourceObjectId);
        command.Parameters.AddWithValue("$file_name", importedAsset.FileName);
        command.Parameters.AddWithValue("$size_bytes", importedAsset.SizeBytes);
        command.Parameters.AddWithValue("$sha256", (object?)importedAsset.Sha256 ?? DBNull.Value);
        command.Parameters.AddWithValue("$local_path", importedAsset.LocalPath);
        command.Parameters.AddWithValue("$imported_at_utc", importedAsset.ImportedAtUtc.UtcDateTime.ToString("O"));

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task ClearHistoryAsync(CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        var commands = new[]
        {
            "DELETE FROM import_job_items;",
            "DELETE FROM import_jobs;",
            "DELETE FROM imported_assets;"
        };

        foreach (var sql in commands)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = transaction;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(options.DatabasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await EnableWalModeAsync(connection, ct).ConfigureAwait(false);
        return connection;
    }

    private static async Task EnableWalModeAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }
}
