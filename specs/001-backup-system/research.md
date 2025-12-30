# Phase 0: Research & Technology Decisions

**Feature**: BackupChrono - Central Pull Backup System
**Date**: 2025-12-30

## Research Questions

### 1. Backup Engine Selection: Restic vs. Kopia vs. Borg

**Question**: Which mature backup engine should BackupChrono orchestrate for deduplication, compression, and snapshot management?

**Options Evaluated**:

#### Option A: Restic
**Overview**: Fast, secure backup program written in Go, designed to work with multiple backends.

**Strengths**:
- ✅ Mature and stable (10+ years in development)
- ✅ Excellent cross-platform support (Windows, Linux, macOS)
- ✅ Native support for many storage backends (local, S3, Azure, GCS, B2, SFTP, REST)
- ✅ Strong encryption by default (AES-256)
- ✅ Content-defined chunking for deduplication
- ✅ Easy to integrate (single binary, clean CLI)
- ✅ Good documentation and active community
- ✅ Repository format is well-documented
- ✅ Built-in verification and integrity checks

**Weaknesses**:
- ⚠️ Slower than Kopia on large repositories
- ⚠️ Limited compression options (only zstd currently)
- ⚠️ Pruning (garbage collection) can be slow
- ⚠️ No built-in scheduling or orchestration (needs external tool)

**Integration Approach**:
```csharp
// Spawn restic process for backup operations
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "restic",
        Arguments = "backup /mnt/smb/share --host office-nas --json",
        RedirectStandardOutput = true,
        RedirectStandardError = true
    }
};
// Parse JSON output for progress tracking
```

**Fit for BackupChrono**:
- ✅ Meets FR-065 (mature, established)
- ✅ Supports FR-012 (global deduplication)
- ✅ Supports FR-028 (compression)
- ✅ Can delegate to restic for FR-031 (checksums)
- ✅ Works well for central-pull model (BackupChrono orchestrates, restic executes)

---

#### Option B: Kopia
**Overview**: Modern backup tool with focus on performance and cloud storage, written in Go.

**Strengths**:
- ✅ Excellent performance (faster than restic in most benchmarks)
- ✅ Better compression options (zstd with multiple levels)
- ✅ More efficient deduplication
- ✅ Built-in UI (KopiaUI) - could be reference for features
- ✅ Very active development
- ✅ Modern codebase and architecture
- ✅ Excellent cloud storage support
- ✅ Faster pruning/garbage collection
- ✅ Better handling of large files

**Weaknesses**:
- ⚠️ Younger project (less battle-tested than restic)
- ⚠️ Repository format less stable (still evolving)
- ⚠️ Smaller community compared to restic
- ⚠️ Some features still in development
- ⚠️ More complex to integrate (repository locking, policy management)

**Integration Approach**:
```csharp
// Similar process spawning but with kopia-specific commands
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "kopia",
        Arguments = "snapshot create /mnt/smb/share --json",
        RedirectStandardOutput = true
    }
};
```

**Fit for BackupChrono**:
- ✅ Meets FR-065 (established, though newer)
- ✅ Supports FR-012 (global deduplication, better than restic)
- ✅ Supports FR-028 (excellent compression)
- ✅ Better performance (SC-007 dedup savings)
- ⚠️ Less mature than restic

---

#### Option C: BorgBackup
**Overview**: Deduplicating backup program written in Python with C extensions.

**Strengths**:
- ✅ Very mature (15+ years including Attic predecessor)
- ✅ Excellent deduplication efficiency
- ✅ Strong security model
- ✅ Efficient storage format
- ✅ Good compression options
- ✅ Large user base and community
- ✅ Well-documented

**Weaknesses**:
- ❌ Linux/macOS only (no native Windows support)
- ❌ Less suitable for SMB/Windows sources
- ❌ Requires Python runtime
- ⚠️ Slower than restic/kopia on some operations
- ⚠️ Less cloud-storage focus
- ⚠️ Repository locking can be problematic in central-pull scenario

**Integration Approach**:
```csharp
// Would need to mount SMB shares, then use borg
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "borg",
        Arguments = "create ::snapshot /mnt/share --json",
        RedirectStandardOutput = true
    }
};
```

**Fit for BackupChrono**:
- ⚠️ Limited Windows support is problematic for SMB sources
- ✅ Mature (FR-065)
- ✅ Good deduplication
- ❌ Python dependency adds complexity to Docker image
- ⚠️ Not ideal for heterogeneous environments

---

