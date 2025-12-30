# Data Model: BackupChrono

**Feature**: Central Pull Backup System  
**Branch**: `001-backup-system`  
**Date**: 2025-12-30  
**Phase**: 1 - Design

## Entity Relationship Diagram

```
Global Configuration
  ↓ (inherits from)
Device ──────┬────── Schedule
  ↓ (has)    ├────── RetentionPolicy
  ↓          └────── IncludeExcludeRules
Share ───────┬───── ScheduleOverride?
  ↓ (has)    ├───── RetentionPolicyOverride?
  ↓          └───── IncludeExcludeRulesOverride?
BackupJob
  ↓ (creates)
Backup ────────── DataBlock (many-to-many via deduplication)

ConfigurationCommit (tracks all changes to Device/Share/Global)
Repository (contains Backups and DataBlocks)
```

## Core Entities

### Device

Represents a physical or virtual machine being backed up (NAS, server, workstation).

**Fields**:
- `Id`: `Guid` (primary key, generated)
- `Name`: `string` (required, unique, 1-100 chars) - Display name (e.g., "office-nas", "web-server-1")
- `Protocol`: `enum ProtocolType` (required) - SMB, SSH, Rsync
- `Host`: `string` (required, 1-255 chars) - IP address or hostname
- `Port`: `int?` (optional) - Protocol-specific port (default: SMB=445, SSH=22, Rsync=873)
- `Username`: `string` (required, 1-100 chars) - Authentication username
- `Password`: `string` (required, encrypted at rest) - Authentication password/credential
- `WakeOnLanEnabled`: `bool` (default: false)
- `WakeOnLanMacAddress`: `string?` (optional, MAC address format) - For WOL
- `Schedule`: `Schedule?` (optional) - Device-level schedule (inherits from global if null)
- `RetentionPolicy`: `RetentionPolicy?` (optional) - Device-level retention (inherits from global if null)
- `IncludeExcludeRules`: `IncludeExcludeRules?` (optional) - Device-level patterns
- `CreatedAt`: `DateTime` (auto-generated)
- `UpdatedAt`: `DateTime` (auto-updated)

**Relationships**:
- One-to-many: Device → Shares
- One-to-many: Device → Backups
- One-to-many: Device → BackupJobs

**Validation Rules**:
- `Name` must be DNS-compatible (alphanumeric, hyphens, no spaces)
- `Host` must be valid IP address or hostname
- `Port` must be 1-65535 if specified
- `WakeOnLanMacAddress` required if `WakeOnLanEnabled` is true
- Password stored encrypted in Git configuration (encrypted with repository passphrase)

**Storage**:
- YAML file: `config/devices/{device-name}.yaml`
- Example: `config/devices/office-nas.yaml`

---

### Share

Represents a specific path or mount point on a device to be backed up.

**Fields**:
- `Id`: `Guid` (primary key, generated)
- `DeviceId`: `Guid` (required, foreign key → Device)
- `Name`: `string` (required, 1-100 chars) - Display name (e.g., "shared", "backup", "var-www")
- `Path`: `string` (required, 1-500 chars) - Share path (e.g., "/data", "\\share", "C:\Users")
- `Enabled`: `bool` (default: true) - Whether this share is actively backed up
- `Schedule`: `Schedule?` (optional) - Share-level override (inherits from device if null)
- `RetentionPolicy`: `RetentionPolicy?` (optional) - Share-level override
- `IncludeExcludeRules`: `IncludeExcludeRules?` (optional) - Share-level override
- `CreatedAt`: `DateTime` (auto-generated)
- `UpdatedAt`: `DateTime` (auto-updated)

**Relationships**:
- Many-to-one: Share → Device
- One-to-many: Share → BackupJobs

**Validation Rules**:
- `Name` must be unique within a device
- `Path` format validated based on device protocol (UNC for SMB, Unix paths for SSH/Rsync)
- Configuration cascade: Share overrides > Device overrides > Global defaults

