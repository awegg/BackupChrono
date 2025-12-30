# Feature Comparison: BackupChrono vs. Alternatives

## Quick Comparison Table

| Feature | BackupChrono | BackupPC | UrBackup | Kopia | Duplicati |
|---------|-------------|----------|----------|-------|-----------|
| **Architecture** | Central-pull | Central-pull | Agent-based | Client-push | Client-push |
| **Protocol Support** | SMB, SSH, Rsync (plugin) | SMB, Rsync, Tar | Agent protocol | Local, SSH, Cloud | Local, Cloud |
| **Deduplication** | Global, block-level | Global, file-level | Per-client | Global, block-level | Block-level |
| **Compression** | Configurable | Configurable | Yes | Configurable (zstd) | Configurable |
| **Web UI** | Modern, responsive | Dated Perl CGI | Modern | CLI + GUI | Web UI |
| **Configuration** | Git-versioned YAML/JSON | Perl config files | Web UI + DB | CLI flags | Web UI + DB |
| **Device-Centric Model** | ✅ Yes | ❌ Flat sources | ❌ Client-based | ❌ N/A | ❌ N/A |
| **Config Cascade** | Global→Device→Share | Flat config | Limited | N/A | N/A |
| **Deployment** | Docker (mandatory) | Source/packages | Packages/Docker | Binary/Docker | Docker/packages |
| **Database Required** | ❌ No (self-describing repo) | ❌ No | ✅ Yes (SQLite/MySQL) | ❌ No | ✅ Yes (SQLite) |
| **Retention Policies** | Latest/Daily/Weekly/Monthly | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes |
| **File History/Diff** | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes |
| **Wake-on-LAN** | ✅ Yes | ✅ Yes | ❌ No | ❌ No | ❌ No |
| **Blackout Periods** | ✅ Yes | ✅ Yes | Limited | ❌ No | ❌ No |
| **Email Notifications** | ✅ Yes | ✅ Yes | ✅ Yes | Limited | ✅ Yes |
| **Restore Methods** | Download, restore-to-source | Browse, direct restore | Agent restore | CLI restore | Web restore |
| **Cloud Storage** | Plugin-based (S3, etc.) | Limited | ❌ No | ✅ Native (S3, GCS, etc.) | ✅ Native |
| **Encryption** | At-rest (repo level) | Limited | ✅ Yes | ✅ Native | ✅ Native |
| **Active Development** | 🚧 Planned | ⚠️ Minimal | ✅ Active | ✅ Very active | ✅ Active |
| **Target Audience** | Homelab/SMB | Enterprise (legacy) | SMB/Enterprise | Personal/Cloud users | Personal/Windows users |

## Detailed Comparison

### BackupChrono (This Specification)

**Philosophy:** Modern, declarative, homelab-focused central-pull backup with device-centric organization

**Strengths:**
- Device→Share hierarchy with configuration cascade (unique)
- Git-versioned configuration with full history and rollback
- Modern architecture (Docker, database-free, plugin-based)
- Clean separation: devices manage credentials, shares manage paths
- Central-pull model (no agents to maintain on clients)
- Designed for typical homelab: 3-10 devices, mixed protocols

**Design Decisions:**
- Intentionally homelab-scoped (not enterprise-scale)
- Relies on mature components (restic/kopia/borg for deduplication)
- Configuration-as-code philosophy
- Single-user default (multi-user is P5)

**Trade-offs:**
- New project (not yet implemented)
- Smaller ecosystem vs. established tools
- Docker-only (intentional simplification)

---

### BackupPC

**Philosophy:** Central-pull backup server for enterprises (2000s-era design)

**Strengths:**
- Battle-tested (20+ years in production)
- No agents required (SMB, rsync, tar pulls)
- Efficient file-level deduplication across clients
- Advanced features (Wake-on-LAN, blackout periods, host overrides)
- Pool-based storage (single deduplicated pool)

**Weaknesses:**
- Dated Perl/CGI web UI (not mobile-friendly)
- Complex configuration (Perl config files)
- Flat "host" model (no hierarchical organization)
- No configuration version control
- Difficult to containerize
- Development mostly stalled

**When to Choose:**
- Already have BackupPC expertise
- Need proven enterprise solution
- Don't need modern UI

---

### UrBackup

**Philosophy:** Fast client-server backup with image and file backups

**Strengths:**
- Agent-based (fast, efficient)
- Supports both file and image backups
- Active development
- Modern web UI
- Good Windows integration
- LAN-optimized (fast incremental transfers)

**Weaknesses:**
- Requires agents on all clients (maintenance overhead)
- Not agentless for network shares
- Database required (SQLite or MySQL)
- Less mature deduplication vs. specialized tools
- Configuration not version-controlled

**When to Choose:**
- Need Windows system image backups
- Can deploy agents everywhere
- Want fast LAN backups
- Prefer agent-based model

---

### Kopia

**Philosophy:** Fast, secure, open-source backup tool with cloud-native design

