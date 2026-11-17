using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace TaskTune.AttachmentMigration;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) ||
                args.Contains("-h", StringComparer.OrdinalIgnoreCase))
            {
                PrintUsage();
                return 0;
            }

            var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
            var rollback = args.Contains("--rollback", StringComparer.OrdinalIgnoreCase);
            if (apply && rollback)
                throw new ArgumentException("Choose either --apply or --rollback, not both.");

            var manifestArgument = GetOption(args, "--manifest");
            if ((apply || rollback) && string.IsNullOrWhiteSpace(manifestArgument))
                throw new ArgumentException("--manifest is required for --apply and --rollback.");

            var connectionString = GetRequiredEnvironment("TASKTUNE_SQLSERVER_CONNECTION");
            var legacyRoot = GetRequiredEnvironment("TASKTUNE_LEGACY_UPLOAD_ROOT");
            var privateRoot = GetRequiredEnvironment("TASKTUNE_PRIVATE_UPLOAD_ROOT");
            ValidateStorageRoots(legacyRoot, privateRoot);

            if (!Directory.Exists(privateRoot))
            {
                AttachmentMigrationSafety.EnsureNoReparsePoints(privateRoot);
                Directory.CreateDirectory(privateRoot);
            }
            AttachmentMigrationSafety.EnsureNoReparsePoints(privateRoot);

            var manifestPath = string.IsNullOrWhiteSpace(manifestArgument)
                ? null
                : AttachmentMigrationSafety.ResolveManifestPath(manifestArgument, legacyRoot, privateRoot);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await AcquireMigrationLockAsync(connection);

            if (rollback)
            {
                await RollbackAsync(connection, privateRoot, manifestPath!);
                return 0;
            }

            var rows = await LoadLegacyRowsAsync(connection);
            Console.WriteLine($"Found {rows.Count} legacy attachment record(s).");
            if (!apply)
                Console.WriteLine("DRY RUN: no file copies or database updates will be performed. Pass --apply to execute.");

            var succeeded = 0;
            var failed = 0;
            foreach (var row in rows)
            {
                try
                {
                    var sourcePath = AttachmentMigrationSafety.ResolveLegacySourcePath(legacyRoot, row.FilePath);
                    var info = new FileInfo(sourcePath);
                    if (info.Length != row.FileSize)
                        throw new InvalidOperationException($"Database FileSize ({row.FileSize}) does not match disk length ({info.Length}).");

                    var hash = await ComputeSha256Async(sourcePath);
                    var storageKey = AttachmentMigrationSafety.CreatePrivateStorageKey(
                        row.TaskId, row.AttachmentId, Path.GetFileName(sourcePath));
                    var destinationPath = AttachmentMigrationSafety.ResolvePrivateDestinationPath(privateRoot, storageKey);

                    if (!apply)
                    {
                        Console.WriteLine($"DRY RUN attachment={row.AttachmentId} task={row.TaskId} size={info.Length} sha256={hash} key={storageKey}");
                        succeeded++;
                        continue;
                    }

                    await MigrateOneAsync(
                        connection, row, sourcePath, destinationPath, storageKey,
                        info.Length, hash, manifestPath!, CancellationToken.None);
                    Console.WriteLine($"MIGRATED attachment={row.AttachmentId} key={storageKey} sha256={hash}");
                    succeeded++;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"FAILED attachment={row.AttachmentId}: {exception.Message}");
                    failed++;
                }
            }

            Console.WriteLine($"Completed: {succeeded} succeeded, {failed} failed. Legacy source files were not deleted.");
            return failed == 0 ? 0 : 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Attachment migration stopped: {exception.Message}");
            return 1;
        }
    }

    private static async Task<List<LegacyAttachmentRow>> LoadLegacyRowsAsync(SqlConnection connection)
    {
        const string sql = """
            SELECT AttachmentId, TaskId, FilePath, FileSize
            FROM dbo.TaskAttachments
            WHERE FilePath LIKE N'/Uploads/%'
            ORDER BY AttachmentId;
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<LegacyAttachmentRow>();
        while (await reader.ReadAsync())
            rows.Add(new LegacyAttachmentRow(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3)));
        return rows;
    }

    private static async Task MigrateOneAsync(
        SqlConnection connection,
        LegacyAttachmentRow row,
        string sourcePath,
        string destinationPath,
        string storageKey,
        long expectedLength,
        string expectedHash,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("Could not determine destination directory.");
        Directory.CreateDirectory(destinationDirectory);
        AttachmentMigrationSafety.EnsureNoReparsePoints(destinationPath);
        if (File.Exists(destinationPath))
            throw new IOException("The private destination already exists; refusing to overwrite it.");

        var prepared = new ManifestRecord
        {
            Event = "prepared",
            AttachmentId = row.AttachmentId,
            TaskId = row.TaskId,
            OriginalPath = row.FilePath,
            StorageKey = storageKey,
            Size = expectedLength,
            Sha256 = expectedHash,
            TimestampUtc = DateTimeOffset.UtcNow
        };
        // Write intent before copying/DB mutation so a crash can be reconciled by --rollback.
        await AppendManifestAsync(manifestPath, prepared, cancellationToken);

        var temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var databaseUpdated = false;
        try
        {
            var copied = await CopyAndHashAsync(sourcePath, temporaryPath, cancellationToken);
            if (copied.Length != expectedLength ||
                !string.Equals(copied.Sha256, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The copied attachment failed its source checksum or length verification.");

            AttachmentMigrationSafety.EnsureNoReparsePoints(destinationPath);
            File.Move(temporaryPath, destinationPath);

            databaseUpdated = await UpdatePathAsync(connection, row, storageKey, expectedLength, cancellationToken);
            if (!databaseUpdated)
                throw new InvalidOperationException("The database row changed after inventory; private copy was discarded.");

            await AppendManifestAsync(manifestPath, prepared with
            {
                Event = "committed",
                TimestampUtc = DateTimeOffset.UtcNow
            }, cancellationToken);
        }
        catch
        {
            if (!databaseUpdated)
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                if (File.Exists(destinationPath)) File.Delete(destinationPath);
            }
            throw;
        }
    }

    private static async Task<bool> UpdatePathAsync(
        SqlConnection connection,
        LegacyAttachmentRow row,
        string storageKey,
        long fileSize,
        CancellationToken cancellationToken)
    {
        using var transaction = connection.BeginTransaction();
        await using var command = new SqlCommand("""
            UPDATE dbo.TaskAttachments
            SET FilePath = @NewPath, FileSize = @FileSize
            WHERE AttachmentId = @AttachmentId AND TaskId = @TaskId AND FilePath = @OldPath;
            """, connection, transaction);
        command.Parameters.Add("@NewPath", SqlDbType.NVarChar, 500).Value = storageKey;
        command.Parameters.Add("@FileSize", SqlDbType.Int).Value = checked((int)fileSize);
        command.Parameters.Add("@AttachmentId", SqlDbType.Int).Value = row.AttachmentId;
        command.Parameters.Add("@TaskId", SqlDbType.Int).Value = row.TaskId;
        command.Parameters.Add("@OldPath", SqlDbType.NVarChar, 500).Value = row.FilePath;

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected != 1)
        {
            transaction.Rollback();
            return false;
        }

        transaction.Commit();
        return true;
    }

    private static async Task RollbackAsync(SqlConnection connection, string privateRoot, string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("The attachment migration manifest does not exist.", manifestPath);

        var latest = new Dictionary<int, ManifestRecord>();
        foreach (var line in await File.ReadAllLinesAsync(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var entry = JsonSerializer.Deserialize<ManifestRecord>(line, JsonOptions)
                ?? throw new InvalidDataException("The manifest contains an invalid record.");
            latest[entry.AttachmentId] = entry;
        }

        var pending = latest.Values.Where(entry => entry.Event is "prepared" or "committed")
            .OrderBy(entry => entry.AttachmentId).ToList();
        Console.WriteLine($"Found {pending.Count} attachment record(s) eligible for rollback.");

        foreach (var entry in pending)
        {
            var currentPath = await GetCurrentFilePathAsync(connection, entry.AttachmentId, entry.TaskId);
            if (currentPath == null)
                throw new InvalidOperationException($"Attachment {entry.AttachmentId} no longer exists.");

            if (string.Equals(currentPath, entry.OriginalPath, StringComparison.Ordinal))
            {
                await AppendManifestAsync(manifestPath, entry with { Event = "rolled_back", TimestampUtc = DateTimeOffset.UtcNow }, CancellationToken.None);
                Console.WriteLine($"ALREADY ROLLED BACK attachment={entry.AttachmentId}");
                continue;
            }

            if (!string.Equals(currentPath, entry.StorageKey, StringComparison.Ordinal))
                throw new InvalidOperationException($"Attachment {entry.AttachmentId} now references an unexpected path; refusing to overwrite that change.");

            var migratedFile = AttachmentMigrationSafety.ResolvePrivateDestinationPath(privateRoot, entry.StorageKey);
            if (!File.Exists(migratedFile))
                throw new FileNotFoundException("The migrated private copy is missing; database path was not rolled back.", migratedFile);

            var fileInfo = new FileInfo(migratedFile);
            var actualHash = await ComputeSha256Async(migratedFile);
            if (fileInfo.Length != entry.Size || !string.Equals(actualHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Migrated file verification failed for attachment {entry.AttachmentId}.");

            await RestoreOriginalPathAsync(connection, entry);
            await AppendManifestAsync(manifestPath, entry with { Event = "rolled_back", TimestampUtc = DateTimeOffset.UtcNow }, CancellationToken.None);
            Console.WriteLine($"ROLLED BACK metadata attachment={entry.AttachmentId}; private copy retained.");
        }
    }

    private static async Task<string?> GetCurrentFilePathAsync(SqlConnection connection, int attachmentId, int taskId)
    {
        await using var command = new SqlCommand("""
            SELECT FilePath FROM dbo.TaskAttachments
            WHERE AttachmentId = @AttachmentId AND TaskId = @TaskId;
            """, connection);
        command.Parameters.Add("@AttachmentId", SqlDbType.Int).Value = attachmentId;
        command.Parameters.Add("@TaskId", SqlDbType.Int).Value = taskId;
        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task RestoreOriginalPathAsync(SqlConnection connection, ManifestRecord entry)
    {
        using var transaction = connection.BeginTransaction();
        await using var command = new SqlCommand("""
            UPDATE dbo.TaskAttachments
            SET FilePath = @OriginalPath
            WHERE AttachmentId = @AttachmentId AND TaskId = @TaskId AND FilePath = @StorageKey;
            """, connection, transaction);
        command.Parameters.Add("@OriginalPath", SqlDbType.NVarChar, 500).Value = entry.OriginalPath;
        command.Parameters.Add("@AttachmentId", SqlDbType.Int).Value = entry.AttachmentId;
        command.Parameters.Add("@TaskId", SqlDbType.Int).Value = entry.TaskId;
        command.Parameters.Add("@StorageKey", SqlDbType.NVarChar, 500).Value = entry.StorageKey;
        if (await command.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException($"Attachment {entry.AttachmentId} changed during rollback.");
        transaction.Commit();
    }

    private static async Task<(long Length, string Sha256)> CopyAndHashAsync(
        string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var source = new FileStream(
            sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[81920];
        long length = 0;
        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            length += read;
        }

        await destination.FlushAsync(cancellationToken);
        destination.Flush(flushToDisk: true);
        var sourceHash = Convert.ToHexString(hash.GetHashAndReset());
        var destinationHash = await ComputeSha256Async(destinationPath, cancellationToken);
        if (!string.Equals(sourceHash, destinationHash, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The copied attachment failed its SHA-256 verification.");
        return (length, sourceHash);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static async Task AppendManifestAsync(
        string manifestPath, ManifestRecord entry, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(manifestPath)
            ?? throw new InvalidOperationException("Manifest path must include a directory.");
        Directory.CreateDirectory(directory);
        AttachmentMigrationSafety.EnsureNoReparsePoints(manifestPath);

        await using var stream = new FileStream(
            manifestPath, FileMode.Append, FileAccess.Write, FileShare.Read,
            bufferSize: 4096, FileOptions.Asynchronous);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(entry, JsonOptions).AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static async Task AcquireMigrationLockAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = N'TaskTune.LegacyAttachmentMigration',
                @LockMode = N'Exclusive',
                @LockOwner = N'Session',
                @LockTimeout = 0;
            SELECT @result;
            """, connection);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (result < 0)
            throw new InvalidOperationException("Another attachment migration process currently holds the database lock.");
    }

    private static void ValidateStorageRoots(string legacyRoot, string privateRoot)
    {
        var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(legacyRoot));
        var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(privateRoot));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (source.Equals(destination, comparison) ||
            AttachmentMigrationSafety.IsWithinDirectory(source, destination) ||
            AttachmentMigrationSafety.IsWithinDirectory(destination, source))
            throw new InvalidOperationException("Legacy and private upload roots must be separate, non-nested directories.");
    }

    private static string GetRequiredEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Environment variable {name} is required.")
            : value;
    }

    private static string? GetOption(string[] args, string option)
    {
        var index = Array.FindIndex(args, item => string.Equals(item, option, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return null;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Value is required after {option}.");
        return args[index + 1];
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            TaskTune legacy attachment migration

            Environment:
              TASKTUNE_SQLSERVER_CONNECTION   SQL Server connection string
              TASKTUNE_LEGACY_UPLOAD_ROOT     Physical directory for legacy /Uploads/{fileName}
              TASKTUNE_PRIVATE_UPLOAD_ROOT    Active API App_Data/tasktune-private/uploads root

            Commands:
              (no switch)                            Inventory and verify file hashes; no writes
              --apply --manifest <path>              Copy, checksum and update metadata
              --rollback --manifest <path>           Restore DB paths from the manifest

            Dry-run is the default. --apply is required to copy or change database metadata.
            Legacy and private files are never deleted. Store the manifest outside both upload roots.
            Back up the database and upload directory before applying.
            """);
    }

    private sealed record LegacyAttachmentRow(int AttachmentId, int TaskId, string FilePath, int FileSize);

    private sealed record ManifestRecord
    {
        public string Event { get; init; } = string.Empty;
        public int AttachmentId { get; init; }
        public int TaskId { get; init; }
        public string OriginalPath { get; init; } = string.Empty;
        public string StorageKey { get; init; } = string.Empty;
        public long Size { get; init; }
        public string Sha256 { get; init; } = string.Empty;
        public DateTimeOffset TimestampUtc { get; init; }
    }
}
