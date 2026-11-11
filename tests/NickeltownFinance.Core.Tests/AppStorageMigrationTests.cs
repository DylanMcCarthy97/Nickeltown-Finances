using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.Storage;
using Xunit;

namespace NickeltownFinance.Core.Tests;

public class AppStorageMigrationTests
{
    [Fact]
    public void AccessDenied_in_onedrive_explains_the_folder_without_the_extended_prefix()
    {
        var exception = new UnauthorizedAccessException(
            @"Access to the path '\\?\C:\Users\dylan\OneDrive\Documents\NickeltownFinance\files\month-documents\6a5d9e1fe7ff6b02fd26126e\previews' is denied.");

        var message = StorageAccess.ForUser(exception);

        Assert.Contains("OneDrive", message);
        Assert.Contains(
            @"C:\Users\dylan\OneDrive\Documents\NickeltownFinance\files\month-documents\6a5d9e1fe7ff6b02fd26126e\previews",
            message);
        Assert.DoesNotContain(@"\\?\", message);
    }

    [Fact]
    public void AccessDenied_to_backups_names_onedrive_instead_of_a_startup_crash()
    {
        var exception = new UnauthorizedAccessException(
            @"Access to the path 'C:\Users\nicke\OneDrive\Documents\NickeltownFinance\backups' is denied.");

        var message = StorageAccess.ForUser(exception, "Nickeltown Finance failed to start:");

        Assert.Contains("OneDrive", message);
        Assert.Contains(@"NickeltownFinance\backups", message);
        Assert.DoesNotContain("failed to start", message);
    }

    [Fact]
    public void Other_errors_keep_their_message()
    {
        var exception = new InvalidOperationException("Enter the sync server URL.");

        Assert.Equal("Enter the sync server URL.", StorageAccess.ForUser(exception));
        Assert.Equal(
            "Nickeltown Finance failed to start:\n\nEnter the sync server URL.",
            StorageAccess.ForUser(exception, "Nickeltown Finance failed to start:"));
    }

    [Fact]
    public void Backup_folder_inside_onedrive_documents_is_retargeted()
    {
        var legacy = @"C:\Users\nicke\OneDrive\Documents\NickeltownFinance";
        var cloud = legacy + @"\backups";
        var local = @"C:\Users\nicke\AppData\Local\NickeltownFinance\backups";

        Assert.True(BackupFolderPolicy.ShouldRetarget(cloud, legacy, local));
        Assert.True(BackupFolderPolicy.ShouldRetarget(null, legacy, local));
        Assert.True(BackupFolderPolicy.ShouldRetarget("  ", legacy, local));
    }

    [Fact]
    public void Backup_folder_on_this_pc_or_a_usb_drive_is_kept()
    {
        var legacy = @"C:\Users\nicke\OneDrive\Documents\NickeltownFinance";
        var local = @"C:\Users\nicke\AppData\Local\NickeltownFinance\backups";

        Assert.False(BackupFolderPolicy.ShouldRetarget(local, legacy, local));
        Assert.False(BackupFolderPolicy.ShouldRetarget(@"D:\ClubBackups", legacy, local));
    }

    [Fact]
    public void Onedrive_backup_folder_resolves_locally_without_creating_the_cloud_path()
    {
        using var workspace = new Workspace();
        var cloudParent = workspace.At("cloud");
        File.WriteAllText(cloudParent, "blocked");
        var cloud = Path.Combine(cloudParent, "OneDrive", "Documents", "NickeltownFinance", "backups");
        var legacy = Path.Combine(cloudParent, "OneDrive", "Documents", "NickeltownFinance");
        var local = workspace.At("local");

        var resolved = BackupFolderPolicy.ResolveWritableFolder(cloud, legacy, local);

        Assert.Equal(Path.GetFullPath(local), Path.GetFullPath(resolved));
        Assert.True(Directory.Exists(local));
        Assert.False(Directory.Exists(cloud));
    }

    [Fact]
    public void A_blocked_usb_folder_is_not_replaced()
    {
        using var workspace = new Workspace();
        var usb = workspace.At("usb-file");
        File.WriteAllText(usb, "not a directory");
        var local = workspace.At("local");

        Assert.Throws<IOException>(() =>
            BackupFolderPolicy.ResolveWritableFolder(usb, workspace.At("legacy"), local));
        Assert.False(Directory.Exists(local));
    }

    [Fact]
    public void Working_files_move_out_of_documents_and_the_originals_stay()
    {
        using var workspace = new Workspace();
        var legacy = workspace.At("documents");
        var local = workspace.At("local");
        var receipt = Path.Combine(legacy, "files", "month-documents", "abc", "statement.pdf");
        var preview = Path.Combine(legacy, "files", "month-documents", "abc", "previews", "page1.jpg");
        var backup = Path.Combine(legacy, "backups", "nickeltown_auto.zip");
        var database = Path.Combine(legacy, "data", "nickeltown.db");
        Write(receipt, "receipt");
        Write(preview, "preview");
        Write(backup, "zip");
        Write(database, "books");

        var denied = Path.Combine(legacy, "files", "month-documents", "abc", "previews");
        var result = WorkingTreeMigration.EnsureLocalCopy(local, legacy, path =>
        {
            if (string.Equals(path, denied, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException($"Access to the path '{path}' is denied.");
            return Directory.EnumerateFileSystemEntries(path);
        });

        Assert.Equal("receipt", File.ReadAllText(Path.Combine(local, "files", "month-documents", "abc", "statement.pdf")));
        Assert.Equal("zip", File.ReadAllText(Path.Combine(local, "backups", "nickeltown_auto.zip")));
        Assert.False(File.Exists(Path.Combine(local, "files", "month-documents", "abc", "previews", "page1.jpg")));
        Assert.False(File.Exists(Path.Combine(local, "data", "nickeltown.db")));
        Assert.Equal("receipt", File.ReadAllText(receipt));
        Assert.Equal("preview", File.ReadAllText(preview));
        Assert.Equal("books", File.ReadAllText(database));
        Assert.Contains(result.SkippedPaths, path => path.Contains("previews", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, result.CopiedFiles);
    }

    [Fact]
    public void A_later_launch_copies_a_folder_onedrive_had_blocked()
    {
        using var workspace = new Workspace();
        var legacy = workspace.At("documents");
        var local = workspace.At("local");
        var preview = Path.Combine(legacy, "files", "month-documents", "abc", "previews", "page1.jpg");
        Write(preview, "preview");
        var denied = Path.GetDirectoryName(preview)!;

        WorkingTreeMigration.EnsureLocalCopy(local, legacy, path =>
        {
            if (string.Equals(path, denied, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException($"Access to the path '{path}' is denied.");
            return Directory.EnumerateFileSystemEntries(path);
        });

        var retry = WorkingTreeMigration.EnsureLocalCopy(local, legacy);

        Assert.Equal(1, retry.CopiedFiles);
        Assert.Equal("preview", File.ReadAllText(Path.Combine(local, "files", "month-documents", "abc", "previews", "page1.jpg")));
    }

    [Fact]
    public void Local_copy_is_not_replaced_when_documents_still_has_the_file()
    {
        using var workspace = new Workspace();
        var legacy = workspace.At("documents");
        var local = workspace.At("local");
        Write(Path.Combine(legacy, "files", "logo.png"), "old");
        Write(Path.Combine(local, "files", "logo.png"), "current");

        var result = WorkingTreeMigration.EnsureLocalCopy(local, legacy);

        Assert.Equal(0, result.CopiedFiles);
        Assert.Equal("current", File.ReadAllText(Path.Combine(local, "files", "logo.png")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(legacy, "files", "logo.png")));
    }

    [Fact]
    public void Listing_files_skips_a_denied_previews_folder_and_keeps_the_rest()
    {
        using var workspace = new Workspace();
        var root = workspace.At("files");
        var receipt = Path.Combine(root, "month-documents", "abc", "statement.pdf");
        var previewDir = Path.Combine(root, "month-documents", "abc", "previews");
        Write(receipt, "receipt");
        Write(Path.Combine(previewDir, "page1.jpg"), "preview");
        var skipped = new List<string>();

        var files = FileTreeWalker.ListFiles(root, skipped, path =>
        {
            if (string.Equals(path, previewDir, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException($"Access to the path '{path}' is denied.");
            return Directory.EnumerateFileSystemEntries(path);
        });

        Assert.Contains(files, file => file.EndsWith("statement.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, file => file.EndsWith("page1.jpg", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(skipped, path => path.Contains("previews", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Relative_paths_resolve_to_the_local_copy_when_it_exists()
    {
        var name = "migration-" + Guid.NewGuid().ToString("N");
        var relative = Path.Combine("files", name, "note.txt");
        var local = Path.Combine(AppPaths.AppDataRoot, relative);
        var legacy = Path.Combine(AppPaths.LegacyAppDataRoot, relative);
        Write(local, "local");
        Write(legacy, "legacy");
        try
        {
            Assert.Equal(Path.GetFullPath(local), Path.GetFullPath(AppPaths.ResolvePath("files/" + name + "/note.txt")));
            Assert.Equal("files/" + name + "/note.txt", AppPaths.ToRelativePath(legacy));
            Assert.Equal("files/" + name + "/note.txt", AppPaths.ToRelativePath(local));
        }
        finally
        {
            Delete(Path.GetDirectoryName(local)!);
            Delete(Path.GetDirectoryName(legacy)!);
        }
    }

    [Fact]
    public void Missing_local_file_still_opens_from_documents_until_it_can_be_copied()
    {
        var name = "migration-" + Guid.NewGuid().ToString("N");
        var relative = Path.Combine("files", name, "note.txt");
        var legacy = Path.Combine(AppPaths.LegacyAppDataRoot, relative);
        Write(legacy, "legacy");
        try
        {
            Assert.Equal(
                Path.GetFullPath(legacy),
                Path.GetFullPath(AppPaths.ResolvePath("files/" + name + "/note.txt")));
        }
        finally
        {
            Delete(Path.GetDirectoryName(legacy)!);
        }
    }

    private static void Write(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, contents);
    }

    private static void Delete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class Workspace : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "nickeltown-storage-" + Guid.NewGuid().ToString("N"));

        public Workspace() => Directory.CreateDirectory(_root);

        public string At(params string[] parts) => Path.Combine(new[] { _root }.Concat(parts).ToArray());

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
