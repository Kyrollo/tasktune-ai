namespace TaskTune.AttachmentMigration;

public static class AttachmentMigrationSafety
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg",
        ".gif", ".zip", ".txt", ".csv"
    };

    public static string ResolveLegacySourcePath(string legacyUploadRoot, string legacyFilePath)
    {
        var root = ResolveExistingDirectory(legacyUploadRoot);
        var normalized = (legacyFilePath ?? string.Empty).Replace('\\', '/');
        const string legacyPrefix = "/Uploads/";
        if (!normalized.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only legacy /Uploads/{file} records are eligible for migration.");

        var fileName = normalized[legacyPrefix.Length..];
        ValidateSimpleFileName(fileName);
        ValidateExtension(fileName);

        var fullPath = Path.GetFullPath(Path.Combine(root, fileName));
        EnsureContained(root, fullPath);
        EnsureNoReparsePoints(fullPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The legacy attachment file does not exist.", fullPath);
        return fullPath;
    }

    public static string CreatePrivateStorageKey(
        int taskId,
        int attachmentId,
        string legacyFileName,
        Guid? stableToken = null)
    {
        if (taskId <= 0) throw new ArgumentOutOfRangeException(nameof(taskId));
        if (attachmentId <= 0) throw new ArgumentOutOfRangeException(nameof(attachmentId));
        var extension = Path.GetExtension(legacyFileName ?? string.Empty);
        ValidateExtension("attachment" + extension);
        var token = (stableToken ?? Guid.NewGuid()).ToString("N");
        return $"tasks/{taskId}/legacy_{attachmentId}_{token}{extension.ToLowerInvariant()}";
    }

    public static string ResolvePrivateDestinationPath(string privateUploadRoot, string storageKey)
    {
        var root = ResolveExistingDirectory(privateUploadRoot);
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Contains('\\'))
            throw new InvalidOperationException("The private storage key is invalid.");

        var parts = storageKey.Split('/');
        if (parts.Length != 3 || !string.Equals(parts[0], "tasks", StringComparison.Ordinal) ||
            !int.TryParse(parts[1], out var taskId) || taskId <= 0)
            throw new InvalidOperationException("The private storage key must use tasks/{taskId}/{fileName}.");

        ValidateSimpleFileName(parts[2]);
        ValidateExtension(parts[2]);
        var fullPath = Path.GetFullPath(Path.Combine(root, parts[0], parts[1], parts[2]));
        EnsureContained(root, fullPath);
        EnsureNoReparsePoints(fullPath);
        return fullPath;
    }

    public static string ResolveManifestPath(string manifestPath, string legacyUploadRoot, string privateUploadRoot)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
            throw new ArgumentException("A manifest path is required.", nameof(manifestPath));

        var fullManifest = Path.GetFullPath(manifestPath);
        var sourceRoot = Path.GetFullPath(legacyUploadRoot);
        var privateRoot = Path.GetFullPath(privateUploadRoot);
        if (IsWithinDirectory(sourceRoot, fullManifest) || IsWithinDirectory(privateRoot, fullManifest))
            throw new InvalidOperationException("Store the migration manifest outside both upload roots.");
        EnsureNoReparsePoints(fullManifest);
        return fullManifest;
    }

    public static bool IsWithinDirectory(string directory, string candidate)
    {
        var fullDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var fullCandidate = Path.GetFullPath(candidate);
        var prefix = fullDirectory + Path.DirectorySeparatorChar;
        return fullCandidate.StartsWith(
            prefix,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    public static void EnsureNoReparsePoints(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var volumeRoot = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(volumeRoot))
            throw new InvalidOperationException("The path has no filesystem root.");

        var current = volumeRoot;
        var segments = fullPath[volumeRoot.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
                break;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"Reparse points and symbolic links are not allowed: {current}");
        }
    }

    private static string ResolveExistingDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A directory path is required.", nameof(directory));
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Directory does not exist: {fullPath}");
        EnsureNoReparsePoints(fullPath);
        return fullPath;
    }

    private static void ValidateSimpleFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or ".." ||
            Path.IsPathRooted(fileName) || fileName.Contains('/') || fileName.Contains('\\') ||
            fileName.Contains(':') || fileName.Any(char.IsControl) ||
            !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
            throw new InvalidOperationException("Attachment file names must be simple, single-segment names.");
    }

    private static void ValidateExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"Attachment extension '{extension}' is not allowed.");
    }

    private static void EnsureContained(string root, string candidate)
    {
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!Path.GetFullPath(candidate).StartsWith(prefix, comparison))
            throw new InvalidOperationException("Resolved attachment path escapes its configured storage root.");
    }
}