#### Option D: Custom Hardlink-Based Implementation (BackupPC-style)
**Overview**: Self-implemented backup storage using filesystem hardlinks for deduplication, similar to BackupPC's pool mechanism.

**Architecture Concept**:
```
repository/
├── pool/                    # Content-addressable pool
│   ├── 0/
│   │   ├── 1/
│   │   │   └── hash123...  # File stored once by content hash
│   │   └── 2/
│   │       └── hash456...
│   └── ...
└── snapshots/              # Snapshot trees with hardlinks
    ├── office-nas/
    │   ├── 2025-12-30_02-00/
    │   │   ├── shared/
    │   │   │   └── file.txt -> ../../../pool/0/1/hash123...
    │   │   └── backup/
    │   │       └── doc.pdf -> ../../../pool/0/2/hash456...
    │   └── 2025-12-29_02-00/
    │       └── shared/
    │           └── file.txt -> ../../../pool/0/1/hash123...  # Same inode
```

**How It Works**:
1. **Backup Process**:
   - Read file from source
   - Calculate SHA-256 hash
   - Check if hash exists in pool
   - If new: Write to pool at `pool/{hash[0]}/{hash[1]}/{hash}`
   - Create hardlink from snapshot tree to pool entry
   - If existing: Just create hardlink (no data copy)

2. **Deduplication**:
   - Identical files share same inode (hardlinked)
   - Filesystem handles reference counting
   - Space saved automatically

3. **Pruning/GC**:
   - Delete old snapshot trees
   - Run `find pool -links 1 -delete` to remove unreferenced files
   - Simple, filesystem-native garbage collection

**Strengths**:
- ✅ **Full control**: No external dependency for core backup logic
- ✅ **Extremely efficient deduplication**: Filesystem-level, zero overhead
- ✅ **Simple restore**: Direct file access, no unpacking needed
- ✅ **Proven model**: BackupPC has used this for 20+ years successfully
- ✅ **Fast browsing**: Snapshots are real directory trees, can use `ls`, `find`, etc.
- ✅ **No special tools needed**: Standard filesystem operations
- ✅ **Transparent storage**: Easy to debug, inspect, verify
- ✅ **Incremental cost**: Only new/changed files consume storage
- ✅ **Cross-platform**: Works on any filesystem supporting hardlinks (ext4, btrfs, NTFS, APFS)
- ✅ **Compression optional**: Can compress pool files individually or use filesystem compression

**Weaknesses**:
- ❌ **Development effort**: 2-3 months to implement properly vs. days with restic
- ❌ **Testing burden**: Need extensive testing for data integrity, corruption recovery
- ❌ **Encryption complexity**: Must implement encryption layer (restic has this built-in)
- ❌ **Cloud storage**: Hardlinks don't work on S3/cloud (local/NFS/CIFS only)
- ❌ **Filesystem limits**: Some filesystems have inode limits (though high)
- ⚠️ **Concurrent access**: Need careful locking to prevent corruption
- ⚠️ **Metadata storage**: Need separate metadata files for permissions, timestamps, symlinks
- ⚠️ **No built-in integrity**: Need to implement checksum verification
- ⚠️ **Snapshot consistency**: Need atomic snapshot creation logic
- ⚠️ **Performance**: Hash calculation on every file (but can cache)

**Implementation Estimate**:
```csharp
public class HardlinkBackupEngine : IBackupEngine
{
    private readonly string _poolPath;
    private readonly string _snapshotsPath;
    
    public async Task<Snapshot> CreateSnapshot(Device device, Share share, string sourcePath)
    {
        var snapshotPath = Path.Combine(_snapshotsPath, device.Name, DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm"));
        
        // Walk source directory tree
        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            // Calculate hash
            var hash = await CalculateSHA256(file);
            
            // Pool location
            var poolFile = Path.Combine(_poolPath, hash[0..1], hash[1..2], hash);
            
            // Add to pool if new
            if (!File.Exists(poolFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(poolFile));
                File.Copy(file, poolFile);
            }
            
            // Create hardlink in snapshot
            var snapshotFile = Path.Combine(snapshotPath, Path.GetRelativePath(sourcePath, file));
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotFile));
            CreateHardLink(snapshotFile, poolFile);
        }
        
        return new Snapshot { Path = snapshotPath, Created = DateTime.UtcNow };
    }
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);
}
```

