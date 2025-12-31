# Implementation Tasks: BackupChrono

**Feature**: Central Pull Backup System  
**Branch**: `001-backup-system`  
**Date**: 2025-12-30  
**Phase**: 2 - Implementation Tasks

## Overview

This document organizes implementation tasks by **user story** to enable independent, incremental delivery. Each user story represents a complete, testable feature increment that delivers value.

**Implementation Strategy**:
- **MVP First**: User Stories 1-2 provide core backup and restore functionality
- **Incremental Delivery**: Each user story can be implemented independently after foundational tasks
- **Parallel Execution**: Tasks marked `[P]` can be developed concurrently
- **Independent Testing**: Each user story has clear test criteria before moving to the next

**Task Format**: `- [ ] [TaskID] [P?] [Story?] Description with file path`

**User Story Priority Order**:
1. **US1** (P1): Automated Scheduled Backups - Core functionality
2. **US2** (P2): Restore Files from Backups - Recovery capability  
3. **US9** (P2): Version-Controlled Configuration with Git - Config management
4. **US6** (P2): Efficient Unix/Linux Backups via Rsync - Protocol support
5. **US3** (P3): Monitor Backup Health - Dashboard visibility
6. **US7** (P3): Email Notifications for Backup Status - Alerting
7. **US8** (P3): Direct Restore to Source Location - Push restore
8. **US4** (P4): Configure Backup Sources via Web UI - Admin UI
9. **US5** (P5): Track File History Across Backups - Version tracking
10. **US10** (P5): Multi-User Access Management - RBAC

---

## Phase 1: Setup

**Goal**: Initialize project structure, dependencies, Docker environment, and CI/CD pipeline.

**Effort Estimate**: 2-4 days

### Tasks

- [X] T001 Create ASP.NET Core solution with 3 projects (Core, Infrastructure, Api) per plan.md architecture
- [X] T002 [P] Add NuGet packages to BackupChrono.Core: No external dependencies needed (domain models only)
- [X] T003 [P] Add NuGet packages to BackupChrono.Infrastructure: LibGit2Sharp, Quartz, Microsoft.AspNetCore.SignalR, SSH.NET, SMBLibrary, YamlDotNet
- [X] T004 [P] Add NuGet packages to BackupChrono.Api: Microsoft.AspNetCore.OpenApi, Swashbuckle.AspNetCore, Serilog.AspNetCore
- [X] T005 [P] Create test projects: BackupChrono.UnitTests (Core+Infrastructure+Api), BackupChrono.Infrastructure.Restic.Tests, BackupChrono.IntegrationTests
- [X] T006 [P] Add test NuGet packages: xUnit, xUnit.runner.visualstudio, Moq, FluentAssertions, Microsoft.AspNetCore.Mvc.Testing, Testcontainers
- [X] T007 Create Dockerfile.backend for .NET 8.0 runtime with restic binary bundled
- [X] T008 [P] Create Dockerfile.frontend placeholder (nginx serving React build) for future frontend implementation
- [X] T009 Create docker-compose.yml for local development with backend, config volume, repository volume
- [X] T010 Create docker-compose.prod.yml for production deployment
- [X] T011 Create .dockerignore with bin/, obj/, node_modules/, .git/, .vs/
- [X] T012 [P] Create README.md with setup instructions, architecture overview, Docker deployment guide
- [X] T013 [P] Create directory structure: src/backend/, src/frontend/, src/plugins/, docker/, docs/
- [X] T014 Initialize Git repository with .gitignore (.NET, Node, Docker, IDE files)
- [X] T200 [P] Create CI/CD pipeline configuration (.github/workflows/) for build, test, Docker image publishing

**Checkpoint**: Solution compiles, Docker builds successfully, all test projects execute (empty), CI/CD pipeline validates PRs.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Goal**: Implement shared infrastructure needed by all user stories.

**Effort Estimate**: 10-12 days

### Tasks

