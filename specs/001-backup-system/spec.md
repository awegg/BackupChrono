# Feature Specification: Central Backup System

**Feature Branch**: `001-backup-system`  
**Created**: December 30, 2025  
**Status**: Draft  
**Input**: User description: "A modern, declarative, central-pull backup system with native SMB + SSH pull, deduplication, clean include/exclude rules, retention policies, scheduling, a modern web UI, file history + diff, and Docker deployment. This is a modern re-imagining of BackupPC, with a clean config model and a contemporary UI."

## Out of Scope

The following features are explicitly **not** included in this specification:

- **System/Disk Image Backups**: This system does not create block-level disk images or support bare-metal recovery. It focuses on file-level backups only.
- **Database Backup Tool**: This is not a specialized database backup tool (e.g., for MySQL dumps, PostgreSQL backups). Database backups should be handled by dedicated tools or exported as files first.
- **Continuous Data Protection (CDP)**: Real-time or near-real-time continuous backup is not supported. The system operates on scheduled snapshots.
- **Client-Side Encryption**: End-to-end encryption where data is encrypted on the source before transmission is not included. Encryption-at-rest in the repository may be handled by storage plugins.
- **Active Directory Integration**: No built-in AD/LDAP authentication or user directory services.
- **Application-Aware Backups**: No VSS (Volume Shadow Copy Service) integration or application-specific snapshot coordination.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automated Scheduled Backups (Priority: P1)

A system administrator sets up the backup system to automatically pull data from devices and their shares during off-hours. The administrator first adds devices (servers, NAS, workstations), then configures which shares on each device to back up. Configuration cascades from defaults to device-level to share-level overrides. The system then runs autonomously, creating incremental backups and managing disk space according to the configured policies.

**Why this priority**: This is the core functionality of the backup system. Without automated backups, the system provides no value. This represents the fundamental "backup data without user intervention" capability that everything else builds upon.

**Independent Test**: Can be fully tested by configuring one device with one share, setting a simple schedule, running a backup, and verifying that data was captured. Delivers immediate value by protecting one data source.

**Acceptance Scenarios**:

1. **Given** a device with a network share is accessible, **When** the administrator adds the device and configures a share with a daily schedule at 2 AM, **Then** the system automatically creates backups each night at 2 AM
2. **Given** a backup job is scheduled for a share, **When** the scheduled time arrives, **Then** the system pulls data from the share without requiring manual intervention
3. **Given** multiple devices with shares are configured with different schedules, **When** each schedule time arrives, **Then** each share is backed up independently according to its schedule
4. **Given** a backup completes successfully, **When** the next scheduled backup runs, **Then** only changed data is backed up (incremental backup)
5. **Given** a device-level schedule is configured, **When** a share on that device has no schedule override, **Then** the share uses the device-level schedule

---

### User Story 2 - Restore Files from Backups (Priority: P2)

A user needs to recover deleted or corrupted files. They access the web interface, select a device, view its backup history, select a specific backup snapshot, and browse the shares that were backed up. The user can browse the backup as if it were a regular file system, seeing folder structures and file names from any point in time.

**Why this priority**: Backups are only useful if data can be restored. This is the second most critical capability - the system must both capture and return data. Without restore, backups are worthless.

**Independent Test**: Can be tested by creating a backup of test files, then using the web UI to select the device, view backup history, and browse/download files. Delivers value by proving data can be recovered.

**Acceptance Scenarios**:

1. **Given** multiple devices have backups, **When** the user selects a device, **Then** they see the backup history for that device with all snapshots and their dates/times
2. **Given** a device's backup history is displayed, **When** the user selects a specific snapshot, **Then** they see all shares that were backed up in that snapshot
3. **Given** a snapshot is selected, **When** the user browses a share, **Then** they see the complete directory structure as it existed at that point in time
4. **Given** the user has located a file they need, **When** they click the download button, **Then** the file is downloaded to their local machine
5. **Given** the user needs multiple files, **When** they select a folder, **Then** they can download the entire folder and its contents
6. **Given** a user is viewing a device's backup history, **When** they compare two snapshots, **Then** they can see which files changed across all shares on that device

