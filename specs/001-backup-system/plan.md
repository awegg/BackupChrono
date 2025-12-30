# Implementation Plan: BackupChrono - Central Pull Backup System

**Branch**: `001-backup-system` | **Date**: 2025-12-30 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-backup-system/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

BackupChrono is a modern, declarative, central-pull backup system designed for homelab environments with 3-10 devices. The system features a device-centric organizational model with configuration cascade (Global → Device → Share), Git-versioned configuration, and a modern web UI. The primary technical approach leverages ASP.NET Core backend with React frontend, orchestrating mature backup engines (restic/kopia/borg) for deduplication and storage, deployed exclusively via Docker containers.

## Technical Context

**Language/Version**: C# / .NET 8.0 (ASP.NET Core), JavaScript/TypeScript (React 18+)
**Primary Dependencies**: 
- Backend: ASP.NET Core 8.0, LibGit2Sharp (Git operations), SSH.NET (SSH protocol), SMBLibrary (SMB protocol), Quartz.NET (scheduling), YamlDotNet (YAML serialization), SignalR (real-time updates)
- Frontend: React 18, React Router, Tailwind CSS, React Query (state management), Axios (HTTP client)
- Backup Engine: Restic (hard-coded integration). Direct process spawning with JSON output parsing. Protocol plugins (SMB, SSH, NFS) for extensibility.
- Container: Docker, docker-compose

**Storage**: 
- Configuration: Git repository (YAML files for device/share config)
- Backup Repository: Pluggable backend via chosen backup engine (local filesystem, S3-compatible)
- No application database required (FR-066)

**Testing**: 
- Backend: xUnit, Moq, FluentAssertions
- Frontend: Vitest, React Testing Library
- Integration: TestContainers for Docker-based tests

**Target Platform**: Linux containers (Docker), accessible via web browser (Chrome, Firefox, Safari, Edge)

**Project Type**: Web application (ASP.NET Core backend + React frontend)

**Performance Goals**: 
- Web UI: <200ms page load time, 60 FPS animations
- Backup operations: Support 1000+ small files/sec or 100MB/sec sustained throughput (whichever is lower, typical SMB share performance)
- Concurrent backups: 3-5 simultaneous device backups
- API response: <100ms for configuration read operations

**Constraints**: 
- Docker-only deployment (FR-064)
- No central database (FR-066)
- Must leverage existing backup engines (FR-065)
- Configuration must be Git-versionable (FR-062)
- Support SMB, SSH, and rsync protocols (FR-006-008)

**Security**:
- Device credentials: Encrypted in Git repo using repository passphrase (FR-074)
- Web UI authentication: Docker secrets-based basic auth (v1.0), OIDC planned (v2.0)
- TLS/HTTPS: Optional nginx reverse proxy (FR-072)

**Observability**:
- Logging: Serilog with structured logging to stdout (Docker logs)
- Metrics: Prometheus endpoint for backup success/failure rates (optional, P3)
- Healthcheck: Docker HEALTHCHECK endpoint for container orchestration
- Monitoring: Backup job status via SignalR real-time updates

**Scale/Scope**: 
- Target: 3-10 devices, 12-50 shares
- Backup repository: 1-10TB total storage
- Configuration repository: ~50-500 files (all devices/shares), Git repo <10MB
- Concurrent users: Single-user default (P1), multi-user P5
- Configuration complexity: <100 YAML lines per device

### Dependencies & External Components

- **Backup Engine**: Restic (hard-coded integration via ResticService class). Direct process spawning with JSON output parsing. Unlikely to change in homelab context.
- **Protocol Plugins**: IProtocolPlugin interface for extensible source protocols (SMB, SSH, Rsync, NFS, iSCSI, etc.). High probability of community contributions.
- **Git Library**: LibGit2Sharp for configuration versioning (FR-062-063)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Status**: Constitution template is empty/placeholder - no specific constitutional principles defined for this project yet.