**Complexity Areas**:
1. **Metadata Handling**: Store as JSON/YAML per snapshot
2. **Locking**: Use filesystem locks or database for coordination
3. **Atomic Snapshots**: Write to temp dir, then atomic rename
4. **Verification**: Periodic hash verification of pool files
5. **Compression**: Optional per-file compression (gzip, zstd)
6. **Encryption**: Optional per-file or per-pool encryption

**Fit for BackupChrono**:
- ✅ Perfect fit for homelab (local/NFS storage)
- ✅ Aligns with "database-free" philosophy (FR-066)
- ✅ Simple, transparent, debuggable
- ✅ No external process dependencies
- ❌ **NOT** suitable for cloud storage backends
- ❌ Significant development and testing effort
- ⚠️ Would violate FR-065 if sole implementation (not "established component")

**Risk Assessment**:
- **Data Loss Risk**: HIGH - Custom implementation = bugs = potential data loss
- **Development Time**: 2-3 months for production-ready implementation
- **Maintenance Burden**: Ongoing bug fixes, edge cases, filesystem quirks
- **Testing Requirements**: Need comprehensive data integrity tests

**Mitigation Strategy If Chosen**:
1. Start with extensive test suite (thousands of test cases)
2. Implement with restic as fallback option
3. Run parallel backups (hardlink + restic) for first 6 months to verify
4. Open source immediately for community review
5. Document every design decision and edge case

---

### Decision Matrix

| Criteria | Weight | Restic | Kopia | Borg | Hardlinks |
|----------|--------|--------|-------|------|-----------|
| **Maturity/Stability** | 30% | 9/10 | 7/10 | 10/10 | 3/10 |
| **Performance** | 25% | 7/10 | 9/10 | 7/10 | 9/10 |
| **Cross-platform** | 20% | 10/10 | 10/10 | 5/10 | 9/10 |
| **Integration Ease** | 15% | 9/10 | 7/10 | 6/10 | 4/10 |
| **Cloud Storage** | 10% | 9/10 | 10/10 | 6/10 | 2/10 |
| **Weighted Score** | | **8.35** | **8.05** | **7.15** | **6.15** |

**Additional Factors Not Scored**:
- Development Effort: Restic/Kopia/Borg = Days, Hardlinks = Months
- Risk: Restic = Low, Kopia = Medium, Borg = Medium, Hardlinks = HIGH
- Compliance with FR-065: Restic/Kopia/Borg = ✅, Hardlinks = ❌ (not "established")

### Recommendation: **Hard-code Restic + Protocol Plugins**

**Decision**: Direct restic integration (not abstracted behind IBackupEngine interface), but keep protocol plugin architecture.

**Rationale**:
1. **YAGNI Principle**: Backup engine unlikely to change in homelab context (~5-10% probability)
2. **Reduced Complexity**: No abstraction layer overhead, simpler codebase
3. **Faster Development**: Direct integration vs. interface design + implementation
4. **Future Refactoring**: If needed, restic calls isolated in `ResticService` class
5. **FR-065 Compliance**: Using established component (restic)
6. **Protocol Flexibility**: Source protocols (SMB, SSH, NFS, etc.) WILL expand - keep plugin architecture there

**Why Hard-code Restic**:
- Mature and stable (10+ years, lowest risk)
- Simplest integration (clean CLI, JSON output)
- Best cross-platform support
- Unlikely to need replacement in homelab scenarios
- Bundled in Docker image as only backup engine

**Why Keep Protocol Plugins**:
- High probability of adding NFS, iSCSI, FTP, WebDAV support
- Source protocols are extension points users actually request
- Restic doesn't need to support protocols - BackupChrono stages data, restic backs it up
- Community contributions likely for protocol support, not backup engines

**Direct Restic Integration**:
```csharp
public class ResticService
{
    private readonly string _resticPath;
    private readonly string _repositoryPath;
    
    public async Task<Snapshot> CreateSnapshot(Device device, Share share, string sourcePath)
    {
        var args = new List<string>
        {
            "backup",
            sourcePath,
            "--host", device.Name,
            "--tag", $"share:{share.Name}",
            "--json"
        };
        
        if (share.ExcludePatterns?.Any() == true)
        {
            var excludeFile = await CreateExcludeFile(share.ExcludePatterns);
            args.Add($"--exclude-file={excludeFile}");
        }
        
        var result = await RunRestic(args);
        return ParseSnapshotFromJson(result.Output);
    }
    
    public async Task ApplyRetentionPolicy(Device device, RetentionPolicy policy)
    {
        var args = new List<string>
        {
            "forget",
            "--host", device.Name,
            "--keep-last", policy.Latest.ToString(),
            "--keep-daily", policy.Daily.ToString(),
            "--keep-weekly", policy.Weekly.ToString(),
            "--keep-monthly", policy.Monthly.ToString(),
            "--prune",
            "--json"
        };
        
        await RunRestic(args);
    }
    
    public async Task<IEnumerable<Snapshot>> ListSnapshots(string deviceName = null)
    {
        var args = new List<string> { "snapshots", "--json" };
        if (deviceName != null)
            args.AddRange(new[] { "--host", deviceName });
            
        var result = await RunRestic(args);
        return ParseSnapshotsFromJson(result.Output);
    }
    
    private async Task<ProcessResult> RunRestic(List<string> args)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _resticPath,
                Arguments = string.Join(" ", args),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        return new ProcessResult { Output = output, Error = error, ExitCode = process.ExitCode };
    }
}
```