---

### User Story 3 - Monitor Backup Health (Priority: P3)

An administrator needs to ensure backups are running successfully and identify any issues. They access a dashboard that shows the status of all devices and their shares, when they were last backed up successfully, how much space is being used, and how much space is saved through deduplication. Clicking on a device shows detailed backup history for that device. If any backups fail, alerts are clearly visible.

**Why this priority**: While the system can run automatically, administrators need visibility to ensure everything is working correctly. This provides confidence and early warning of problems.

**Independent Test**: Can be tested by setting up backups and accessing the dashboard to view device status and history. Delivers value by providing operational visibility and peace of mind.

**Acceptance Scenarios**:

1. **Given** the administrator opens the web interface, **When** they view the dashboard, **Then** they see the last backup time for each configured device and its shares
2. **Given** a backup has failed on a device, **When** the administrator views the dashboard, **Then** the failed device/share is clearly highlighted with error information
3. **Given** backups are running, **When** the administrator views storage statistics, **Then** they see total backup size and space saved through deduplication
4. **Given** multiple backups are scheduled, **When** the administrator views the dashboard, **Then** they see upcoming scheduled backups organized by device for the next 24 hours
5. **Given** the administrator clicks on a device, **When** the device detail page loads, **Then** they see complete backup history with all snapshots, success/failure status, and size information for that device

---

### User Story 4 - Configure Backup Sources via Web UI (Priority: P4)

An administrator needs to add a new device and its shares to the backup system. Instead of editing configuration files, they use the web interface to: (1) add a device with connection credentials and device-level settings, (2) add shares under that device with share-specific overrides. Configuration cascades from global defaults → device-level → share-level. Changes take effect immediately.

**Why this priority**: While configuration can initially be done via files, a web UI makes the system more accessible to non-technical users and reduces errors. This is valuable but not essential for core functionality.

**Independent Test**: Can be tested by using the web UI to add a new device with shares and verify the hierarchy appears in the configuration. Delivers value by making administration easier.

**Acceptance Scenarios**:

1. **Given** the administrator is logged into the web interface, **When** they click "Add Device", **Then** they see a form to enter device details (name, protocol, host, credentials)
2. **Given** a device exists, **When** the administrator clicks "Add Share" for that device, **Then** they see a form to configure the share path and optional overrides for schedule, retention, and patterns
3. **Given** a device has device-level settings configured, **When** a share on that device has no overrides, **Then** the share inherits the device-level settings
4. **Given** a share has explicit overrides, **When** the administrator views the effective configuration, **Then** the share-level overrides take precedence over device and global defaults
5. **Given** the administrator edits include/exclude patterns at device level, **When** they save the configuration, **Then** all shares on that device without overrides use the updated patterns

---

### User Story 5 - Track File History Across Snapshots (Priority: P5)

A user needs to understand when a specific file changed and what the previous versions looked like. They locate the file in the most recent backup, then view its history timeline showing all snapshots where the file existed and when it changed. They can download any historical version.

**Why this priority**: This provides advanced forensic capabilities useful for understanding file evolution and recovering from gradual data corruption. It's valuable but builds on the basic restore functionality.

**Independent Test**: Can be tested by creating multiple backups with changes to a specific file, then viewing that file's history timeline in the web UI. Delivers value by providing version tracking.

**Acceptance Scenarios**:

1. **Given** a file exists in multiple snapshots, **When** the user views the file's history, **Then** they see a timeline showing all snapshots containing that file
2. **Given** the file changed in some snapshots, **When** the user views the timeline, **Then** snapshots where the file changed are marked distinctly from snapshots where it remained unchanged
3. **Given** the user has selected a historical version, **When** they download it, **Then** they receive the exact file content from that point in time
4. **Given** two versions of a file exist, **When** the user selects "compare versions", **Then** they see the differences between those versions

---

### User Story 6 - Efficient Unix/Linux Backups via Rsync (Priority: P2)

An administrator configures backup sources for Unix/Linux servers using the rsync protocol. The system leverages rsync's efficient delta-transfer algorithm to minimize network traffic by only transferring file differences rather than entire files. This is particularly effective for large files with small changes.