**Evaluation**: No gates to check at this time. Proceeding with standard best practices:
- Test-driven development for core business logic
- Clear separation of concerns (backend API, frontend UI, backup orchestration)
- Minimal dependencies (leverage existing mature tools)
- Docker-based deployment only

**Re-evaluation Required**: After Phase 1 design, verify:
- No more than 3 C# projects (Core, Infrastructure, Api)
- No pattern over-engineering (Repository, UoW, CQRS, Mediator)
- Protocol plugin interface complexity (max 5 methods)
- Restic integration abstraction level (direct process spawn vs wrapper library)
- Git integration patterns (LibGit2Sharp implementation details)
- Security credential storage approach

**Phase 1 Re-evaluation** (2025-12-30):

✅ **3 C# Projects Rule**: Compliant
- BackupChrono.Core (domain entities, interfaces)
- BackupChrono.Infrastructure (restic, protocols, git, scheduling)
- BackupChrono.Api (controllers, SignalR hubs)
- Test projects excluded from count (4 test projects)

✅ **No Pattern Over-engineering**: Compliant
- No Repository pattern (Git is the repository, restic manages backups)
- No Unit of Work (no database transactions)
- No CQRS (simple CRUD operations)
- No Mediator (direct service injection)
- Service layer only: DeviceService, BackupOrchestrator, SchedulingService

✅ **Protocol Plugin Interface**: Compliant (4 methods - under 5 limit)
```csharp
public interface IProtocolPlugin
{
    Task<bool> TestConnection(Device device);
    Task<string> MountShare(Device device, Share share);
    Task UnmountShare(string mountPath);
    Task WakeDevice(Device device);
}
```

✅ **Restic Integration**: Compliant (direct process spawning)
- ResticService spawns restic as subprocess
- JSON output parsed for progress/status
- No wrapper library overhead
- See: data-model.md IResticService interface

✅ **Git Integration**: Compliant (LibGit2Sharp direct usage)
- GitConfigService uses LibGit2Sharp for commits/history
- Auto-commit on config changes
- Rollback via git reset
- No custom abstraction layer

✅ **Security Credential Storage**: Compliant
- Device passwords encrypted in YAML using repository passphrase
- RESTIC_PASSWORD via environment variable (Docker secrets)
- SMTP credentials in env vars (not committed to Git)
- No plaintext credentials in configuration files

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── BackupChrono.Api/              # ASP.NET Core Web API
│   │   ├── Controllers/               # REST API endpoints
│   │   ├── Hubs/                      # SignalR hubs for real-time updates
│   │   ├── Middleware/                # Auth, logging, error handling
│   │   └── Program.cs                 # Entry point
│   ├── BackupChrono.Core/             # Domain models and interfaces
│   │   ├── Entities/                  # Device, Share, Snapshot, etc.
│   │   ├── Interfaces/                # IProtocolPlugin, service interfaces
│   │   │   ├── IProtocolPlugin.cs     # Protocol plugin interface
│   │   │   ├── IDeviceService.cs      # Service interfaces
│   │   │   └── IBackupOrchestrator.cs
│   │   ├── Services/                  # Service interfaces
│   │   └── ValueObjects/              # Schedule, RetentionPolicy, etc.
│   └── BackupChrono.Infrastructure/   # External integrations
│       ├── Git/                       # LibGit2Sharp config management
│       ├── Restic/                    # Direct Restic integration
│       │   ├── ResticService.cs       # Main restic wrapper
│       │   ├── ResticClient.cs        # Process spawning
│       │   └── JsonParsers.cs         # Parse restic JSON output
│       ├── Protocols/                 # Protocol plugin implementations
│       │   ├── SmbProtocolPlugin.cs   # SMBLibrary integration
│       │   ├── SshProtocolPlugin.cs   # SSH.NET integration
│       │   ├── RsyncProtocolPlugin.cs # Rsync spawning
│       │   └── ProtocolPluginLoader.cs# Plugin discovery
│       └── Scheduling/                # Quartz.NET job scheduling
└── tests/
    ├── BackupChrono.Core.Tests/               # Unit tests for domain logic
    ├── BackupChrono.Infrastructure.Tests/     # Infrastructure unit tests
    │   ├── Git/                               # Git service tests
    │   └── Protocols/                         # Protocol plugin tests
    ├── BackupChrono.Infrastructure.Restic.Tests/ # Restic integration tests (CRITICAL)
    │   ├── ResticVersionTests.cs              # Test restic version compatibility
    │   ├── ResticBackupTests.cs               # Test backup operations
    │   ├── ResticRestoreTests.cs              # Test restore operations
    │   ├── ResticRetentionTests.cs            # Test retention policies
    │   └── ResticErrorHandlingTests.cs        # Test error scenarios
    ├── BackupChrono.Api.Tests/                # API integration tests
    └── BackupChrono.Integration.Tests/        # End-to-end with TestContainers