**Storage**:
- YAML file: `config/shares/{device-name}/{share-name}.yaml`
- Example: `config/shares/office-nas/shared.yaml`

---

### Backup

Represents the state of a device at a specific point in time, containing backups of all configured shares.

**Fields**:
- `Id`: `string` (unique backup identifier, e.g., "a1b2c3d4")
- `DeviceId`: `Guid` (required, foreign key → Device)
- `DeviceName`: `string` (denormalized for query performance)
- `Timestamp`: `DateTime` (required, UTC)
- `Status`: `enum BackupStatus` (required) - Success, Partial, Failed
- `SharesPaths`: `Dictionary<string, string>` - Share names → paths included in this backup
- `FilesNew`: `int` - Number of new files in this backup
- `FilesChanged`: `int` - Number of changed files
- `FilesUnmodified`: `int` - Number of unchanged files
- `DataAdded`: `long` - Bytes of new data added
- `DataProcessed`: `long` - Total bytes processed
- `Duration`: `TimeSpan` - Backup duration
- `ErrorMessage`: `string?` (optional) - Error details if status is Failed/Partial
- `CreatedByJobId`: `Guid?` (optional) - Backup job that created this backup

**Relationships**:
- Many-to-one: Backup → Device
- Many-to-many: Backup ↔ DataBlock (via deduplication)
- One-to-one: Backup ← BackupJob (created by)

**Storage**:
- Managed by backup engine in repository
- Cached in-memory for UI performance (refreshed on demand)
- Immutable once created

---

### DataBlock

Represents a deduplicated chunk of file data (managed by restic).

**Fields**:
- `Hash`: `string` (SHA-256 hash, primary key)
- `Size`: `long` (bytes)
- `RefCount`: `int` - Number of backups referencing this block

**Relationships**:
- Many-to-many: DataBlock ↔ Backup

**Storage**:
- Managed entirely by backup engine repository
- Not directly exposed in BackupChrono domain model
- Referenced here for conceptual completeness

---

### BackupJob

Represents a scheduled or on-demand execution of a backup operation.

**Fields**:
- `Id`: `Guid` (primary key, generated)
- `DeviceId`: `Guid` (required, foreign key → Device)
- `ShareId`: `Guid?` (optional, foreign key → Share) - Null if device-level backup
- `Type`: `enum BackupJobType` (required) - Scheduled, Manual, Retry
- `Status`: `enum BackupJobStatus` (required) - Pending, Running, Success, Failed, Cancelled
- `StartedAt`: `DateTime?` (UTC)
- `CompletedAt`: `DateTime?` (UTC)
- `BackupId`: `string?` (backup identifier if successful)
- `FilesProcessed`: `int` (default: 0)
- `BytesTransferred`: `long` (default: 0)
- `ErrorMessage`: `string?` (optional)
- `RetryAttempt`: `int` (default: 0) - For exponential backoff tracking
- `NextRetryAt`: `DateTime?` (optional) - Scheduled retry time

**Relationships**:
- Many-to-one: BackupJob → Device
- Many-to-one: BackupJob → Share (optional)
- One-to-one: BackupJob → Backup (if successful)

**Storage**:
- In-memory job queue (Quartz.NET)
- Historical jobs logged to file: `logs/backup-jobs.jsonl` (JSON Lines format)

---

## Value Objects

### Schedule

Defines when backup jobs should run.

**Fields**:
- `CronExpression`: `string` (required) - Cron format (e.g., "0 2 * * *" for 2 AM daily)
- `TimeWindowStart`: `TimeOnly?` (optional) - Backup window start (e.g., "02:00")
- `TimeWindowEnd`: `TimeOnly?` (optional) - Backup window end (e.g., "06:00")

**Validation Rules**:
- `CronExpression` must be valid cron syntax (validated by Quartz.NET)
- `TimeWindowEnd` must be after `TimeWindowStart`
- If no time window specified, backups can run anytime