**Why this priority**: Rsync is the industry-standard protocol for efficient Unix/Linux backups, offering superior performance compared to basic file transfers. Supporting rsync enables efficient backups of Linux servers, which are common in modern infrastructure.

**Independent Test**: Can be tested by configuring a Linux server as a backup source with rsync protocol, making incremental changes to large files, and verifying that subsequent backups transfer minimal data. Delivers value by providing efficient Linux server backups.

**Acceptance Scenarios**:

1. **Given** a Linux server with rsync installed, **When** the administrator configures it as a backup source using rsync protocol, **Then** the system successfully connects and performs a backup
2. **Given** a large file exists in a backup source, **When** a small change is made to the file and a new backup runs, **Then** only the changed portions are transferred
3. **Given** a backup source is configured with rsync, **When** a backup runs, **Then** file permissions, ownership, and timestamps are preserved
4. **Given** rsync is configured for a source, **When** files are deleted on the source, **Then** the backup snapshot reflects the deletions without re-transferring unchanged files

---

### User Story 7 - Email Notifications for Backup Status (Priority: P3)

An administrator configures email notifications to receive daily summaries of backup status. When backups fail or complete successfully, relevant stakeholders receive email alerts with details about what happened, allowing proactive monitoring without constantly checking the web interface.

**Why this priority**: Email notifications enable passive monitoring and ensure administrators are alerted to issues even when not actively watching the system. This is valuable for operational reliability but not essential for core backup functionality.

**Independent Test**: Can be tested by configuring email settings, running backups that succeed and fail, and verifying that appropriate emails are sent. Delivers value by enabling proactive issue detection.

**Acceptance Scenarios**:

1. **Given** email notifications are configured, **When** a backup fails, **Then** administrators receive an email with error details and affected backup source
2. **Given** daily summary emails are enabled, **When** the configured time arrives, **Then** administrators receive a summary of all backup activity in the last 24 hours
3. **Given** email notifications are configured for critical alerts, **When** the repository runs out of space, **Then** administrators immediately receive a critical alert email
4. **Given** multiple administrators are configured, **When** a backup event occurs, **Then** all configured recipients receive the notification

---

### User Story 8 - Direct Restore to Source Location (Priority: P3)

A user needs to restore files directly back to the original source machine rather than downloading them first. They select files from a backup snapshot and choose "restore to source," and the system pushes the files back to the original location, preserving paths and permissions.

**Why this priority**: Direct restore simplifies disaster recovery by eliminating the manual step of downloading and re-uploading files. This is particularly useful for restoring entire directory structures or performing bulk restores.

**Independent Test**: Can be tested by deleting files from a backup source, then using the restore-to-source feature to push them back, and verifying they appear in the correct locations. Delivers value by streamlining disaster recovery workflows.

**Acceptance Scenarios**:

1. **Given** files exist in a backup snapshot, **When** the user selects "restore to source" for those files, **Then** the files are written back to their original paths on the source machine
2. **Given** restored files already exist on the source, **When** restore to source is initiated, **Then** the user is prompted to confirm overwrite
3. **Given** a directory is selected for restore, **When** restore to source runs, **Then** the entire directory structure is recreated on the source with correct permissions
4. **Given** the source machine is offline, **When** restore to source is attempted, **Then** the system provides a clear error and allows the user to download instead

---

### User Story 9 - Version-Controlled Configuration with Git (Priority: P2)

An administrator manages all backup system configuration through Git version control. The system stores configuration in a Git repository, allowing administrators to commit changes, view history, rollback to previous configurations, and track who changed what. The web interface provides a configuration history viewer showing diffs between versions and the ability to restore previous configurations.

**Why this priority**: Git-based configuration management enables infrastructure-as-code practices, provides audit trails, enables collaboration, and allows safe experimentation with rollback capabilities. This is critical for production reliability and team collaboration.

**Independent Test**: Can be tested by making configuration changes through the UI or files, viewing the git commit history in the web interface, comparing configuration versions, and rolling back to a previous configuration. Delivers value by providing configuration safety and auditability.