- [X] T015 [P] Create ProtocolType enum in BackupChrono.Core/Entities/ProtocolType.cs
- [X] T016 [P] Create BackupJobType enum in BackupChrono.Core/Entities/BackupJobType.cs
- [X] T017 [P] Create BackupJobStatus enum in BackupChrono.Core/Entities/BackupJobStatus.cs
- [X] T018 [P] Create BackupStatus enum in BackupChrono.Core/Entities/BackupStatus.cs
- [X] T019 [P] Create Schedule value object in BackupChrono.Core/ValueObjects/Schedule.cs
- [X] T020 [P] Create RetentionPolicy value object in BackupChrono.Core/ValueObjects/RetentionPolicy.cs
- [X] T021 [P] Create IncludeExcludeRules value object in BackupChrono.Core/ValueObjects/IncludeExcludeRules.cs
- [X] T022 [P] Create ConfigurationCommit value object in BackupChrono.Core/ValueObjects/ConfigurationCommit.cs
- [X] T023 [P] Create Device entity in BackupChrono.Core/Entities/Device.cs with all fields per data-model.md
- [X] T024 [P] Create Share entity in BackupChrono.Core/Entities/Share.cs with all fields per data-model.md
- [X] T025 [P] Create Backup entity in BackupChrono.Core/Entities/Backup.cs with all fields per data-model.md
- [X] T026 [P] Create BackupJob entity in BackupChrono.Core/Entities/BackupJob.cs with all fields per data-model.md
- [X] T027 [P] Create DataBlock entity in BackupChrono.Core/Entities/DataBlock.cs (conceptual, managed by restic)
- [X] T028 [P] Create Repository entity in BackupChrono.Core/Entities/Repository.cs
- [X] T029 [P] Create IProtocolPlugin interface in BackupChrono.Core/Interfaces/IProtocolPlugin.cs with 4 methods per data-model.md
- [X] T030 [P] Create IResticService interface in BackupChrono.Core/Interfaces/IResticService.cs with ~12 methods per data-model.md
- [X] T031 [P] Create IDeviceService interface in BackupChrono.Core/Interfaces/IDeviceService.cs for CRUD operations
- [X] T032 [P] Create IShareService interface in BackupChrono.Core/Interfaces/IShareService.cs for CRUD operations
- [X] T033 [P] Create IBackupOrchestrator interface in BackupChrono.Core/Interfaces/IBackupOrchestrator.cs for job coordination
- [X] T034 [P] Create DTOs in BackupChrono.Core/DTOs/: BackupProgress, RestoreProgress, RepositoryStats, FileEntry, FileVersion per data-model.md
- [X] T035 Implement GitConfigService in src/backend/BackupChrono.Infrastructure/Git/GitConfigService.cs using LibGit2Sharp for YAML file read/write and commits
- [X] T036 Implement ResticClient in src/backend/BackupChrono.Infrastructure/Restic/ResticClient.cs for process spawning with JSON output parsing
- [X] T037 Implement ResticService in src/backend/BackupChrono.Infrastructure/Restic/ResticService.cs implementing IResticService with all 12+ methods
- [X] T038 Implement JsonParsers in src/backend/BackupChrono.Infrastructure/Restic/JsonParsers.cs for parsing restic JSON output (backup stats, progress, file lists)
- [X] T039 [P] Implement SmbProtocolPlugin in src/backend/BackupChrono.Infrastructure/Protocols/SmbProtocolPlugin.cs using SMBLibrary
- [X] T040 [P] Implement SshProtocolPlugin in src/backend/BackupChrono.Infrastructure/Protocols/SshProtocolPlugin.cs using SSH.NET
- [X] T041 [P] Implement RsyncProtocolPlugin in src/backend/BackupChrono.Infrastructure/Protocols/RsyncProtocolPlugin.cs spawning rsync process
- [X] T042 Implement ProtocolPluginLoader in src/backend/BackupChrono.Infrastructure/Protocols/ProtocolPluginLoader.cs for plugin discovery
- [X] T043 Configure Serilog in BackupChrono.Api/Program.cs with JSON logging to stdout and file
- [X] T044 Add global error handling middleware in BackupChrono.Api/Middleware/ErrorHandlingMiddleware.cs
- [X] T045 Configure CORS in BackupChrono.Api/Program.cs for frontend origin
- [X] T046 Configure Swagger/OpenAPI in BackupChrono.Api/Program.cs using contracts/openapi.yaml
- [X] T047 Configure dependency injection in BackupChrono.Api/Program.cs for all services and plugins
- [X] T048 Create health check endpoint at /health in BackupChrono.Api/Controllers/HealthController.cs
- [X] T049 Create ResticVersionTests in BackupChrono.Infrastructure.Restic.Tests/ResticVersionTests.cs to verify restic binary compatibility
- [X] T050 Create ResticErrorHandlingTests in BackupChrono.Infrastructure.Restic.Tests/ResticErrorHandlingTests.cs to verify error scenarios

**Checkpoint**: All foundational services compile and pass unit tests. Health check endpoint responds. Plugins load successfully at startup.

---

## Phase 3: User Story 1 - Automated Scheduled Backups (P1) 🎯 MVP

**Story Goal**: As a homelab admin, I want my devices to be backed up automatically on a schedule, so I don't have to manually trigger backups.

**Effort Estimate**: 7-9 days (includes production-critical monitoring and graceful shutdown)

**Independent Test Criteria**:
- Device can be created with SMB credentials and schedule
- Scheduled backup job executes at configured time
- Backup completes successfully and is stored in repository
- Backup data is deduplicated (verify with restic stats)
- Next backup is scheduled correctly

### Implementation Tasks

