using LiteDB;
using NickeltownFinance.Core.Storage;
using Xunit;

namespace NickeltownFinance.Core.Tests;

public class DatabaseFileMigrationTests
{
    [Fact]
    public void GetLogFilePath_inserts_log_before_extension()
    {
        var path = DatabaseFileMigration.GetLogFilePath(
            Path.Combine("data", "nickeltown.db"));

        Assert.Equal(Path.Combine("data", "nickeltown-log.db"), path);
    }

    [Fact]
    public void EnsureLocalCopy_copies_books_and_leaves_the_original()
    {
        using var workspace = new Workspace();
        var legacy = workspace.At("legacy", "nickeltown.db");
        var local = workspace.At("local", "nickeltown.db");
        CreateDatabase(legacy, "Nickeltown");

        DatabaseFileMigration.EnsureLocalCopy(
            local,
            legacy,
            TimeSpan.FromSeconds(2),
            delay: _ => { },
            validate: CanRead);

        Assert.True(File.Exists(legacy));
        Assert.Equal("Nickeltown", ReadClub(local));
        Assert.False(File.Exists(workspace.At("local", "nickeltown.migrating.db")));
    }

    [Fact]
    public void EnsureLocalCopy_does_not_copy_the_recovery_log()
    {
        using var workspace = new Workspace();
        var legacy = workspace.At("legacy", "nickeltown.db");
        var local = workspace.At("local", "nickeltown.db");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(legacy)!);
        File.WriteAllBytes(legacy, new byte[32]);
        File.WriteAllText(DatabaseFileMigration.GetLogFilePath(legacy), "recovery");

        DatabaseFileMigration.EnsureLocalCopy(
            local,
            legacy,
            TimeSpan.FromSeconds(2),
            delay: _ => { },
            validate: _ => true);

        Assert.Equal(new byte[32], File.ReadAllBytes(local));
        Assert.False(File.Exists(DatabaseFileMigration.GetLogFilePath(local)));
        Assert.Equal("recovery", File.ReadAllText(DatabaseFileMigration.GetLogFilePath(legacy)));
    }

    [Fact]
    public void EnsureLocalCopy_does_not_replace_an_existing_local_database()
    {
        using var workspace = new Workspace();
        var legacy = workspace.At("legacy", "nickeltown.db");
        var local = workspace.At("local", "nickeltown.db");
        CreateDatabase(legacy, "Old");
        CreateDatabase(local, "Current");

        DatabaseFileMigration.EnsureLocalCopy(local, legacy, TimeSpan.Zero, delay: _ => { }, validate: CanRead);

        Assert.Equal("Current", ReadClub(local));
        Assert.Equal("Old", ReadClub(legacy));
    }

    [Fact]
    public void EnsureLocalCopy_leaves_the_original_when_the_copy_cannot_be_read()
    {
        using var workspace = new Workspace();
        var legacy = workspace.At("legacy", "nickeltown.db");
        var local = workspace.At("local", "nickeltown.db");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(legacy)!);
        File.WriteAllBytes(legacy, [1, 2, 3, 4]);

        var error = Assert.Throws<InvalidOperationException>(() =>
            DatabaseFileMigration.EnsureLocalCopy(
                local,
                legacy,
                TimeSpan.Zero,
                delay: _ => { },
                validate: _ => false));

        Assert.Contains(legacy, error.Message);
        Assert.False(File.Exists(local));
        Assert.False(File.Exists(workspace.At("local", "nickeltown.migrating.db")));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(legacy));
    }

    [Fact]
    public void ExecuteWithRetry_retries_sharing_violations_then_succeeds()
    {
        var attempts = 0;

        DatabaseFileMigration.ExecuteWithRetry(TimeSpan.FromSeconds(5), () =>
        {
            attempts++;
            if (attempts < 3)
                throw SharingViolation();
        }, delay: _ => { });

        Assert.Equal(3, attempts);
    }

    [Fact]
    public void ExecuteWithRetry_stops_when_the_timeout_is_already_spent()
    {
        var attempts = 0;

        var error = Assert.Throws<IOException>(() =>
            DatabaseFileMigration.ExecuteWithRetry(TimeSpan.Zero, () =>
            {
                attempts++;
                throw SharingViolation();
            }, delay: _ => { }));

        Assert.Equal(1, attempts);
        Assert.True(DatabaseFileMigration.IsSharingViolation(error));
    }

    [Fact]
    public void ExecuteWithRetry_does_not_retry_other_io_errors()
    {
        var attempts = 0;

        Assert.Throws<IOException>(() =>
            DatabaseFileMigration.ExecuteWithRetry(TimeSpan.FromSeconds(5), () =>
            {
                attempts++;
                throw new IOException("disk full");
            }, delay: _ => { }));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public void InUseMessage_names_task_manager_and_onedrive_when_the_file_is_there()
    {
        var path = @"C:\Users\dylan\OneDrive\Documents\NickeltownFinance\data\nickeltown.db";
        var message = DatabaseInUseException.BuildMessage(path);

        Assert.Contains("Task Manager", message);
        Assert.Contains("OneDrive", message);
        Assert.Contains(path, message);
    }

    [Fact]
    public void InUseMessage_skips_cloud_advice_for_a_local_file()
    {
        var message = DatabaseInUseException.BuildMessage(
            @"C:\Users\dylan\AppData\Local\NickeltownFinance\data\nickeltown.db");

        Assert.Contains("Task Manager", message);
        Assert.DoesNotContain("Pause syncing", message);
    }

    private static void CreateDatabase(string path, string club)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var database = new LiteDatabase(path);
        var settings = database.GetCollection("settings");
        settings.Insert(new BsonDocument
        {
            ["_id"] = "club",
            ["name"] = club
        });
        database.Checkpoint();
    }

    private static string ReadClub(string path)
    {
        using var database = new LiteDatabase(new ConnectionString
        {
            Filename = path,
            Connection = ConnectionType.Direct
        });
        var club = database.GetCollection("settings").FindById("club");
        return club?["name"].AsString ?? string.Empty;
    }

    private static bool CanRead(string path)
    {
        using var database = new LiteDatabase(new ConnectionString
        {
            Filename = path,
            Connection = ConnectionType.Direct,
            ReadOnly = true
        });
        return database.GetCollectionNames().Contains("settings");
    }

    private static IOException SharingViolation() =>
        new(
            "The process cannot access the file 'nickeltown.db' because it is being used by another process.",
            unchecked((int)0x80070020));

    private sealed class Workspace : IDisposable
    {
        private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nickeltown-db-" + Guid.NewGuid().ToString("N"));

        public Workspace() => Directory.CreateDirectory(_root);

        public string At(params string[] parts) => System.IO.Path.Combine(new[] { _root }.Concat(parts).ToArray());

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
                // LiteDB can keep a handle for a moment on some runtimes.
            }
        }
    }
}