**Acceptance Scenarios**:

1. **Given** an administrator changes a backup source configuration, **When** the change is saved, **Then** the system automatically commits the change to the Git repository with a descriptive message
2. **Given** multiple configuration changes exist, **When** the administrator views configuration history in the web UI, **Then** they see a timeline of all changes with commit messages, timestamps, and authors
3. **Given** the administrator selects a past configuration version, **When** they view the diff, **Then** they see exactly what changed between that version and the current version
4. **Given** the administrator wants to undo a change, **When** they select "rollback to this version", **Then** the system restores that configuration and creates a new commit documenting the rollback
5. **Given** configuration files are edited directly in the Git repository, **When** the system detects changes, **Then** the web UI reflects the updated configuration

---

### User Story 10 - Multi-User Access Management (Priority: P5)

An administrator needs to grant different levels of access to multiple users accessing the backup system. The administrator can create user accounts, assign roles (admin, operator, read-only), and control what each user can do. Before implementing multi-user support, the system operates as a single-user homelab setup where authentication is handled externally (e.g., reverse proxy).

**Why this priority**: Multi-user management is valuable for team environments but unnecessary for homelab single-admin deployments. Starting with single-user simplifies initial implementation and deployment. This can be added later when the system is used in larger environments.

**Independent Test**: Can be tested by creating multiple user accounts with different roles, logging in as each user, and verifying that permissions are correctly enforced. Delivers value by enabling team collaboration with appropriate access controls.

**Acceptance Scenarios**:

1. **Given** multi-user mode is enabled, **When** an administrator creates a new user account, **Then** the user can log in with their credentials and access the system according to their assigned role
2. **Given** a user has read-only role, **When** they access the web interface, **Then** they can browse snapshots and download files but cannot modify configuration or trigger backups
3. **Given** a user has operator role, **When** they access the web interface, **Then** they can trigger backups and download files but cannot modify system configuration
4. **Given** a user has admin role, **When** they access the web interface, **Then** they can perform all operations including configuration changes and user management
5. **Given** the system is in single-user mode (default for homelab), **When** a user accesses the web interface, **Then** authentication is handled by external mechanisms (reverse proxy, VPN, network isolation)

---

### Edge Cases

- What happens when a backup source becomes temporarily unreachable during a scheduled backup? System retries with exponential backoff (5min, 15min, 45min) within the same backup window, then waits for next scheduled backup
- How does the system handle sources with millions of small files (performance considerations)?
- What happens when the backup repository runs out of disk space? System pauses all backup operations immediately, sends critical alert, and requires admin to free space or adjust retention policy
- How does the system handle files that are being actively written during a backup?
- What happens when include/exclude patterns conflict?
- How does the system handle file name encoding issues (non-UTF-8 filenames)?
- What happens when credentials for a backup source expire or change?
- How does the system handle very large individual files (multi-GB or multi-TB)?
- What happens when the backup schedule overlaps (previous backup still running when next is scheduled)? System skips the scheduled backup, allows current backup to complete, then resumes normal schedule
- How does the system handle symbolic links and hard links? Symbolic links are stored as metadata only (preserve the link itself without following to target); hard links are treated as independent files
- What happens when backed-up files have special permissions or ACLs? System stores permissions/ACLs as metadata and preserves them for restoration where the filesystem supports them
- How does the system handle corrupted data in the backup repository?
- What happens during a restore if the requested file was deleted in all snapshots?
- What happens if the repository metadata becomes corrupted? System can reconstruct metadata from the self-describing backup data structure
- How does the system handle a plugin failure (protocol or storage)? System logs the error, marks the backup as failed, and sends notifications without affecting other backup sources
- What happens when switching storage backends for an existing repository? System must migrate data or maintain compatibility with the repository format

## Requirements *(mandatory)*

### Functional Requirements

**Device & Share Management**
- **FR-001**: System MUST organize backup sources in a two-level hierarchy: Device → Shares
- **FR-002**: System MUST allow defining devices with connection information (protocol, host, credentials)
- **FR-003**: System MUST allow defining multiple shares under each device with share-specific paths
- **FR-004**: System MUST cascade configuration from global defaults → device-level → share-level (most specific wins)
- **FR-005**: System MUST display backup history organized by device with all shares visible under each device