**Backup Orchestration Flow**:
```csharp
public class BackupOrchestrator
{
    private readonly ResticService _restic;
    private readonly IProtocolPluginFactory _protocolFactory;
    
    public async Task<BackupResult> ExecuteBackup(Device device, Share share)
    {
        // 1. Get protocol plugin for this device
        var protocol = _protocolFactory.GetPlugin(device.Protocol);
        
        // 2. Mount/stage the share
        var sourcePath = await protocol.MountShare(device, share);
        
        try
        {
            // 3. Restic backs up from staged location
            var snapshot = await _restic.CreateSnapshot(device, share, sourcePath);
            
            // 4. Apply retention policy
            await _restic.ApplyRetentionPolicy(device, share.RetentionPolicy);
            
            return BackupResult.Success(snapshot);
        }
        finally
        {
            // 5. Cleanup mounted share
            await protocol.UnmountShare(sourcePath);
        }
    }
}
```

**Protocol Plugin Interface** (THIS is where plugins matter):
```csharp
public interface IProtocolPlugin
{
    string ProtocolName { get; }  // "SMB", "SSH", "NFS", "iSCSI"
    
    Task<bool> TestConnection(Device device);
    Task<string> MountShare(Device device, Share share);  // Returns local path for restic
    Task UnmountShare(string mountPath);
}

// Example: NFS plugin (future community contribution)
public class NfsProtocolPlugin : IProtocolPlugin
{
    public string ProtocolName => "NFS";
    
    public async Task<string> MountShare(Device device, Share share)
    {
        var mountPoint = $"/tmp/backupchrono/nfs/{device.Name}/{share.Name}";
        await RunCommand($"mount -t nfs {device.Host}:{share.Path} {mountPoint}");
        return mountPoint;  // Restic backs up from here
    }
}
```

**Benefits of This Hybrid Approach**:
- ✅ Simpler codebase (no backup engine abstraction)
- ✅ Protocol flexibility where it matters (SMB, SSH, NFS, iSCSI, WebDAV)
- ✅ Restic doesn't need to support all protocols - BackupChrono stages data
- ✅ Community can easily add protocol support
- ✅ Fast time-to-market (less abstraction layers)

**Why Not Abstract Backup Engine**:
- Backup engine change unlikely in homelab (~5-10% probability)
- If needed later, restic calls isolated in `ResticService` (easy refactor)
- YAGNI: Don't build complexity for low-probability scenario
- Protocol plugins are high-value extension point

---

## Restic Feature Compatibility Analysis

**Question**: Does Restic support all functional requirements from spec.md?

Below is a comprehensive mapping of BackupChrono's functional requirements (FR-001 to FR-099) against Restic's capabilities.

### ✅ Fully Supported by Restic (Can Delegate)