- [ ] T051 [US1] Implement DeviceService in src/backend/BackupChrono.Infrastructure/Services/DeviceService.cs for CRUD operations on devices with YAML persistence via GitConfigService
- [ ] T052 [US1] Implement ShareService in src/backend/BackupChrono.Infrastructure/Services/ShareService.cs for CRUD operations on shares with YAML persistence via GitConfigService
- [ ] T053 [US1] Implement BackupOrchestrator in src/backend/BackupChrono.Infrastructure/Services/BackupOrchestrator.cs to coordinate device wake, protocol mounting, restic execution, unmounting
- [ ] T054 [US1] Implement QuartzSchedulerService in src/backend/BackupChrono.Infrastructure/Scheduling/QuartzSchedulerService.cs to schedule backup jobs based on device/share schedules
- [ ] T055 [US1] Implement BackupJob Quartz job in src/backend/BackupChrono.Infrastructure/Scheduling/BackupJob.cs that calls BackupOrchestrator
- [ ] T056 [US1] Create DevicesController in BackupChrono.Api/Controllers/DevicesController.cs with GET /devices, POST /devices, GET /devices/{id}, PUT /devices/{id}, DELETE /devices/{id} per openapi.yaml
- [ ] T057 [US1] Create SharesController in BackupChrono.Api/Controllers/SharesController.cs with GET /devices/{deviceId}/shares, POST, GET, PUT, DELETE per openapi.yaml
- [ ] T058 [US1] Implement POST /devices/{deviceId}/test-connection endpoint in DevicesController to test connectivity before creating device
- [ ] T059 [US1] Implement POST /devices/{deviceId}/wake endpoint in DevicesController for Wake-on-LAN
- [ ] T060 [US1] Create BackupJobsController in BackupChrono.Api/Controllers/BackupJobsController.cs with GET /backup-jobs, POST /backup-jobs (manual trigger), GET /backup-jobs/{id}
- [ ] T061 [US1] Implement repository initialization logic in ResticService.InitializeRepository when first backup runs
- [ ] T062 [US1] Implement configuration cascade logic in BackupOrchestrator (global → device → share) per data-model.md semantics
- [ ] T063 [US1] Implement retry logic with exponential backoff (5min, 15min, 45min) in BackupOrchestrator for failed backups
- [ ] T064 [US1] Create DeviceServiceTests in BackupChrono.Infrastructure.Tests/Services/DeviceServiceTests.cs for device CRUD operations
- [ ] T065 [US1] Create BackupOrchestratorTests in BackupChrono.Infrastructure.Tests/Services/BackupOrchestratorTests.cs for backup workflow
- [ ] T066 [US1] Create ResticBackupTests in BackupChrono.Infrastructure.Restic.Tests/ResticBackupTests.cs for backup operations against test repository
- [ ] T067 [US1] Create end-to-end backup test in BackupChrono.Integration.Tests/BackupFlowTests.cs using TestContainers
- [ ] T180 [US1] Add health check logic to HealthController for repository integrity, backup job status, storage capacity
- [ ] T181 [US1] Implement storage capacity monitoring with alerts when repository disk usage exceeds threshold
- [ ] T182 [US1] Add backup operation pause logic when repository storage exhausted
- [ ] T195 [US1] Add graceful shutdown handling in BackupChrono.Api/Program.cs to complete running backups before exit

**Checkpoint**: Can create device with shares, trigger manual backup via API, verify backup in repository with restic CLI, scheduled backups execute automatically, health checks validate system state, graceful shutdown prevents data loss.

---

## Phase 4: User Story 2 - Restore Files from Backups (P2) 🎯 MVP

**Story Goal**: As a homelab admin, I want to browse and restore files from backups, so I can recover data when needed.

**Effort Estimate**: 4-5 days

**Independent Test Criteria**:
- Can list all backups for a device
- Can browse directory structure within a backup
- Can download individual files from a backup
- Can download folders (as .tar.gz) from a backup
- Restored files match original checksums

### Implementation Tasks

- [ ] T068 [US2] Create BackupsController in BackupChrono.Api/Controllers/BackupsController.cs with GET /backups, GET /backups/{id}, GET /backups/{id}/files, POST /backups/{id}/restore per openapi.yaml
- [ ] T069 [US2] Implement GET /devices/{deviceId}/backups endpoint in DevicesController to list backups for a device
- [ ] T070 [US2] Implement ResticService.ListBackups to query restic snapshots and return Backup entities
- [ ] T071 [US2] Implement ResticService.GetBackup to get backup details by ID
- [ ] T072 [US2] Implement ResticService.BrowseBackup to list files/folders at a path within a backup using restic ls
- [ ] T073 [US2] Implement file download endpoint GET /backups/{id}/files with path query parameter using restic dump
- [ ] T074 [US2] Implement folder download endpoint GET /backups/{id}/files with path and folder=true query parameter using restic restore to temp + tar.gz
- [ ] T075 [US2] Implement ResticService.RestoreBackup for restoring files to a local target path using restic restore
- [ ] T076 [US2] Implement progress tracking for restore operations using ResticClient JSON output parsing
- [ ] T077 [US2] Create RestoreProgress SignalR hub in BackupChrono.Api/Hubs/RestoreProgressHub.cs for real-time restore status
- [ ] T078 [US2] Create ResticRestoreTests in BackupChrono.Infrastructure.Restic.Tests/ResticRestoreTests.cs for restore operations
- [ ] T079 [US2] Create end-to-end restore test in BackupChrono.Integration.Tests/RestoreFlowTests.cs verifying file checksum after restore

**Checkpoint**: Can browse backup contents via API, download individual files, download folders, verify restored file integrity.

---

## Phase 5: User Story 9 - Version-Controlled Configuration with Git (P2)

**Story Goal**: As a homelab admin, I want all configuration changes tracked in Git, so I can review history and rollback mistakes.

**Effort Estimate**: 3-4 days

**Independent Test Criteria**:
- Configuration changes create Git commits automatically
- Can view configuration history via API
- Can view diff between two configuration versions
- Can rollback to a previous configuration version
- Configuration file structure matches device-share hierarchy

### Implementation Tasks