**Default** (Global):
```yaml
schedule:
  cron_expression: "0 2 * * *"  # 2 AM daily
  time_window_start: "02:00"
  time_window_end: "06:00"
```

---

### RetentionPolicy

Defines how long backups should be kept.

**Fields**:
- `KeepLatest`: `int` (default: 7) - Keep last N backups regardless of age
- `KeepDaily`: `int` (default: 7) - Keep one backup per day for N days
- `KeepWeekly`: `int` (default: 4) - Keep one backup per week for N weeks
- `KeepMonthly`: `int` (default: 12) - Keep one backup per month for N months
- `KeepYearly`: `int` (default: 3) - Keep one backup per year for N years

**Validation Rules**:
- All values must be >= 0
- At least one value must be > 0

**Default** (Global):
```yaml
retention_policy:
  keep_latest: 7
  keep_daily: 7
  keep_weekly: 4
  keep_monthly: 12
  keep_yearly: 3
```

**Backup Engine Integration**:
```bash
# Apply retention policy
forget --keep-last 7 --keep-daily 7 --keep-weekly 4 --keep-monthly 12 --keep-yearly 3 --prune
```

---

### IncludeExcludeRules

Defines which files and directories to include or exclude from backups.

**Fields**:
- `ExcludePatterns`: `string[]` (optional) - Glob patterns (e.g., `["*.tmp", "node_modules/"]`)
- `ExcludeRegex`: `string[]` (optional) - Regex patterns for exclusion
- `IncludeOnlyRegex`: `string[]` (optional) - Regex patterns for inclusion-only (BackupFilesOnly)
- `ExcludeIfPresent`: `string[]` (optional) - File markers to exclude directories (e.g., `[".nobackup"]`)

**Validation Rules**:
- Regex patterns must be valid .NET regex syntax
- Glob patterns validated on syntax
- Cannot specify both `ExcludeRegex` and `IncludeOnlyRegex` (conflicting strategies)

**Configuration Cascade**:
- Global patterns apply to all devices/shares
- Device patterns merge with global (union)
- Share patterns override device and global (most specific wins)

**Default** (Global):
```yaml
include_exclude_rules:
  exclude_patterns:
    - "*.tmp"
    - "*.temp"
    - "Thumbs.db"
    - ".DS_Store"
    - "$RECYCLE.BIN/"
  exclude_if_present:
    - ".nobackup"
```

**Restic Integration**:
```bash
restic backup /path --exclude-file=exclude.txt --exclude-if-present=.nobackup
```

---

### ConfigurationCommit

Represents a version-controlled change to system configuration stored in Git.

**Fields**:
- `Hash`: `string` (Git commit SHA)
- `Timestamp`: `DateTime` (commit timestamp)
- `Author`: `string` (commit author name)
- `Email`: `string` (commit author email)
- `Message`: `string` (commit message)
- `FilesChanged`: `string[]` (list of changed file paths)

**Storage**:
- Git repository metadata
- Queried via LibGit2Sharp: `repo.Commits`

**Usage**:
- FR-057: View configuration change history
- FR-058: View diffs between versions
- FR-059: Rollback to previous configuration

---

## Service Interfaces

### ResticService

Main service for interacting with restic backup engine (hard-coded integration).

**Methods**:
```csharp
public interface IResticService
{
    // Repository management
    Task<bool> InitializeRepository(string repositoryPath, string password);
    Task<bool> RepositoryExists(string repositoryPath);
    Task<RepositoryStats> GetStats(string repositoryPath);
    Task VerifyIntegrity(string repositoryPath);
    Task Prune(string repositoryPath);
    
    // Backup operations
    Task<Backup> CreateBackup(Device device, Share share, string sourcePath, IncludeExcludeRules rules);
    Task<BackupProgress> GetBackupProgress(string jobId);
    Task CancelBackup(string jobId);
    
    // Backup management
    Task<IEnumerable<Backup>> ListBackups(string deviceName = null);
    Task<Backup> GetBackup(string backupId);
    Task<IEnumerable<FileEntry>> BrowseBackup(string backupId, string path = "/");
    Task<IEnumerable<FileVersion>> GetFileHistory(string deviceName, string filePath);
    
    // Retention
    Task ApplyRetentionPolicy(string deviceName, RetentionPolicy policy);
    
    // Restore
    Task RestoreBackup(string backupId, string targetPath, string[] includePaths = null);
    Task<RestoreProgress> GetRestoreProgress(string restoreId);
}
```