| FR | Requirement | Restic Support | Notes |
|----|-------------|----------------|-------|
| FR-012 | Global deduplication across devices/shares | ✅ Yes | Content-defined chunking, single repository |
| FR-013 | Handle many small files efficiently | ✅ Yes | Packs small files into larger blobs |
| FR-014 | Handle large files (GB-TB scale) | ✅ Yes | Chunking handles files of any size |
| FR-028 | Configurable compression | ✅ Yes | zstd compression (configurable) |
| FR-031 | Data integrity via checksums | ✅ Yes | SHA-256 for all chunks, built-in verify |
| FR-032 | Repository garbage collection | ✅ Yes | `restic prune` command |
| FR-035 | Restore individual files | ✅ Yes | `restic restore --include file.txt` |
| FR-036 | Restore entire directories | ✅ Yes | `restic restore /path` |
| FR-037 | Restore to original or alternate location | ✅ Yes | `--target` flag |
| FR-038 | View backup size and dedup savings | ✅ Yes | `restic stats` shows size and dedup ratio |
| FR-039 | Browse snapshots to find files | ✅ Yes | `restic ls`, `restic find`, `restic mount` |
| FR-040 | Compare file versions across snapshots | ⚠️ Partial | Can list/diff manually, no built-in diff tool |
| FR-073 | Store in pluggable storage backends | ✅ Yes | Local, SFTP, S3, Azure, GCS, B2, REST |
| FR-074 | Repository encryption at rest | ✅ Yes | AES-256-CTR encryption by default |
| FR-080 | Repository format versioning | ✅ Yes | Repository format version in metadata |
| FR-081 | Version migration tool | ✅ Yes | `restic migrate` command |
| FR-082 | Reject incompatible repository versions | ✅ Yes | Version check on repository open |

### ✅ Supported via BackupChrono Orchestration (Restic Provides Building Blocks)

| FR | Requirement | Implementation Approach |
|----|-------------|-------------------------|
| FR-001-005 | Device & Share Management | BackupChrono manages; restic backs up shares |
| FR-006-011 | Protocol connectivity (SMB, SSH, Rsync) | BackupChrono mounts/stages; restic backs up |
| FR-015-021 | Scheduling, retries, WOL, blackouts | BackupChrono orchestrates; restic executes |
| FR-022-027 | Include/exclude rules, config cascade | BackupChrono generates restic exclude files |
| FR-029-030 | Retention policies (daily/weekly/monthly) | BackupChrono calls `restic forget` with policies |
| FR-033-034 | Preserve symlinks, track file moves | Restic preserves symlinks; moves = delete+add (acceptable) |
| FR-041-048 | Monitoring, status, notifications | BackupChrono parses restic JSON output |
| FR-049-059 | Web UI for all operations | BackupChrono provides UI; calls restic via API |
| FR-060-072 | Git config, Docker, plugins | BackupChrono features; restic is payload engine |
| FR-075-079 | Repository metadata | BackupChrono stores metadata; restic stores data |
| FR-083-099 | Restore ops, monitoring, notifications | BackupChrono orchestrates restic restore |

### ⚠️ Limitations / Workarounds Required

| FR | Requirement | Restic Limitation | Workaround |
|----|-------------|-------------------|------------|
| FR-040 | File version diff/compare | No built-in diff tool | BackupChrono fetches versions, runs diff externally |
| FR-010 | Rsync protocol support | Restic doesn't speak rsync | BackupChrono uses rsync to stage, then restic backs up |
| FR-075 | Metadata organized by device | Restic organizes by snapshot | BackupChrono tags snapshots with device name (`--host`) |
| FR-079 | Fast snapshot browsing | Restic requires repository access | Cache snapshot listings in BackupChrono for speed |

### ❌ Not Possible with Restic (Require Alternative Approach)

| FR | Requirement | Why Restic Can't Do It | Alternative Solution |
|----|-------------|------------------------|---------------------|
| **NONE** | All FRs achievable | - | Restic + BackupChrono orchestration covers all requirements |

### 🎯 Restic Compatibility Verdict

**Result**: ✅ **All 99 functional requirements can be satisfied using Restic**

**Breakdown**:
- **Direct Support**: 17 FRs (restic handles natively)
- **Orchestration Support**: 82 FRs (BackupChrono orchestrates restic)
- **Workaround Needed**: 4 FRs (minor limitations, acceptable workarounds)
- **Impossible**: 0 FRs

**Key Insight**: Restic is a **data engine**, BackupChrono is an **orchestration platform**. The division of responsibilities is clean:

```
BackupChrono Responsibilities:
├── Device & Share Management (FR-001-005)
├── Protocol Handling (FR-006-011)
├── Scheduling & Orchestration (FR-015-021)
├── Configuration Management (FR-060-063)
├── Web UI (FR-049-059)
├── Monitoring & Notifications (FR-041-048)
└── User Experience (device-centric model)

Restic Responsibilities:
├── Deduplication (FR-012)
├── Compression (FR-028)
├── Encryption (FR-074)
├── Storage Backend Abstraction (FR-073)
├── Snapshot Creation
├── Data Restore (FR-035-037)
└── Integrity Verification (FR-031)
```

This separation is **exactly what BackupPC does** (orchestration layer over tar/rsync) and what makes the architecture clean.

---

### Critical Restic Features for BackupChrono