**Source Connectivity**
- **FR-006**: System MUST connect to SMB/CIFS network shares natively without requiring manual mounting
- **FR-007**: System MUST connect to remote servers via SSH/SFTP
- **FR-008**: System MUST connect to remote servers via rsync protocol for efficient delta transfers
- **FR-009**: System MUST authenticate to backup sources using credentials configured at device level
- **FR-010**: System MUST operate as a central pull-based server (no client software on target machines)

**Backup Operations**
- **FR-011**: System MUST perform incremental backups, only transferring changed data
- **FR-012**: System MUST use global deduplication across all devices, shares, and snapshots
- **FR-013**: System MUST handle directory trees containing many small files efficiently
- **FR-014**: System MUST handle individual files of any size (GB to TB scale)
- **FR-015**: System MUST run backup jobs for shares according to configurable schedules (inherited or overridden)
- **FR-016**: System MUST run backups automatically without user intervention
- **FR-017**: System MUST run backups during configurable time windows (e.g., off-hours only)
- **FR-018**: System MUST retry failed backups automatically according to configurable retry policies
- **FR-019**: System MUST skip scheduled backups when a previous backup of the same share is still running
- **FR-020**: System MUST support Wake-on-LAN at device level to wake sleeping devices before backup jobs
- **FR-021**: System MUST support blackout periods where backups are prevented during specified times or days

**Data Management**
- **FR-022**: System MUST support include/exclude rules using glob patterns for files and directories
- **FR-023**: System MUST support regex-based file exclusion patterns (BackupFilesExclude)
- **FR-024**: System MUST support regex-based inclusion-only patterns (BackupFilesOnly)
- **FR-025**: System MUST apply include/exclude rules at global, device, and share levels with share-level taking precedence
- **FR-026**: System MUST support device-level configuration overrides for compression, schedules, and retention policies
- **FR-027**: System MUST support share-level configuration overrides that take precedence over device and global settings
- **FR-028**: System MUST support configurable compression for backup data
- **FR-029**: System MUST apply retention policies automatically, removing old snapshots according to rules
- **FR-030**: System MUST support retention policies for latest, daily, weekly, monthly, and yearly snapshots
- **FR-031**: System MUST maintain data integrity through checksum verification
- **FR-032**: System MUST support repository garbage collection to reclaim space from deleted snapshots
- **FR-033**: System MUST preserve symbolic links as metadata without following them to their targets
- **FR-034**: System MUST store file permissions and ACLs as metadata and restore them where the target filesystem supports them

**Web Interface - Dashboard**
- **FR-035**: Users MUST be able to view devices with their backup status and last backup time
- **FR-036**: Users MUST be able to click on a device to view complete backup history for that device
- **FR-037**: Users MUST be able to see backup status (success/failure) for each device and share
- **FR-038**: Users MUST be able to view backup size and deduplication savings
- **FR-039**: Users MUST be able to see upcoming scheduled backups organized by device

**Web Interface - Browsing & Restore**
- **FR-040**: Users MUST be able to select a device and view its backup history with all snapshots
- **FR-041**: Users MUST be able to select a specific snapshot from a device's backup history
- **FR-042**: Users MUST be able to browse all shares backed up in a selected snapshot
- **FR-043**: Users MUST be able to browse the directory structure within a share snapshot
- **FR-044**: Users MUST be able to download individual files from any share snapshot
- **FR-045**: Users MUST be able to download entire folders from any share snapshot
- **FR-046**: Users MUST be able to restore files and folders directly back to the original device/share location
- **FR-047**: Users MUST be able to compare two snapshots for a device to see what changed across all shares (diff view)
- **FR-048**: Users MUST be able to view the history timeline for a specific file across all device snapshots