- [ ] T080 [US9] Implement GitConfigService.CommitChanges in src/backend/BackupChrono.Infrastructure/Git/GitConfigService.cs to auto-commit on device/share CRUD
- [ ] T081 [US9] Update DeviceService to call GitConfigService.CommitChanges after create/update/delete operations
- [ ] T082 [US9] Update ShareService to call GitConfigService.CommitChanges after create/update/delete operations
- [ ] T083 [US9] Create ConfigurationController in BackupChrono.Api/Controllers/ConfigurationController.cs with GET /configuration/global, PUT /configuration/global per openapi.yaml
- [ ] T084 [US9] Implement GET /configuration/history endpoint in ConfigurationController to list commits using LibGit2Sharp
- [ ] T085 [US9] Implement GET /configuration/history/{commitHash} endpoint to view specific commit details and diff
- [ ] T086 [US9] Implement POST /configuration/rollback endpoint to checkout previous commit and apply configuration
- [ ] T087 [US9] Implement global configuration YAML read/write in GitConfigService for config/global.yaml
- [ ] T088 [US9] Add validation to rollback endpoint: prevent rollback if uncommitted changes exist
- [ ] T089 [US9] Create GitConfigServiceTests in BackupChrono.Infrastructure.Tests/Git/GitConfigServiceTests.cs for commit, history, rollback operations
- [ ] T090 [US9] Create end-to-end configuration test in BackupChrono.Integration.Tests/ConfigurationFlowTests.cs verifying Git commits on CRUD operations

**Checkpoint**: Device/share CRUD creates Git commits, can view history via API, can rollback configuration and verify changes applied.

---

## Phase 6: User Story 6 - Efficient Unix/Linux Backups via Rsync (P2)

**Story Goal**: As a homelab admin, I want to use rsync for Unix/Linux devices, so I get efficient delta transfers.

**Effort Estimate**: 3-4 days

**Independent Test Criteria**:
- Can create device with protocol=Rsync
- RsyncProtocolPlugin syncs data to local staging directory
- ResticService backs up staged data to repository
- Incremental rsync only transfers changed files
- Cleanup removes staging directory after backup

### Implementation Tasks

