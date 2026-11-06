namespace NickeltownFinance.Core.DTOs;

public class ClubCloudSyncPreferences
{
    public bool Enabled { get; set; }

    public string ServerUrl { get; set; } = string.Empty;

    public string ClubId { get; set; } = string.Empty;

    public string SyncCode { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public long LastPulledRevision { get; set; }

    public long LastPushedRevision { get; set; }
}

public class ClubCloudRemoteManifest
{
    public long Revision { get; set; }

    public DateTime WrittenUtc { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string MachineName { get; set; } = string.Empty;

    public long PackageBytes { get; set; }
}

public class ClubCloudSyncStatus
{
    public bool Enabled { get; set; }

    public string ServerUrl { get; set; } = string.Empty;

    public string ClubId { get; set; } = string.Empty;

    public bool HasSyncCode { get; set; }

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(ServerUrl)
        && !string.IsNullOrWhiteSpace(ClubId)
        && HasSyncCode;

    public long LocalRevision { get; set; }

    public ClubCloudRemoteManifest? Remote { get; set; }

    public bool NewerRemoteAvailable { get; set; }

    public string StatusSummary { get; set; } = string.Empty;

    public string? LastError { get; set; }
}

public class ClubCloudSyncResult
{
    public bool Success { get; set; }

    public bool Skipped { get; set; }

    public bool Conflict { get; set; }

    public bool RestartRequired { get; set; }

    public long? Revision { get; set; }

    public string? Message { get; set; }
}