**Web Interface - Configuration**
- **FR-049**: Administrators MUST be able to add and edit devices through the web interface
- **FR-050**: Administrators MUST be able to add and edit shares under devices through the web interface
- **FR-051**: Administrators MUST be able to configure include/exclude rules at global, device, and share levels through the web interface
- **FR-052**: Administrators MUST be able to configure retention policies at global, device, and share levels through the web interface
- **FR-053**: Administrators MUST be able to configure backup schedules at global, device, and share levels through the web interface
- **FR-054**: Administrators MUST be able to configure blackout periods through the web interface
- **FR-055**: Administrators MUST be able to view effective configuration for a share showing what is inherited vs. overridden
- **FR-056**: Administrators MUST be able to configure Wake-on-LAN settings at device level through the web interface
- **FR-057**: Administrators MUST be able to view configuration change history through the web interface
- **FR-058**: Administrators MUST be able to view diffs between configuration versions through the web interface
- **FR-059**: Administrators MUST be able to rollback to previous configuration versions through the web interface

**Configuration & Deployment**
- **FR-060**: System MUST use declarative configuration files in a version-controllable format (YAML or JSON)
- **FR-061**: System MUST represent device-share hierarchy clearly in configuration format
- **FR-062**: System MUST store all configuration in a Git repository for version control
- **FR-063**: System MUST automatically commit configuration changes to Git with descriptive messages
- **FR-064**: System MUST run as a container
- **FR-065**: System MUST leverage mature, established open-source components for core functionality (backup engines, deduplication, protocols)
- **FR-066**: System MUST NOT require a central database; all metadata must be stored alongside backup data in a self-describing format
- **FR-067**: System MUST use a plugin architecture for data pull protocols to enable adding new protocols (e.g., FTP, WebDAV) without core system changes
- **FR-068**: System MUST use a plugin architecture for storage backends to enable adding new storage destinations (e.g., Google Drive, S3, Azure Blob) without core system changes
- **FR-069**: System MUST discover and load plugins automatically at startup by scanning a designated plugin directory
- **FR-070**: System MUST support bind-mounting for configuration and repository storage
- **FR-071**: System MUST support deployment behind reverse proxies
- **FR-072**: System MUST support TLS termination at the reverse proxy level

**Repository**
- **FR-073**: System MUST store backups in repositories using pluggable storage backend implementations
- **FR-074**: System MUST store all backup metadata (indexes, catalogs, snapshots) within the backup repository itself in a self-describing format
- **FR-075**: System MUST organize snapshot metadata by device for efficient device-centric queries
- **FR-076**: System MUST support integrity checks on the backup repository
- **FR-077**: System MUST support repair operations for corrupted repository data
- **FR-078**: System MUST support multiple storage backend plugins (local filesystem, S3-compatible, cloud storage providers)
- **FR-079**: System MUST allow storage backend plugins to be configured per repository or globally
- **FR-080**: System MUST embed repository format version identifier in repository metadata
- **FR-081**: System MUST reject operations on repositories with incompatible format versions
- **FR-082**: System MUST provide a migration tool for upgrading repository format versions when format changes occur

**Observability**
- **FR-083**: System MUST provide access to logs through both the web interface and filesystem
- **FR-084**: System MUST expose health check endpoints for repository integrity, backup job status, and storage capacity
- **FR-085**: System MUST log all backup operations with device, share, timestamps, and status
- **FR-086**: System MUST log all restore operations with device, share, timestamp, snapshot identifier, and file list
- **FR-087**: System MUST pause all backup operations when repository storage is exhausted
- **FR-088**: System MUST send critical alerts when backup operations are paused due to storage constraints
- **FR-089**: System MUST support email notifications for backup failures, successes, and daily summaries organized by device
- **FR-090**: System MUST support configurable email recipients and notification thresholds

**API & Extensibility**
- **FR-091**: System MUST provide an API for triggering backups programmatically for specific devices or shares
- **FR-092**: System MUST provide an API for querying snapshot history organized by device
- **FR-093**: System MUST provide an API for managing policies at global, device, and share levels
- **FR-094**: System MUST provide an API for querying configuration history from Git
- **FR-095**: System MUST provide a plugin API for registering new data pull protocol implementations
- **FR-096**: System MUST provide a plugin API for registering new storage backend implementations