- [ ] T091 [US6] Enhance RsyncProtocolPlugin.MountShare in src/backend/BackupChrono.Infrastructure/Protocols/RsyncProtocolPlugin.cs to spawn rsync process and sync to temp directory
- [ ] T092 [US6] Implement rsync progress parsing in RsyncProtocolPlugin for file transfer status
- [ ] T093 [US6] Implement rsync over SSH authentication using SSH.NET credentials from Device entity
- [ ] T094 [US6] Implement rsync daemon protocol support (rsync://host/module) with username/password auth
- [ ] T095 [US6] Add cleanup logic to RsyncProtocolPlugin.UnmountShare to remove staging directory after backup completes
- [ ] T096 [US6] Add error handling for rsync process failures (connection refused, auth failure, permission denied)
- [ ] T097 [US6] Create RsyncProtocolPluginTests in BackupChrono.Infrastructure.Tests/Protocols/RsyncProtocolPluginTests.cs for rsync operations
- [ ] T098 [US6] Create integration test in BackupChrono.Integration.Tests/RsyncBackupTests.cs with rsync daemon container

**Checkpoint**: Can create rsync device, trigger backup, verify files synced to staging then backed up to repository, staging cleaned up.

---

## Phase 7: User Story 3 - Monitor Backup Health (P3)

**Story Goal**: As a homelab admin, I want to see backup status at a glance on a dashboard, so I know if backups are healthy.

**Effort Estimate**: 3-4 days

**Independent Test Criteria**:
- Dashboard shows all devices with last backup time and status
- Can see upcoming scheduled backups
- Can view backup size and deduplication savings
- Failed backups are highlighted
- Real-time backup progress displayed during execution

### Implementation Tasks

- [ ] T099 [US3] Implement GET /devices endpoint enhancement in DevicesController to include lastBackupTime, lastBackupStatus, nextScheduledBackup
- [ ] T100 [US3] Implement ResticService.GetStats to query repository statistics (total size, unique size, deduplication savings)
- [ ] T101 [US3] Create RepositoryController in BackupChrono.Api/Controllers/RepositoryController.cs with GET /repository/stats endpoint
- [ ] T102 [US3] Create BackupProgressHub SignalR hub in BackupChrono.Api/Hubs/BackupProgressHub.cs for real-time backup status broadcasts
- [ ] T103 [US3] Update BackupOrchestrator to broadcast progress events via BackupProgressHub during backup execution
- [ ] T104 [US3] Implement GET /backup-jobs endpoint enhancement to support filtering by status (pending, running, failed) and date range
- [ ] T105 [US3] Implement GET /backup-jobs/upcoming endpoint to query next scheduled jobs from Quartz scheduler
- [ ] T106 [US3] Add logging to BackupJob for structured logging with device name, share name, duration, status to logs/backup-jobs.jsonl
- [ ] T107 [US3] Create BackupProgressHubTests in BackupChrono.Api.Tests/Hubs/BackupProgressHubTests.cs for SignalR messaging
- [ ] T108 [US3] Create end-to-end dashboard test in BackupChrono.Integration.Tests/DashboardTests.cs verifying backup status display

**Checkpoint**: API returns device status with last backup time, can view repository stats, SignalR broadcasts backup progress, upcoming backups visible.

---

## Phase 8: User Story 7 - Email Notifications for Backup Status (P3)

**Story Goal**: As a homelab admin, I want email notifications for backup failures and daily summaries, so I'm informed of issues.

**Effort Estimate**: 3-4 days

**Independent Test Criteria**:
- Email sent on backup failure with error details
- Email sent on backup success (if configured)
- Daily summary email lists all backup activity
- Can configure SMTP settings via API
- Can test email configuration before saving

### Implementation Tasks

- [ ] T109 [US7] Create NotificationSettings entity in BackupChrono.Core/Entities/NotificationSettings.cs with SMTP configuration fields per data-model.md
- [ ] T110 [US7] Implement IEmailService interface in BackupChrono.Core/Interfaces/IEmailService.cs with SendEmail, TestConnection methods
- [ ] T111 [US7] Implement EmailService in src/backend/BackupChrono.Infrastructure/Notifications/EmailService.cs using MailKit for SMTP
- [ ] T112 [US7] Implement notification templates in src/backend/BackupChrono.Infrastructure/Notifications/Templates/ for failure, success, daily summary emails
- [ ] T113 [US7] Update BackupOrchestrator to call EmailService after backup completion (success or failure)
- [ ] T114 [US7] Implement DailySummaryJob Quartz job in src/backend/BackupChrono.Infrastructure/Scheduling/DailySummaryJob.cs to send summary at configured time
- [ ] T115 [US7] Create NotificationsController in BackupChrono.Api/Controllers/NotificationsController.cs with GET /notifications, PUT /notifications, POST /notifications/test per openapi.yaml
- [ ] T116 [US7] Implement notification settings YAML persistence via GitConfigService at config/notifications.yaml
- [ ] T117 [US7] Add email sending to background queue to avoid blocking backup operations
- [ ] T118 [US7] Create EmailServiceTests in BackupChrono.Infrastructure.Tests/Notifications/EmailServiceTests.cs for email operations
- [ ] T119 [US7] Create integration test in BackupChrono.Integration.Tests/EmailNotificationTests.cs with mock SMTP server

**Checkpoint**: SMTP settings configurable via API, test email succeeds, backup failure triggers email, daily summary sent at configured time.

---

## Phase 9: User Story 8 - Direct Restore to Source Location (P3)

**Story Goal**: As a homelab admin, I want to restore files directly back to the source device, so I can recover in-place quickly.

**Effort Estimate**: 2-3 days

**Independent Test Criteria**:
- Can trigger restore-to-source via API
- Protocol plugin mounts share with write permissions
- Files restored to original path on source device
- Restore progress tracked and broadcast via SignalR
- Cleanup unmounts share after restore completes

### Implementation Tasks

- [ ] T120 [US8] Enhance IProtocolPlugin interface to add MountShareWritable method in BackupChrono.Core/Interfaces/IProtocolPlugin.cs
- [ ] T121 [US8] Implement SmbProtocolPlugin.MountShareWritable for write-enabled SMB mounting using SMBLibrary
- [ ] T122 [US8] Implement SshProtocolPlugin.MountShareWritable for SFTP write access using SSH.NET
- [ ] T123 [US8] Implement RsyncProtocolPlugin.MountShareWritable for rsync write operations
- [ ] T124 [US8] Implement POST /backups/{backupId}/restore-to-source endpoint in BackupsController with shareId, filePaths parameters
- [ ] T125 [US8] Implement BackupOrchestrator.RestoreToSource method to mount share, restore files via ResticService, unmount
- [ ] T126 [US8] Add error handling for write permission failures during restore-to-source
- [ ] T127 [US8] Update RestoreProgressHub to support restore-to-source progress broadcasts
- [ ] T128 [US8] Create end-to-end restore-to-source test in BackupChrono.Integration.Tests/RestoreToSourceTests.cs verifying files on source device

**Checkpoint**: Can trigger restore-to-source via API, files restored to original device location, progress tracked via SignalR, cleanup completes.

---

## Phase 10: User Story 4 - Configure Backup Sources via Web UI (P4)

**Story Goal**: As a homelab admin, I want to configure devices and shares through a web interface, so I don't edit YAML manually.

**Effort Estimate**: 8-10 days

**Independent Test Criteria**:
- React frontend displays device list
- Can create/edit device through forms
- Can create/edit shares under devices
- Can configure schedules, retention, include/exclude rules via UI
- Can view effective configuration for a share (inherited vs overridden)

### Implementation Tasks

- [ ] T129 [P] [US4] Initialize React 18 project in src/frontend/ with Vite, TypeScript, Tailwind CSS
- [ ] T130 [P] [US4] Install frontend dependencies: react-router-dom, axios, @tanstack/react-query, react-hook-form, zod, lucide-react
- [ ] T131 [P] [US4] Create API client in src/frontend/src/services/api.ts with axios instance configured for backend base URL
- [ ] T132 [P] [US4] Create TypeScript interfaces in src/frontend/src/types/ matching OpenAPI schemas (Device, Share, Schedule, RetentionPolicy, etc.)
- [ ] T133 [P] [US4] Create deviceService.ts in src/frontend/src/services/ with CRUD methods calling backend API
- [ ] T134 [P] [US4] Create shareService.ts in src/frontend/src/services/ with CRUD methods
- [ ] T135 [US4] Create Dashboard page in src/frontend/src/pages/Dashboard.tsx displaying device list with status
- [ ] T136 [US4] Create DeviceList component in src/frontend/src/components/devices/DeviceList.tsx
- [ ] T137 [US4] Create DeviceCard component in src/frontend/src/components/devices/DeviceCard.tsx showing device name, status, last backup
- [ ] T138 [US4] Create DeviceForm component in src/frontend/src/components/devices/DeviceForm.tsx with react-hook-form for create/edit
- [ ] T139 [US4] Create ShareForm component in src/frontend/src/components/shares/ShareForm.tsx for share configuration
- [ ] T140 [US4] Create ScheduleEditor component in src/frontend/src/components/common/ScheduleEditor.tsx with cron expression builder
- [ ] T141 [US4] Create RetentionPolicyEditor component in src/frontend/src/components/common/RetentionPolicyEditor.tsx
- [ ] T142 [US4] Create IncludeExcludeEditor component in src/frontend/src/components/common/IncludeExcludeEditor.tsx for pattern management
- [ ] T143 [US4] Implement GET /devices/{deviceId}/shares/{shareId}/effective-config endpoint in SharesController
- [ ] T144 [US4] Create EffectiveConfigView component in src/frontend/src/components/shares/EffectiveConfigView.tsx showing inherited vs overridden settings
- [ ] T145 [US4] Create DeviceDetail page in src/frontend/src/pages/DeviceDetail.tsx with device info and share list
- [ ] T146 [US4] Create DeviceSettings page in src/frontend/src/pages/DeviceSettings.tsx for device configuration
- [ ] T147 [US4] Create ShareSettings page in src/frontend/src/pages/ShareSettings.tsx for share configuration
- [ ] T148 [US4] Implement routing in src/frontend/src/App.tsx using react-router-dom
- [ ] T149 [US4] Create layout components in src/frontend/src/components/layout/: Sidebar, Header, Navigation
- [ ] T150 [US4] Add form validation using zod schemas matching backend validation rules
- [ ] T151 [US4] Create component tests in src/frontend/tests/unit/components/ using Vitest + React Testing Library
- [ ] T152 [US4] Create Playwright e2e test in src/frontend/tests/e2e/configuration.spec.ts for device/share CRUD workflow

**Checkpoint**: Frontend displays devices, can create/edit device, can create/edit shares, effective config view shows inheritance, e2e tests pass.

---

## Phase 11: User Story 5 - Track File History Across Backups (P5)

**Story Goal**: As a homelab admin, I want to see the version history of a file across backups, so I can restore specific versions.

**Effort Estimate**: 3-4 days

**Independent Test Criteria**:
- Can search for a file by path across all backups
- File history shows all versions with timestamps
- Can view diff between two file versions
- Can download specific file version
- History grouped by device for clarity

### Implementation Tasks

- [ ] T153 [US5] Implement GET /backups/files/history endpoint in BackupsController with deviceName, filePath query parameters per openapi.yaml
- [ ] T154 [US5] Implement ResticService.GetFileHistory to query file across all backups using restic find and restic ls
- [ ] T155 [US5] Implement file version diff logic in BackupsController comparing two file versions by hash
- [ ] T156 [US5] Create FileHistory page in src/frontend/src/pages/FileHistory.tsx with search input and timeline view
- [ ] T157 [US5] Create FileVersionTimeline component in src/frontend/src/components/files/FileVersionTimeline.tsx displaying versions chronologically
- [ ] T158 [US5] Create FileVersionDiff component in src/frontend/src/components/files/FileVersionDiff.tsx showing side-by-side diff
- [ ] T159 [US5] Add file search autocomplete in FileHistory page querying backend for file paths
- [ ] T160 [US5] Implement GET /backups/{id}/files/{path}/diff endpoint in BackupsController for file content diff
- [ ] T161 [US5] Create end-to-end file history test in BackupChrono.Integration.Tests/FileHistoryTests.cs verifying version tracking

**Checkpoint**: Can search for file, view version history, compare versions, download specific version via UI.

---

## Phase 12: User Story 10 - Multi-User Access Management (P5)

**Story Goal**: As a homelab admin, I want to control access with user roles, so multiple admins can manage backups safely.

**Effort Estimate**: 4-5 days

**Independent Test Criteria**:
- Can create users with roles (Admin, Operator, Viewer)
- Admins can manage devices, shares, and configuration
- Operators can trigger backups and restores but not edit config
- Viewers can browse backups and download files only
- Authentication required for all API endpoints

### Implementation Tasks

- [ ] T162 [P] [US10] Create User entity in BackupChrono.Core/Entities/User.cs with username, passwordHash, role fields
- [ ] T163 [P] [US10] Create UserRole enum in BackupChrono.Core/Entities/UserRole.cs (Admin, Operator, Viewer)
- [ ] T164 [P] [US10] Create IAuthService interface in BackupChrono.Core/Interfaces/IAuthService.cs with Login, CreateUser, ValidateToken methods
- [ ] T165 [US10] Implement AuthService in src/backend/BackupChrono.Infrastructure/Auth/AuthService.cs using JWT tokens and BCrypt password hashing
- [ ] T166 [US10] Implement user storage in YAML files at config/users/{username}.yaml with encrypted passwords
- [ ] T167 [US10] Create AuthController in BackupChrono.Api/Controllers/AuthController.cs with POST /auth/login, POST /auth/register endpoints
- [ ] T168 [US10] Implement JWT authentication middleware in BackupChrono.Api/Middleware/JwtAuthMiddleware.cs
- [ ] T169 [US10] Add [Authorize] attributes to controllers with role-based restrictions (Admin, Operator, Viewer)
- [ ] T170 [US10] Implement authorization policies in BackupChrono.Api/Program.cs for role-based access control
- [ ] T171 [US10] Create Login page in src/frontend/src/pages/Login.tsx with form for username/password
- [ ] T172 [US10] Implement auth context in src/frontend/src/context/AuthContext.tsx for storing JWT token
- [ ] T173 [US10] Add axios interceptor in src/frontend/src/services/api.ts to include JWT token in requests
- [ ] T174 [US10] Add role-based UI visibility in frontend (hide admin features for Operators/Viewers)
- [ ] T175 [US10] Create AuthServiceTests in BackupChrono.Infrastructure.Tests/Auth/AuthServiceTests.cs for login, registration, token validation
- [ ] T176 [US10] Create authorization integration test in BackupChrono.Api.Tests/AuthorizationTests.cs verifying role-based access

**Checkpoint**: Can create users, login returns JWT, role-based access enforced on API endpoints, UI adapts to user role.

---

## Phase 13: Polish & Cross-Cutting Concerns

**Goal**: Final polish, advanced features, and comprehensive documentation.

**Effort Estimate**: 3-4 days

### Tasks

- [ ] T177 [P] Implement ResticRetentionTests in BackupChrono.Infrastructure.Restic.Tests/ResticRetentionTests.cs verifying retention policy application
- [ ] T178 [P] Implement ResticService.Prune for garbage collection and space reclamation
- [ ] T179 [P] Implement ResticService.VerifyIntegrity for repository integrity checks
- [ ] T183 [P] Implement skip-if-running logic in BackupJob to prevent schedule overlaps
- [ ] T184 [P] Add proper credential encryption/decryption in GitConfigService using repository passphrase
- [ ] T185 [P] Create API documentation in docs/api/ using Swagger UI hosted at /swagger
- [ ] T186 [P] Create deployment guide in docs/deployment/ with Docker setup, reverse proxy config, TLS termination
- [ ] T187 [P] Create plugin development guide in src/plugins/README.md for IProtocolPlugin implementation
- [ ] T188 [P] Add Prometheus metrics endpoint at /metrics (optional observability)
- [ ] T189 [P] Create backup operation logging to logs/backup-jobs.jsonl in JSON Lines format
- [ ] T190 [P] Create restore operation logging to logs/restore-jobs.jsonl
- [ ] T191 [P] Implement log viewer in frontend at Configuration page for backup-jobs.jsonl and restore-jobs.jsonl
- [ ] T192 [P] Add database-free verification tests in BackupChrono.Integration.Tests/ confirming no database dependency
- [ ] T193 [P] Create repository format version validation in ResticService rejecting incompatible versions
- [ ] T194 [P] Implement repository migration tool in src/backend/BackupChrono.Infrastructure/Restic/RepositoryMigration.cs for format upgrades
- [ ] T196 [P] Create performance tests in BackupChrono.Performance.Tests/ for 1M+ files, 10+ concurrent backups
- [ ] T197 Finalize README.md with complete setup instructions, architecture diagrams, contribution guidelines
- [ ] T198 Create CHANGELOG.md documenting feature implementation and version history
- [ ] T199 Add .editorconfig for consistent code formatting

**Checkpoint**: All production features implemented, tests passing, documentation complete, Docker images published.

---

## Dependencies & Execution Order

### User Story Dependency Graph

```text
Setup (Phase 1)
  ↓
Foundational (Phase 2) ← BLOCKING for all user stories
  ↓
  ├─→ US1: Automated Backups (P1) ← MVP START
  │     ↓
  ├─→ US2: Restore Files (P2) ← MVP END (depends on US1 for backups to exist)
  │
  ├─→ US9: Git Config (P2) ← Independent (foundational only)
  │
  ├─→ US6: Rsync Protocol (P2) ← Independent (foundational only)
  │
  ├─→ US3: Monitor Health (P3) ← Depends on US1 (needs backup jobs to monitor)
  │
  ├─→ US7: Email Notifications (P3) ← Depends on US1 (needs backup completion events)
  │
  ├─→ US8: Direct Restore (P3) ← Depends on US2 (extends restore capability)
  │
  ├─→ US4: Configure via UI (P4) ← Independent (frontend work)
  │
  ├─→ US5: File History (P5) ← Depends on US2 (extends backup browsing)
  │
  └─→ US10: Multi-User (P5) ← Independent (auth layer)
```

### Parallel Execution Opportunities

**Phase 1 (Setup)**: Tasks T002-T006, T008, T012-T013 can run in parallel (independent file creation).

**Phase 2 (Foundational)**: Tasks T015-T034 (entity/interface creation), T039-T041 (protocol plugins) can run in parallel.

**User Story Implementation**: After foundational phase completes:
- **Parallel Track 1**: US1 + US9 (backend work, different services)
- **Parallel Track 2**: US6 (protocol plugin work)
- **Parallel Track 3**: US4 (frontend work, independent)

After US1 completes:
- **Parallel Track 1**: US2 + US3 + US7 (all extend US1 functionality)
- **Parallel Track 2**: US10 (auth layer, independent)

After US2 completes:
- **Parallel Track 1**: US5 + US8 (both extend restore/browse capabilities)

**Phase 13 (Polish)**: Most tasks marked `[P]` can run in parallel.

---

## Implementation Notes

### MVP Scope (Recommended First Delivery)

**Include**:
- Phase 1: Setup ✅
- Phase 2: Foundational ✅
- Phase 3: US1 - Automated Backups ✅
- Phase 4: US2 - Restore Files ✅

**MVP Deliverable**: Functional backup system that automatically backs up SMB/SSH devices and allows file restoration via API. Configuration managed via YAML files. ~67 tasks.

**Post-MVP Increments**:
1. **Config Management**: US9 (Git versioning) - 11 tasks
2. **Protocol Support**: US6 (Rsync) - 8 tasks
3. **Observability**: US3 (Dashboard) + US7 (Email) - 19 tasks
4. **Advanced Restore**: US8 (Restore-to-source) - 9 tasks
5. **UI**: US4 (Web UI) - 24 tasks
6. **Advanced Features**: US5 (File History) + US10 (Multi-User) - 25 tasks
7. **Production**: Phase 13 (Polish) - 24 tasks

### Test Coverage Requirements

- **Unit Tests**: All service classes, value objects, entities
- **Integration Tests**: Restic operations (backup, restore, retention, integrity)
- **API Tests**: All controllers with mock services
- **E2E Tests**: Complete workflows (backup-flow, restore-flow, configuration)
- **Performance Tests**: 1M+ files, 10+ concurrent backups, repository growth

### Configuration Management

**Note**: Configuration files are stored in the `config/` volume mount at runtime (not in source repository):

- **Global**: `config/global.yaml`
- **Devices**: `config/devices/{device-name}.yaml`
- **Shares**: `config/shares/{device-name}/{share-name}.yaml`
- **Notifications**: `config/notifications.yaml`
- **Users**: `config/users/{username}.yaml` (encrypted passwords)

### Restic Integration Strategy

- **Hard-coded dependency**: No abstraction layer (per constitution)
- **Process spawning**: ResticClient spawns restic binary
- **JSON parsing**: All restic output parsed from stdout
- **Repository**: Single repository for all devices (global deduplication)
- **Retention**: Applied per-device after each backup

---

## Summary

- **Total Tasks**: 200
- **Setup Tasks**: 15 (Phase 1) - includes CI/CD setup
- **Foundational Tasks**: 36 (Phase 2)
- **User Story Tasks**: 130 (Phases 3-12)
  - US1: 21 tasks (includes production-critical monitoring)
  - US2: 12 tasks
  - US9: 11 tasks
  - US6: 8 tasks
  - US3: 10 tasks
  - US7: 11 tasks
  - US8: 9 tasks
  - US4: 24 tasks
  - US5: 9 tasks
  - US10: 15 tasks
- **Polish Tasks**: 19 (Phase 13)

**Parallel Opportunities**: 78 tasks marked `[P]` (39% of total tasks).

**MVP Scope**: Phases 1-4 (72 tasks) deliver production-ready automated backups and file restoration with monitoring.

**Independent Testing**: Each user story phase has clear test criteria and checkpoint verification before proceeding to next story.

## Effort Estimates

### Total Project Effort
**Sequential**: 55-68 days (11-14 weeks) for single developer  
**Parallel (2 developers)**: 32-40 days (6-8 weeks) with optimal task distribution  
**Parallel (3+ developers)**: 24-30 days (5-6 weeks) with parallelized Phase 2 and user stories

### By Phase
- **Phase 1 (Setup)**: 2-4 days (includes CI/CD pipeline)
- **Phase 2 (Foundational)**: 10-12 days ⚠️ BLOCKING - Must complete before user stories
- **Phase 3 (US1 - Automated Backups)**: 7-9 days 🎯 MVP (includes production monitoring)
- **Phase 4 (US2 - Restore Files)**: 4-5 days 🎯 MVP
- **Phase 5 (US9 - Git Config)**: 3-4 days
- **Phase 6 (US6 - Rsync)**: 3-4 days
- **Phase 7 (US3 - Monitor Health)**: 3-4 days
- **Phase 8 (US7 - Email Notifications)**: 3-4 days
- **Phase 9 (US8 - Direct Restore)**: 2-3 days
- **Phase 10 (US4 - Web UI)**: 8-10 days
- **Phase 11 (US5 - File History)**: 3-4 days
- **Phase 12 (US10 - Multi-User)**: 4-5 days
- **Phase 13 (Polish)**: 3-4 days

### MVP Effort
**Phases 1-4**: 23-30 days (4.5-6 weeks) for single developer (production-ready with CI/CD and monitoring)  
**Parallel (2 developers)**: 15-19 days (3-4 weeks) with one on backend, one on testing/integration

### Post-MVP Increments
- **Config Management (US9)**: +3-4 days
- **Protocol Support (US6)**: +3-4 days
- **Observability (US3 + US7)**: +6-8 days
- **Advanced Features (US8 + US4 + US5 + US10)**: +17-22 days
- **Polish (Phase 13)**: +3-4 days

### Assumptions
- Estimates assume experienced developers familiar with ASP.NET Core, React, and Docker
- Restic integration complexity is moderate (well-documented tool)
- Protocol plugins (SMB, SSH, Rsync) have existing libraries
- Testing time included in estimates (unit, integration, e2e)
- No major architectural changes during implementation
- 6-hour productive coding days (actual work time, not calendar time)
