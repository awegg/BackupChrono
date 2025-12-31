using System.Text.Json.Serialization;

namespace BackupChrono.Infrastructure.Restic;

/// <summary>
/// JSON models for parsing restic command outputs.
/// </summary>
public static class JsonParsers
{
    /// <summary>
    /// Parses JSON output from 'restic snapshots --json' command.
    /// </summary>
    public class SnapshotJson
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("time")]
        public DateTime Time { get; set; }

        [JsonPropertyName("parent")]
        public string? Parent { get; set; }

        [JsonPropertyName("tree")]
        public string Tree { get; set; } = string.Empty;

        [JsonPropertyName("paths")]
        public string[] Paths { get; set; } = Array.Empty<string>();

        [JsonPropertyName("hostname")]
        public string Hostname { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("tags")]
        public string[] Tags { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Parses JSON output from 'restic backup --json' command (summary message).
    /// </summary>
    public class BackupSummaryJson
    {
        [JsonPropertyName("message_type")]
        public string MessageType { get; set; } = string.Empty;

        [JsonPropertyName("files_new")]
        public long FilesNew { get; set; }

        [JsonPropertyName("files_changed")]
        public long FilesChanged { get; set; }

        [JsonPropertyName("files_unmodified")]
        public long FilesUnmodified { get; set; }

        [JsonPropertyName("dirs_new")]
        public long DirsNew { get; set; }

        [JsonPropertyName("dirs_changed")]
        public long DirsChanged { get; set; }

        [JsonPropertyName("dirs_unmodified")]
        public long DirsUnmodified { get; set; }

        [JsonPropertyName("data_blobs")]
        public long DataBlobs { get; set; }

        [JsonPropertyName("tree_blobs")]
        public long TreeBlobs { get; set; }

        [JsonPropertyName("data_added")]
        public long DataAdded { get; set; }

        [JsonPropertyName("total_files_processed")]
        public long TotalFilesProcessed { get; set; }

        [JsonPropertyName("total_bytes_processed")]
        public long TotalBytesProcessed { get; set; }

        [JsonPropertyName("total_duration")]
        public double TotalDuration { get; set; }

        [JsonPropertyName("snapshot_id")]
        public string SnapshotId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Parses JSON output from 'restic backup --json' command (status messages).
    /// </summary>
    public class BackupStatusJson
    {
        [JsonPropertyName("message_type")]
        public string MessageType { get; set; } = string.Empty;

        [JsonPropertyName("percent_done")]
        public double PercentDone { get; set; }

        [JsonPropertyName("total_files")]
        public long TotalFiles { get; set; }

        [JsonPropertyName("files_done")]
        public long FilesDone { get; set; }

        [JsonPropertyName("total_bytes")]
        public long TotalBytes { get; set; }

        [JsonPropertyName("bytes_done")]
        public long BytesDone { get; set; }

        [JsonPropertyName("current_files")]
        public string[] CurrentFiles { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Parses JSON output from 'restic stats --json' command.
    /// </summary>
    public class StatsJson
    {
        [JsonPropertyName("total_size")]
        public long TotalSize { get; set; }

        [JsonPropertyName("total_file_count")]
        public long TotalFileCount { get; set; }

        [JsonPropertyName("total_blob_count")]
        public long TotalBlobCount { get; set; }

        [JsonPropertyName("snapshots_count")]
        public long SnapshotsCount { get; set; }
    }

    /// <summary>
    /// Parses JSON output from 'restic ls --json' command.
    /// </summary>
    public class LsJson
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("uid")]
        public uint Uid { get; set; }

        [JsonPropertyName("gid")]
        public uint Gid { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("mode")]
        public uint Mode { get; set; }

        [JsonPropertyName("mtime")]
        public DateTime Mtime { get; set; }

        [JsonPropertyName("atime")]
        public DateTime Atime { get; set; }

        [JsonPropertyName("ctime")]
        public DateTime Ctime { get; set; }

        [JsonPropertyName("struct_type")]
        public string StructType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Parses JSON output from 'restic restore --json' command.
    /// </summary>
    public class RestoreStatusJson
    {
        [JsonPropertyName("message_type")]
        public string MessageType { get; set; } = string.Empty;

        [JsonPropertyName("percent_done")]
        public double PercentDone { get; set; }

        [JsonPropertyName("total_files")]
        public long TotalFiles { get; set; }

        [JsonPropertyName("files_restored")]
        public long FilesRestored { get; set; }

        [JsonPropertyName("total_bytes")]
        public long TotalBytes { get; set; }

        [JsonPropertyName("bytes_restored")]
        public long BytesRestored { get; set; }
    }

    /// <summary>
    /// Parses JSON output from 'restic forget --json' command.
    /// </summary>
    public class ForgetResultJson
    {
        [JsonPropertyName("keep")]
        public SnapshotJson[] Keep { get; set; } = Array.Empty<SnapshotJson>();

        [JsonPropertyName("remove")]
        public SnapshotJson[] Remove { get; set; } = Array.Empty<SnapshotJson>();
    }

    /// <summary>
    /// Parses JSON output from 'restic check --json' command.
    /// </summary>
    public class CheckResultJson
    {
        [JsonPropertyName("message_type")]
        public string MessageType { get; set; } = string.Empty;

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("check")]
        public string Check { get; set; } = string.Empty;
    }
}