frontend/
├── src/
│   ├── components/                    # Reusable React components
│   │   ├── common/                    # Buttons, inputs, tables
│   │   ├── devices/                   # Device cards, device table
│   │   └── layout/                    # Sidebar, header, navigation
│   ├── pages/                         # Route-level components
│   │   ├── Dashboard.tsx
│   │   ├── DeviceDetail.tsx
│   │   ├── DeviceSettings.tsx
│   │   ├── ShareSettings.tsx
│   │   ├── Configuration.tsx
│   │   ├── Notifications.tsx
│   │   └── Browse.tsx
│   ├── services/                      # API clients
│   │   ├── api.ts                     # Axios instance
│   │   ├── deviceService.ts
│   │   ├── backupService.ts
│   │   └── configService.ts
│   ├── hooks/                         # Custom React hooks
│   ├── types/                         # TypeScript interfaces
│   └── App.tsx
└── tests/
    ├── unit/                          # Vitest + React Testing Library
    │   ├── components/                # Component tests
    │   └── hooks/                     # Custom hook tests
    └── e2e/                           # Playwright end-to-end tests
        ├── backup-flow.spec.ts        # Full backup workflow
        ├── restore-flow.spec.ts       # File restore workflow
        └── configuration.spec.ts      # Config management

docker/
├── Dockerfile.backend                 # .NET 8.0 runtime + restic binary (bundled)
├── Dockerfile.frontend                # nginx serving React build
├── docker-compose.yml                 # Local development
└── docker-compose.prod.yml            # Production deployment

config/
└── .gitkeep                          # Git repo for config files (volume mount)
                                      # Structure: devices/*.yaml, shares/*/*.yaml

repository/
└── .gitkeep                          # Restic repository (volume mount)
                                      # Initialized on first run (restic init)
                                      # Structure managed by restic (snapshots, index, keys, data)

plugins/
├── README.md                         # Plugin development guide
└── community/                        # Community protocol plugin DLLs
    └── .gitkeep                      # Example: NfsProtocolPlugin.dll

docs/
├── api/                              # OpenAPI/Swagger specs
├── architecture/                     # Architecture decision records
└── deployment/                       # Docker deployment guides
```

**Structure Decision**: Web application structure with backend/frontend separation. ASP.NET Core backend provides RESTful API and SignalR for real-time backup status updates. React frontend provides modern, responsive UI. Clear separation allows independent scaling and testing of each tier. Docker Compose orchestrates multi-container deployment matching the spec's Docker-only requirement (FR-064).

## Complexity Tracking

**Status**: ✅ No constitutional violations. Architecture follows simplicity principles:

- **3 projects**: Core (domain), Infrastructure (implementations), Api (web layer)
- **No unnecessary abstractions**: Direct restic integration, no Repository/UoW/CQRS/Mediator
- **Plugin architecture only where needed**: Protocol plugins for source extensibility
- **Minimal dependencies**: Leverage mature libraries (restic, LibGit2Sharp, Quartz.NET)
- **Database-free**: Git for configuration, restic for backup data (FR-066)

> If violations arise during Phase 1-2, document them here with justification.