**Performance & Reliability**
- **FR-097**: System MUST operate efficiently on home-server class hardware
- **FR-098**: System MUST run continuously and remain stable for long-running operation
- **FR-099**: System MUST support backing up multiple shares from multiple devices concurrently

### Key Entities

- **Device**: Represents a physical or virtual machine being backed up (NAS, server, workstation). Contains protocol type (SMB, SSH, rsync), host/IP address, authentication credentials, and device-level configuration (schedule, retention, compression, patterns). A device can have multiple shares and appears as the primary organizational unit in the UI. Device-level settings cascade to shares unless overridden.

- **Share**: Represents a specific path or mount point on a device to be backed up (e.g., /data, C:\Users, /var/www). Contains share path, optional share-level configuration overrides (schedule, retention, patterns). Multiple shares can exist under one device. Share-level overrides take precedence over device and global settings.

- **Snapshot**: Represents the state of a device at a specific point in time, containing backups of all configured shares. Contains timestamp, device reference, status, and references to deduplicated data blocks for each share. Snapshots are organized by device for easy device-centric navigation and history viewing.

- **Data Block**: Represents a deduplicated chunk of file data. Multiple files and snapshots can reference the same block. Blocks are stored in the repository and tracked by checksums for integrity.

- **Backup Job**: Represents a scheduled or on-demand execution of a backup operation. Contains source reference, start time, end time, status, transferred data size, and error messages if applicable.

- **Retention Policy**: Defines rules for how long snapshots should be kept. Contains counts for latest, daily, weekly, monthly, and yearly snapshots. Applied automatically to remove old snapshots.

- **Include/Exclude Rule**: Defines patterns for which files and directories to include or exclude from backups. Supports glob patterns and regex patterns. Can be defined at global or per-source scope with host-specific overrides.

- **Schedule**: Defines when backup jobs should run for a source. Contains cron-style expressions and optional time windows. Can support multiple schedules per source and blackout periods.

- **Blackout Period**: Defines time periods when backups must not run (e.g., business hours, maintenance windows). Can be configured globally or per-source with specific days and time ranges.

- **Configuration Override**: Defines device-level or share-level configuration that overrides higher-level defaults. Configuration cascades: Global Defaults → Device-Level → Share-Level (most specific wins). Can include compression settings, retention policies, schedules, include/exclude patterns, and protocol-specific parameters.

- **Configuration Commit**: Represents a version-controlled change to system configuration stored in Git. Contains commit hash, timestamp, author, message, and the specific configuration changes made. Enables configuration history tracking and rollback.

- **Pull Protocol Plugin**: Represents a pluggable data transfer implementation (SMB, SSH, rsync, FTP, etc.). Each plugin handles connection, authentication, and data retrieval for its specific protocol. Plugins can be added without modifying core system code.

- **Storage Backend Plugin**: Represents a pluggable storage destination implementation (local filesystem, S3, Google Drive, Azure Blob, etc.). Each plugin handles data persistence, retrieval, and repository operations for its specific storage system. Plugins can be added without modifying core system code.

- **Repository**: Represents the storage location for all backup data and metadata. Contains deduplicated blocks, snapshot metadata, backup catalogs, and integrity information in a self-describing format that requires no external database. Supports operations like garbage collection and repair.

### Assumptions