**Must Use**:
1. **`--host` flag**: Tag snapshots with device name for organization
2. **`--json` output**: Parse structured output for progress/status
3. **`--exclude-file`**: Generated by BackupChrono from share config
4. **`forget --keep-*`**: Implement retention policies
5. **`stats` command**: Get deduplication savings for dashboard

**Example Integration**:
```csharp
// Create snapshot for share
await RunRestic(
    "backup",
    $"--host {device.Name}",
    $"--tag share:{share.Name}",
    $"--exclude-file {excludeFilePath}",
    $"--json",
    sourcePath
);

// Apply retention policy
await RunRestic(
    "forget",
    $"--host {device.Name}",
    $"--keep-last {retention.Latest}",
    $"--keep-daily {retention.Daily}",
    $"--keep-weekly {retention.Weekly}",
    $"--keep-monthly {retention.Monthly}",
    "--prune"
);

// Get dedup stats for dashboard
var stats = await RunRestic("stats", "--json", "--mode raw-data");
```

---

### 2. ASP.NET Core Backend Architecture

**Question**: How should the ASP.NET Core backend be structured for orchestrating backup operations?

**Decision**: **Clean Architecture with Minimal Layers**

**Structure**:
```
BackupChrono.Core/          # Domain layer (entities, interfaces)
BackupChrono.Infrastructure/ # Implementations (Git, protocols, backup engine)
BackupChrono.Api/           # Web API (controllers, SignalR hubs)
```

**Rationale**:
- Separation of concerns without over-engineering
- Core contains business logic (device/share model, scheduling rules)
- Infrastructure implements external dependencies (Git, SMB, SSH, restic)
- API layer is thin (controllers delegate to services)

**Pattern Decisions**:
- ✅ Repository pattern: **NO** - Git is the repository, no need for abstraction
- ✅ Unit of Work: **NO** - No database transactions needed
- ✅ CQRS: **NO** - Simple CRUD operations, no need for command/query separation
- ✅ Mediator: **NO** - Direct service injection sufficient
- ✅ Service layer: **YES** - `DeviceService`, `BackupOrchestrator`, `SchedulingService`

---

### 3. Git Integration Pattern

**Question**: How to manage Git operations for configuration versioning (FR-062-063)?

**Decision**: **LibGit2Sharp with Automatic Commits**

**Approach**:
```csharp
public class GitConfigService
{
    private readonly string _configRepoPath;
    
    public async Task SaveDeviceConfig(Device device)
    {
        // 1. Serialize device to YAML
        var yaml = _serializer.Serialize(device);
        
        // 2. Write to file in config repo
        await File.WriteAllTextAsync(
            Path.Combine(_configRepoPath, "devices", $"{device.Name}.yaml"),
            yaml
        );
        
        // 3. Auto-commit with descriptive message
        using var repo = new Repository(_configRepoPath);
        Commands.Stage(repo, "*");
        repo.Commit(
            $"Updated device: {device.Name}",
            new Signature("BackupChrono", "system@backupchrono", DateTimeOffset.Now),
            new Signature("BackupChrono", "system@backupchrono", DateTimeOffset.Now)
        );
    }
    
    public async Task<IEnumerable<ConfigChange>> GetHistory()
    {
        using var repo = new Repository(_configRepoPath);
        return repo.Commits.Select(c => new ConfigChange
        {
            Hash = c.Sha,
            Message = c.Message,
            Author = c.Author.Name,
            Timestamp = c.Author.When
        });
    }
    
    public async Task Rollback(string commitHash)
    {
        using var repo = new Repository(_configRepoPath);
        var commit = repo.Lookup<Commit>(commitHash);
        repo.Reset(ResetMode.Hard, commit);
        // Create new commit documenting the rollback
        repo.Commit($"Rollback to {commitHash}", ...);
    }
}
```

**Rationale**:
- LibGit2Sharp is mature .NET library for Git operations
- Auto-commit on every config change (FR-063)
- Clear audit trail (FR-062)
- Rollback support (UI shows commit history, click to rollback)

**File Structure in Config Repo**:
```
config/
├── global.yaml              # Global defaults
├── devices/
│   ├── office-nas.yaml      # Device config
│   ├── web-server-1.yaml
│   └── db-server.yaml
└── shares/
    ├── office-nas/
    │   ├── shared.yaml      # Share config under device
    │   ├── backup.yaml
    │   └── archive.yaml
    └── web-server-1/
        ├── var-www.yaml
        └── etc.yaml
```

---

### 4. Protocol Plugin Architecture