**Strengths:**
- Excellent block-level deduplication and compression (zstd)
- Native cloud storage support (S3, GCS, Azure, B2, etc.)
- Strong encryption (AES-256)
- Very active development
- Modern CLI and GUI
- Efficient snapshots
- Good documentation

**Weaknesses:**
- Client-push model (not central-pull)
- No central web UI for multiple clients
- Each client manages its own repository
- No agentless SMB/SSH pulling
- Requires configuration on each client
- Not designed for central monitoring

**When to Choose:**
- Need personal backups to cloud
- Want best-in-class deduplication
- Comfortable with CLI
- Don't need central management
- Cloud storage is primary target

---

### Duplicati

**Philosophy:** Free backup software for Windows/Linux/Mac with cloud focus

**Strengths:**
- Extensive cloud storage support
- Strong encryption
- Web-based UI
- Cross-platform
- Block-level deduplication
- Good for desktop/laptop backups

**Weaknesses:**
- Client-push (not central-pull)
- Database required per client
- Performance issues with large datasets
- Less efficient deduplication vs. Kopia
- Development pace has slowed
- Stability concerns on large repos

**When to Choose:**
- Need Windows desktop backups
- Want many cloud storage options
- Prefer GUI over CLI
- Small to medium datasets

---

## Architecture Comparison

### Central-Pull vs. Client-Push

**Central-Pull (BackupChrono, BackupPC):**
- ✅ No agents to install/maintain on clients
- ✅ Centralized scheduling and monitoring
- ✅ Works with "dumb" network shares (NAS)
- ❌ Requires network access to all sources
- ❌ Limited to protocols server can speak

**Client-Push (Kopia, Duplicati, UrBackup agents):**
- ✅ Client controls when backups run
- ✅ Can backup offline/mobile devices
- ✅ Works behind NAT/firewalls
- ❌ Requires software on every client
- ❌ Harder to centrally monitor

---

## Use Case Recommendations

### Choose **BackupChrono** if:
- You have a homelab with 3-10 devices (NAS, servers, workstations)
- You value configuration-as-code and Git version control
- You want device-centric organization (Device → Shares)
- You prefer Docker deployment
- You want modern web UI
- You don't want to maintain agents

### Choose **BackupPC** if:
- You already run BackupPC and it works
- You have expertise with Perl configuration
- You need proven 20-year track record
- Modern UI is not important
- You're comfortable with legacy tech

### Choose **UrBackup** if:
- You need Windows system image backups
- You can deploy agents on all clients
- You want fast LAN backups
- You need both file and image backups
- Active development matters

### Choose **Kopia** if:
- You need personal/laptop backups to cloud
- You want best-in-class deduplication
- You're comfortable with CLI
- You don't need central management
- Cloud storage is primary target
- You want cutting-edge backup tech

### Choose **Duplicati** if:
- You need Windows desktop backup
- You want many cloud provider options
- You prefer GUI configuration
- You have small to medium datasets
- You need cross-platform support

---

## Technology Stack Comparison

| Component | BackupChrono | BackupPC | UrBackup | Kopia | Duplicati |
|-----------|-------------|----------|----------|-------|-----------|
| **Backend** | C# (ASP.NET Core 8.0) | Perl | C++ | Go | C# (.NET) |
| **Web UI** | Modern JS framework | Perl CGI | Modern web | Electron/React | Webserver |
| **Storage** | Pluggable (restic/kopia/borg) | Custom pool | Custom | Custom (Kopia format) | Custom |
| **Dedup Engine** | Delegate to component | Custom file-level | Block-level | Block-level (zstd) | Block-level |
| **Config Format** | YAML/JSON | Perl | Database/files | JSON/flags | Database |
| **Protocol Support** | SMB, SSH, Rsync+ | SMB, Rsync, Tar | Agent protocol | Many (via rclone) | Many cloud APIs |

---

## Migration Paths

### From BackupPC to BackupChrono:
- Similar mental model (central-pull)
- Upgrade to modern UI and config management
- Leverage device-centric organization
- Migration tool needed for existing backups

### From Kopia/Duplicati to BackupChrono:
- Shift from client-push to central-pull
- Better for homelab with fixed devices
- Trade individual client control for central management

### From UrBackup to BackupChrono:
- Remove agent requirement
- Better for network shares and NAS devices
- Trade system images for file-level backups

---

## Summary

**BackupChrono's Niche:**
- Modern re-imagining of BackupPC's central-pull model
- Designed specifically for homelabs (not enterprise or personal cloud backups)
- Device-centric organization with configuration cascade
- Git-based configuration management
- Docker-native deployment
- No database dependency

**Best Alternative if BackupChrono Doesn't Fit:**
- **Homelab central management**: BackupPC (proven), UrBackup (modern)
- **Personal cloud backups**: Kopia (best-in-class)
- **Windows desktops**: Duplicati, UrBackup
- **Mixed environment with agents acceptable**: UrBackup