- Network connectivity to devices is stable during scheduled backup windows
- Administrators have necessary permissions to access devices and shares (read permissions)
- Device-level settings are appropriate defaults for most shares on that device
- Share-level overrides are used sparingly for exceptions
- The system will be deployed on a server with sufficient storage for expected backup data
- System time is synchronized (NTP) for accurate scheduling and snapshot timestamps
- Web interface access will be secured at the network or reverse proxy level (not specifying authentication mechanism here)
- Most files in backup sources are relatively stable (not constantly changing), making deduplication effective
- Backup sources use standard file systems with UTF-8 compatible file naming
- The default retention policy will be: 7 latest, 7 daily, 4 weekly, 12 monthly, 3 yearly (can be customized)
- Compression will default to enabled with a balanced algorithm (can be disabled if needed)
- Backup windows will default to 2 AM - 6 AM local time (can be customized)
- Failed backups will retry with exponential backoff (5min, 15min, 45min) within the backup window before waiting for the next scheduled backup
- Email notifications require SMTP server configuration
- Wake-on-LAN requires network broadcast capability and compatible hardware on backup sources
- Rsync protocol requires rsync daemon or rsync-over-SSH on backup sources
- Restore-to-source requires write permissions on the target backup source
- Git repository for configuration will be initialized on first deployment and must be accessible to the container
- Configuration changes through the web UI will be committed with the authenticated user's identity where available
- The system will use only mature, well-established open-source components for core backup functionality (no custom protocol implementations)
- Docker is the only supported deployment method; traditional installations are not supported
- All backup metadata is self-contained within the repository; no separate database backup is required
- The system itself does not require backup; all critical data (configuration in Git, backups in repository) is already protected
- Plugin architecture allows third-party protocol and storage implementations to be added as container extensions
- Built-in plugins will include: SMB, SSH/SFTP, rsync for protocols; local filesystem and S3-compatible for storage backends
- Plugins are discovered automatically at container startup by scanning a designated plugin directory in the filesystem
- Repository format versions are strictly enforced; each container version supports only one repository format version
- Repository format changes require explicit migration using provided migration tool before the repository can be used with newer container versions
- Restore operations are logged in general system logs; no dedicated restore job tracking entity exists

## Clarifications

### Session 2025-12-30

- Q: When a backup source becomes temporarily unreachable during a scheduled backup, what should the retry behavior be? → A: Retry with exponential backoff (5min, 15min, 45min) within the same backup window, then wait for next schedule
- Q: When the backup repository runs out of disk space, how should the system respond? → A: Pause all backup operations immediately, send critical alert, require admin to free space or adjust retention policy
- Q: How should the system handle symbolic links encountered during backup? → A: Store symlinks as metadata only, preserve the link itself but don't follow to target
- Q: When the backup schedule overlaps (previous backup still running when the next is scheduled to start), what should happen? → A: Skip the scheduled backup, let current one finish, resume normal schedule afterward
- Q: How should the system handle files with special permissions or ACLs (Access Control Lists)? → A: Store permissions/ACLs as metadata, preserve for restoration where filesystem supports them

## Success Criteria *(mandatory)*

### Measurable Outcomes

**Backup Reliability**
- **SC-001**: System successfully completes 95% or more of scheduled backups without errors
- **SC-002**: Backup jobs complete within their scheduled time windows for sources up to 1TB in size
- **SC-003**: System runs continuously for 30+ days without requiring restart or intervention

**Performance**
- **SC-004**: Incremental backups capture changed data within 10 minutes for sources with up to 100GB of changes
- **SC-005**: Users can browse and download files from backups within 5 seconds for files under 100MB
- **SC-006**: System handles backup sources with 1 million+ files without degradation

**Storage Efficiency**
- **SC-007**: Global deduplication reduces total storage requirements by at least 40% for typical file server workloads
- **SC-008**: Repository garbage collection reclaims space from deleted snapshots within 24 hours

**Usability**
- **SC-009**: Administrators can configure a new backup source in under 5 minutes using the web interface
- **SC-010**: Users can locate and restore a specific file within 2 minutes without external documentation
- **SC-011**: Dashboard clearly displays backup health status at a glance (within 3 seconds of loading)
- **SC-012**: Administrators can view configuration change history and identify who made specific changes within 30 seconds

**Recoverability**
- **SC-013**: Users successfully restore files on first attempt 90% of the time
- **SC-014**: Complete disaster recovery (restore entire backup source) completes at rates of at least 50MB/second on gigabit networks
- **SC-015**: Configuration can be rolled back to any previous version within 1 minute through the web UI

**Operational Health**
- **SC-016**: System provides clear error messages and diagnostic information for 100% of backup failures
- **SC-017**: Repository integrity checks complete successfully on all repositories
- **SC-018**: Administrators receive visibility into upcoming backups, storage usage, and job status without examining log files
- **SC-019**: All configuration changes are tracked in Git with complete audit trail (100% coverage)
- **SC-020**: Repository metadata can be fully reconstructed from backup data without external database dependencies