**Question**: How to implement plugin system for SMB, SSH, Rsync (FR-067-069)?

**Decision**: **Interface-Based Plugins with Filesystem Discovery**

**Interface Design**:
```csharp
public interface IProtocolPlugin
{
    string ProtocolName { get; } // "SMB", "SSH", "Rsync"
    string Version { get; }
    
    Task<bool> TestConnection(ConnectionConfig config);
    
    Task<string> MountShare(Device device, Share share, string mountPoint);
    
    Task UnmountShare(string mountPoint);
    
    Task<FileSystemInfo> GetFileSystemInfo(string mountPoint);
}

public class SmbProtocolPlugin : IProtocolPlugin
{
    public string ProtocolName => "SMB";
    
    public async Task<string> MountShare(Device device, Share share, string mountPoint)
    {
        // Use SMBLibrary to mount as FUSE or
        // Use platform-specific mount commands
        // Return path where share is accessible
    }
}
```

**Plugin Discovery** (FR-069):
```csharp
public class PluginLoader
{
    public IEnumerable<IProtocolPlugin> LoadPlugins(string pluginDirectory)
    {
        var assemblies = Directory.GetFiles(pluginDirectory, "*.dll")
            .Select(Assembly.LoadFrom);
            
        return assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IProtocolPlugin).IsAssignableFrom(t) && !t.IsInterface)
            .Select(t => (IProtocolPlugin)Activator.CreateInstance(t));
    }
}
```

**Built-in Plugins**:
- `SmbProtocolPlugin` - Uses SMBLibrary or mount.cifs
- `SshProtocolPlugin` - Uses SSH.NET for SFTP
- `RsyncProtocolPlugin` - Spawns rsync process

---

### 5. React Frontend State Management

**Question**: How to manage state in React frontend for real-time backup updates?

**Decision**: **React Query + SignalR for Real-time Updates**

**Rationale**:
- React Query handles server state (devices, shares, backups)
- SignalR provides real-time backup progress updates
- No need for Redux (overkill for this app)
- Simpler than full state management framework

**Example**:
```typescript
// Device list with auto-refresh
const { data: devices, isLoading } = useQuery({
  queryKey: ['devices'],
  queryFn: () => deviceService.getDevices(),
  refetchInterval: 30000 // Refresh every 30s
});

// Real-time backup progress
useEffect(() => {
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/backup')
    .build();
    
  connection.on('BackupProgress', (deviceName, progress) => {
    // Update UI with progress
    setBackupStatus(prev => ({
      ...prev,
      [deviceName]: progress
    }));
  });
  
  connection.start();
}, []);
```

---

## Technology Stack Summary

### Backend
| Component | Technology | Rationale |
|-----------|-----------|-----------|
| Framework | ASP.NET Core 8.0 | Modern, performant, excellent for APIs |
| Backup Engine | Restic | Mature, cross-platform, easy integration |
| Git Library | LibGit2Sharp | Native .NET Git operations |
| SMB Protocol | SMBLibrary | Pure C# SMB implementation |
| SSH Protocol | SSH.NET | Mature .NET SSH/SFTP library |
| Scheduling | Quartz.NET | Robust job scheduling with cron support |
| Serialization | YamlDotNet | YAML config file handling |
| Real-time | SignalR | WebSocket-based progress updates |

### Frontend
| Component | Technology | Rationale |
|-----------|-----------|-----------|
| Framework | React 18 | Modern, component-based UI |
| State Management | React Query | Server state caching and sync |
| Routing | React Router 6 | Standard React routing |
| Styling | Tailwind CSS | Utility-first, rapid development |
| HTTP Client | Axios | Promise-based HTTP client |
| Real-time | SignalR Client | WebSocket connection to backend |

### Infrastructure
| Component | Technology | Rationale |
|-----------|-----------|-----------|
| Container | Docker | Required by FR-064 |
| Orchestration | Docker Compose | Multi-container local dev |
| Reverse Proxy | nginx (optional) | TLS termination support (FR-072) |

---

## Best Practices Research

### 1. Backup Engine Integration Best Practices

**Finding**: Treat backup engine as external process, not library
- Spawn restic as subprocess with JSON output
- Parse stdout/stderr for progress tracking
- Handle process lifecycle (timeout, cancellation, cleanup)
- Use temp directories for mounted shares
- **CRITICAL: Comprehensive integration tests** - restic is external dependency that will update