---

### IProtocolPlugin

Plugin interface for extensible source protocols (SMB, SSH, Rsync, NFS, etc.).

**Methods**:
```csharp
public interface IProtocolPlugin
{
    string ProtocolName { get; }  // "SMB", "SSH", "Rsync", "NFS"
    
    // Connection management
    Task<bool> TestConnection(Device device);
    
    // Share mounting (returns local path for restic to backup)
    Task<string> MountShare(Device device, Share share);
    Task UnmountShare(string mountPath);
    
    // Wake-on-LAN support
    Task WakeDevice(Device device);
    
    // Protocol-specific capabilities
    bool SupportsWakeOnLan { get; }
    bool RequiresAuthentication { get; }
}
```

**Built-in Implementations**:
- `SmbProtocolPlugin` - SMBLibrary for user-space SMB access
- `SshProtocolPlugin` - SSH.NET for SFTP mounting
- `RsyncProtocolPlugin` - Spawns rsync process for sync-to-local, then restic backs up

---

## Supporting Types

### Enums

```csharp
public enum ProtocolType
{
    SMB,
    SSH,
    Rsync
}

public enum BackupJobType
{
    Scheduled,
    Manual,
    Retry
}

public enum BackupJobStatus
{
    Pending,
    Running,
    Success,
    Failed,
    Cancelled
}

public enum BackupStatus
{
    Success,
    Partial,  // Some shares failed
    Failed
}
```

### DTOs (Data Transfer Objects)

```csharp
public class BackupProgress
{
    public string JobId { get; set; }
    public int FilesProcessed { get; set; }
    public long BytesTransferred { get; set; }
    public double PercentComplete { get; set; }
    public string CurrentFile { get; set; }
}

public class RestoreProgress
{
    public string RestoreId { get; set; }
    public int FilesRestored { get; set; }
    public long BytesRestored { get; set; }
    public double PercentComplete { get; set; }
}

public class RepositoryStats
{
    public long TotalSize { get; set; }
    public long UniqueSize { get; set; }
    public long DeduplicationSavings => TotalSize - UniqueSize;
    public int BackupCount { get; set; }
    public int FileCount { get; set; }
}

public class FileEntry
{
    public string Name { get; set; }
    public string Path { get; set; }
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string Permissions { get; set; }
}

public class FileVersion
{
    public string BackupId { get; set; }
    public DateTime Timestamp { get; set; }
    public long Size { get; set; }
    public string Hash { get; set; }
}
```

---

## Configuration File Examples

### Global Configuration
**Path**: `config/global.yaml`

```yaml
# Global defaults - inherited by all devices and shares
schedule:
  cron_expression: "0 2 * * *"
  time_window_start: "02:00"
  time_window_end: "06:00"

retention_policy:
  keep_latest: 7
  keep_daily: 7
  keep_weekly: 4
  keep_monthly: 12
  keep_yearly: 3

include_exclude_rules:
  exclude_patterns:
    - "*.tmp"
    - "*.temp"
    - "Thumbs.db"
    - ".DS_Store"
    - "$RECYCLE.BIN/"
  exclude_if_present:
    - ".nobackup"

repository:
  path: "/repository"
  password_env: "RESTIC_PASSWORD"
```

### Device Configuration
**Path**: `config/devices/office-nas.yaml`

