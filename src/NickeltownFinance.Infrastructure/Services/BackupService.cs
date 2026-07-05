using System.IO.Compression;
using LiteDB;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Services;

public class BackupService : IBackupService
{
    private readonly ISettingsService _settingsService;
    private readonly IDatabaseShutdownService _databaseShutdown;
    private readonly LiteDbContext _dbContext;

    public BackupService(
        ISettingsService settingsService,
        IDatabaseShutdownService databaseShutdown,
        LiteDbContext dbContext)
    {
        _settingsService = settingsService;
        _databaseShutdown = databaseShutdown;
        _dbContext = dbContext;
    }

    public Task<string> CreateBackupAsync(bool isManual = false)
    {
        Directory.CreateDirectory(_settingsService.BackupFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.DatabasePath)!);
        Directory.CreateDirectory(AppPaths.FilesRoot);

        var prefix = isManual ? "manual" : "auto";
        var fileName = $"nickeltown_{prefix}_{DateTime.Now:yyyy-MM-dd_HHmmss}.zip";
        var destPath = Path.Combine(_settingsService.BackupFolder, fileName);

        if (File.Exists(destPath))
            File.Delete(destPath);

        _dbContext.Checkpoint();

        using (var archive = ZipFile.Open(destPath, ZipArchiveMode.Create))
        {
            if (File.Exists(AppPaths.DatabasePath))
                AddDatabaseEntry(archive, AppPaths.DatabasePath, "data/nickeltown.db");

            AddDirectory(archive, AppPaths.FilesRoot, "files");
        }

        PruneOldBackups(_settingsService.BackupFolder, 30);
        return Task.FromResult(destPath);
    }



    public void ValidateBackupFile(string backupFilePath)

    {

        if (!File.Exists(backupFilePath))

            throw new FileNotFoundException("Backup file not found.", backupFilePath);



        var info = new FileInfo(backupFilePath);

        if (info.Length == 0)

            throw new InvalidOperationException("Backup file is empty.");



        var ext = Path.GetExtension(backupFilePath).ToLowerInvariant();

        if (ext == ".db")

        {

            ValidateLiteDbFile(backupFilePath);

            return;

        }



        if (ext != ".zip")

            throw new InvalidOperationException("Unsupported backup format. Use a .zip or legacy .db file.");



        try

        {

            using var archive = ZipFile.OpenRead(backupFilePath);

            var dbEntry = archive.Entries.FirstOrDefault(e =>

                e.FullName.Equals("data/nickeltown.db", StringComparison.OrdinalIgnoreCase)

                || e.Name.Equals("nickeltown.db", StringComparison.OrdinalIgnoreCase));



            if (dbEntry is null || dbEntry.Length == 0)

                throw new InvalidOperationException("Backup does not contain a database file.");

        }

        catch (InvalidDataException ex)

        {

            throw new InvalidOperationException("Backup file is corrupt or not a valid zip archive.", ex);

        }

    }



    public Task RestoreBackupAsync(string backupFilePath)

    {

        ValidateBackupFile(backupFilePath);



        // Release the database file before overwriting.

        _databaseShutdown.Close();



        var ext = Path.GetExtension(backupFilePath).ToLowerInvariant();

        if (ext == ".db")

        {

            Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.DatabasePath)!);

            File.Copy(backupFilePath, AppPaths.DatabasePath, overwrite: true);

            return Task.CompletedTask;

        }



        var tempDir = Path.Combine(Path.GetTempPath(), $"nickeltown_restore_{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempDir);



        try

        {

            ZipFile.ExtractToDirectory(backupFilePath, tempDir, overwriteFiles: true);



            var dbSource = Path.Combine(tempDir, "data", "nickeltown.db");

            if (!File.Exists(dbSource))

            {

                dbSource = Directory.GetFiles(tempDir, "nickeltown.db", SearchOption.AllDirectories).FirstOrDefault()

                           ?? throw new InvalidOperationException("Backup does not contain a database.");

            }



            ValidateLiteDbFile(dbSource);



            Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.DatabasePath)!);

            File.Copy(dbSource, AppPaths.DatabasePath, overwrite: true);



            var filesSource = Path.Combine(tempDir, "files");

            if (Directory.Exists(filesSource))

            {

                if (Directory.Exists(AppPaths.FilesRoot))

                    Directory.Delete(AppPaths.FilesRoot, recursive: true);



                CopyDirectory(filesSource, AppPaths.FilesRoot);

            }

        }

        finally

        {

            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }

        }



        return Task.CompletedTask;

    }



    public Task BackupOnShutdownAsync() => CreateBackupAsync(isManual: false);



    public Task<IReadOnlyList<BackupInfo>> GetBackupHistoryAsync()

    {

        var folder = _settingsService.BackupFolder;

        Directory.CreateDirectory(folder);



        if (!Directory.Exists(folder))

            return Task.FromResult<IReadOnlyList<BackupInfo>>([]);



        var history = Directory.GetFiles(folder, "nickeltown_*.*")

            .Where(path =>

            {

                var fileExt = Path.GetExtension(path).ToLowerInvariant();

                return fileExt is ".zip" or ".db";

            })

            .Select(path =>

            {

                var info = new FileInfo(path);

                return new BackupInfo

                {

                    FilePath = path,

                    FileName = info.Name,

                    CreatedAt = info.CreationTime,

                    SizeBytes = info.Length

                };

            })

            .OrderByDescending(x => x.CreatedAt)

            .ToList();



        return Task.FromResult<IReadOnlyList<BackupInfo>>(history);

    }



    private static void ValidateLiteDbFile(string path)

    {

        try

        {

            using var db = new LiteDatabase($"Filename={path};Connection=shared;ReadOnly=true");

            _ = db.GetCollectionNames();

        }

        catch (Exception ex)

        {

            throw new InvalidOperationException("Backup database file appears corrupt.", ex);

        }

    }



    private static void AddDatabaseEntry(ZipArchive archive, string dbPath, string entryName)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var fileStream = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fileStream.CopyTo(entryStream);
    }

    private static void AddDirectory(ZipArchive archive, string sourceDir, string entryPrefix)

    {

        if (!Directory.Exists(sourceDir)) return;



        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))

        {

            var relative = Path.GetRelativePath(sourceDir, file).Replace('\\', '/');

            archive.CreateEntryFromFile(file, $"{entryPrefix}/{relative}");

        }

    }



    private static void CopyDirectory(string sourceDir, string destDir)

    {

        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))

        {

            var relative = Path.GetRelativePath(sourceDir, file);

            var dest = Path.Combine(destDir, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            File.Copy(file, dest, overwrite: true);

        }

    }



    private static void PruneOldBackups(string folder, int keep)

    {

        var files = Directory.GetFiles(folder, "nickeltown_*.*")

            .Where(path =>

            {

                var fileExt = Path.GetExtension(path).ToLowerInvariant();

                return fileExt is ".zip" or ".db";

            })

            .OrderByDescending(File.GetCreationTime)

            .Skip(keep)

            .ToList();



        foreach (var file in files)

        {

            try { File.Delete(file); }

            catch { /* ignore locked files */ }

        }

    }

}