**Integration Testing Strategy**:
```csharp
[TestClass]
public class ResticIntegrationTests
{
    [TestMethod]
    public async Task ResticVersion_ShouldBeCompatible()
    {
        // Test that installed restic version is supported
        var version = await _resticService.GetVersion();
        Assert.IsTrue(version >= new Version("0.16.0"), "Restic 0.16.0+ required");
    }
    
    [TestMethod]
    public async Task ResticBackup_ShouldHandleJsonOutput()
    {
        // Test that JSON parsing doesn't break with restic updates
        var snapshot = await _resticService.Backup(testPath);
        Assert.IsNotNull(snapshot.Id);
        Assert.IsTrue(snapshot.FilesNew >= 0);
    }
    
    [TestMethod]
    public async Task ResticForget_ShouldApplyRetentionPolicy()
    {
        // Test retention policy application
        await _resticService.Forget(policy);
        var snapshots = await _resticService.ListSnapshots();
        Assert.AreEqual(expectedCount, snapshots.Count());
    }
}
```

**Why Critical**:
- Restic updates may change JSON output format (breaking changes)
- New restic versions may deprecate commands/flags
- Error message formats may change
- Tests catch breaking changes before production

**References**:
- Restic JSON output format: https://restic.readthedocs.io/en/stable/075_scripting.html
- Process spawning patterns in .NET: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process

### 2. SMB Share Mounting in Docker

**Finding**: Use CIFS mount within container
- Container needs CAP_SYS_ADMIN capability for mount operations
- Or use SMBLibrary for user-space SMB access (no mount needed)
- User-space SMBLibrary is safer for Docker

**Decision**: Use SMBLibrary for SMB access (avoids privileged containers)

### 3. Git Repository as Configuration Store

**Finding**: Common pattern in GitOps tools (FluxCD, ArgoCD)
- Each config change = atomic commit
- Rollback = reset to previous commit + new commit documenting rollback
- Never rewrite history (always forward-only commits)
- Use commit messages as audit log

---

## Alternatives Considered

### Alternative 1: Python Backend (FastAPI)
**Rejected Because**:
- ASP.NET Core provides better type safety
- Better async/await patterns for I/O-heavy operations
- Easier integration with Windows ecosystem (SMB)
- User specified ASP.NET Core preference

### Alternative 2: Full-Stack Framework (Next.js + tRPC)
**Rejected Because**:
- Spec requires clear separation of concerns
- Backend needs to run intensive backup operations
- .NET better suited for orchestration and process management
- React-only frontend allows independent scaling

### Alternative 3: Borg as Backup Engine
**Rejected Because**:
- Limited Windows support
- Python runtime dependency
- Restic provides better cross-platform support for homelab

---

## Open Questions for Phase 1

1. **Snapshot Metadata Storage**: Where to store snapshot metadata for quick UI display?
   - Option A: Parse restic JSON output on-demand (slower but simpler)
   - Option B: Cache in lightweight SQLite (faster but adds dependency)
   - **Recommendation**: Start with Option A, add Option B if performance issue

2. **Share Mounting Strategy**: How to make SMB/SSH shares accessible to restic?
   - Option A: FUSE mounts (requires privileged container)
   - Option B: User-space protocols (SMBLibrary, SSH.NET) + temp copy
   - Option C: Restic native SFTP backend for SSH, mount for SMB
   - **Recommendation**: Option C (hybrid - use restic's SFTP, mount SMB with SMBLibrary)

3. **Multi-device Concurrent Backups**: How to prevent resource exhaustion?
   - **Recommendation**: Configurable concurrency limit (default 3), queue remaining jobs

---

## Phase 0 Completion Checklist

- [x] Backup engine selection (Hard-coded Restic integration)
- [x] Backend architecture (Clean Architecture, minimal layers)
- [x] Git integration pattern (LibGit2Sharp)
- [x] Protocol plugin design (IProtocolPlugin interface)
- [x] Frontend state management (React Query + SignalR)
- [x] Technology stack finalized
- [x] Best practices researched
- [x] Alternatives documented
- [x] Restic feature compatibility verified (all 99 FRs satisfied)
- [x] YAGNI analysis (backup engine abstraction rejected, protocol plugins kept)
- [x] Integration testing strategy defined (restic compatibility tests)
- [ ] Phase 1 ready (data model, contracts, quickstart)

**Next**: Phase 1 - Data Model (ResticService, IProtocolPlugin), API Contracts, Quickstart Guide

**Critical for Phase 2**: Implement restic integration tests FIRST (TDD approach) to catch breaking changes from restic updates