```yaml
id: "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
name: "office-nas"
protocol: "SMB"
host: "192.168.1.100"
port: 445
username: "backup-user"
password: "<encrypted>"  # Encrypted with repository passphrase

wake_on_lan_enabled: true
wake_on_lan_mac_address: "00:11:22:33:44:55"

# Device-level overrides (optional)
schedule:
  cron_expression: "0 3 * * *"  # 3 AM instead of global 2 AM

retention_policy:
  keep_latest: 14  # Keep 14 instead of global 7

created_at: "2025-12-30T10:00:00Z"
updated_at: "2025-12-30T12:30:00Z"
```

### Share Configuration
**Path**: `config/shares/office-nas/shared.yaml`

```yaml
id: "b2c3d4e5-f6a7-8901-bcde-f12345678901"
device_id: "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
name: "shared"
path: "/shared"
enabled: true

# Share-level overrides (optional)
include_exclude_rules:
  exclude_patterns:
    - "*.iso"  # Exclude ISO files for this share only
    - "downloads/"

created_at: "2025-12-30T10:05:00Z"
updated_at: "2025-12-30T10:05:00Z"
```

---

## Data Flow Examples

### Backup Flow

```
1. Quartz.NET scheduler triggers backup job
   ↓
2. BackupOrchestrator creates BackupJob (status: Pending)
   ↓
3. Load Device and Share configurations (with cascading)
   ↓
4. Get IProtocolPlugin for device.Protocol
   ↓
5. ProtocolPlugin.TestConnection(device)
   ↓
6. If WakeOnLanEnabled: ProtocolPlugin.WakeDevice(device)
   ↓
7. ProtocolPlugin.MountShare(device, share) → returns local path
   ↓
8. ResticService.CreateBackup(device, share, localPath, rules)
   ↓
9. Restic process spawns, JSON progress streamed via stdout
   ↓
10. SignalR hub broadcasts progress to connected clients
   ↓
11. Backup completes → Backup record created
   ↓
12. ProtocolPlugin.UnmountShare(localPath)
   ↓
13. ResticService.ApplyRetentionPolicy(device.Name, retentionPolicy)
   ↓
14. BackupJob updated (status: Success, backupId)
   ↓
15. Email notification sent (if configured)
```

### Restore Flow

```
1. User selects device → backup → share → files in UI
   ↓
2. API receives restore request (backupId, targetPath, filePaths)
   ↓
3. ResticService.RestoreBackup(backupId, targetPath, filePaths)
   ↓
4. Backup engine process spawns, extracts files from repository
   ↓
5. SignalR hub broadcasts restore progress
   ↓
6. Files written to targetPath
   ↓
7. Restore complete, user notified
```

### Configuration Change Flow

```
1. User updates device settings in web UI
   ↓
2. API receives PUT /api/devices/{id}
   ↓
3. DeviceService validates and updates device
   ↓
4. GitConfigService serializes to YAML
   ↓
5. Write to config/devices/{device-name}.yaml
   ↓
6. LibGit2Sharp: git add, git commit -m "Updated device: {name}"
   ↓
7. Configuration history updated (queryable via git log)
```

---

## Phase 1 Completion Notes

### Entities Defined
✅ 10 core entities (Device, Share, Backup, DataBlock, BackupJob, etc.)  
✅ 4 value objects (Schedule, RetentionPolicy, IncludeExcludeRules, ConfigurationCommit)  
✅ 1 configuration tracking entity (ConfigurationCommit)

### Service Interfaces
✅ ResticService (hard-coded backup engine integration)  
✅ IProtocolPlugin (extensible protocol support)

### Configuration Model
✅ Global → Device → Share cascade hierarchy  
✅ YAML-based, Git-versioned configuration  
✅ Encryption for sensitive data (credentials)

### Restic Integration Points
✅ All restic operations mapped to service methods  
✅ JSON output parsing for backups, stats, progress  
✅ Retention policy mapping to restic forget command  
✅ Protocol plugin staging + restic backup pattern

**Next Phase**: API Contracts (OpenAPI specs) and Quickstart Guide
