using TaskTune.AttachmentMigration;
using Xunit;

namespace TaskTune.Tests;

public sealed class AttachmentMigrationSafetyTests
{
    [Fact]
    public void ResolveLegacySourcePath_AllowsOnlyExistingFlatUploadsFiles()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var fileName = "a12b34c56d7890.jpg";
            var physicalPath = Path.Combine(root, fileName);
            File.WriteAllBytes(physicalPath, [1, 2, 3]);
            var resolved = AttachmentMigrationSafety.ResolveLegacySourcePath(root, "/Uploads/" + fileName);
            Assert.Equal(Path.GetFullPath(physicalPath), resolved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("/Uploads/../secret.jpg")]
    [InlineData("/Uploads/subfolder/photo.jpg")]
    [InlineData("/other/photo.jpg")]
    [InlineData("/Uploads/C:secret.jpg")]
    [InlineData("/Uploads/")]
    public void ResolveLegacySourcePath_RejectsTraversalAndNonLegacyPaths(string legacyPath)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            Assert.Throws<InvalidOperationException>(
                () => AttachmentMigrationSafety.ResolveLegacySourcePath(root, legacyPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CreatePrivateStorageKey_UsesTaskScopedRelativeKeyAndSafeExtension()
    {
        var key = AttachmentMigrationSafety.CreatePrivateStorageKey(
            taskId: 42,
            attachmentId: 9001,
            legacyFileName: "legacy-document.PDF",
            stableToken: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        Assert.Equal("tasks/42/legacy_9001_aaaaaaaabbbbccccddddeeeeeeeeeeee.pdf", key);
    }

    [Theory]
    [InlineData("../escape.jpg")]
    [InlineData("tasks/1/../../escape.jpg")]
    [InlineData("/tasks/1/file.jpg")]
    [InlineData("tasks/1/sub/file.jpg")]
    [InlineData("tasks/1/file.exe")]
    public void ResolvePrivateDestinationPath_RejectsUnsafeKeys(string key)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            Assert.Throws<InvalidOperationException>(
                () => AttachmentMigrationSafety.ResolvePrivateDestinationPath(root, key));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveLegacySourcePath_RejectsSymbolicLinkFiles()
    {
        var root = CreateTemporaryDirectory();
        var outside = CreateTemporaryDirectory();
        try
        {
            var target = Path.Combine(outside, "outside.jpg");
            File.WriteAllBytes(target, [1, 2, 3]);
            File.CreateSymbolicLink(Path.Combine(root, "linked.jpg"), target);
            Assert.Throws<InvalidOperationException>(
                () => AttachmentMigrationSafety.ResolveLegacySourcePath(root, "/Uploads/linked.jpg"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "tasktune-attachment-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
