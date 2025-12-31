# BackupChrono

**Version-controlled backup orchestration system using Restic and Git.**

## Overview

BackupChrono is a distributed backup management solution that combines Restic's powerful backup engine with Git-based configuration management. It enables teams to manage backup jobs as code, track changes over time, and automate backups across multiple devices and storage backends.

### Key Features

- **Git-based Configuration**: All backup job definitions stored in Git for versioning, auditing, and collaboration
- **Restic Integration**: Leverages Restic for deduplication, encryption, and efficient incremental backups
- **Multi-Protocol Support**: SMB, SFTP, local paths, and plugin-based extensibility
- **Scheduled Backups**: Cron-like scheduling with Quartz.NET for reliable execution
- **Real-time Monitoring**: SignalR WebSocket updates for backup progress tracking
- **RESTful API**: Complete API for integration with external systems
- **Docker Deployment**: Production-ready containerized deployment

## Quick Start (5 minutes)

### Prerequisites

- Docker and Docker Compose installed
- Git installed
- 2GB RAM minimum
- 10GB disk space for repository

### 1. Clone and Configure

```bash
git clone <repository-url> BackupChrono
cd BackupChrono

# Set Restic password
echo "YourSecurePasswordHere" > docker/secrets/restic_password.txt
chmod 600 docker/secrets/restic_password.txt
```

### 2. Start Services

```bash
cd docker
docker-compose up -d
```

### 3. Verify Installation

```bash
# Check API health
curl http://localhost:5000/health

# View logs
docker-compose logs -f backupchrono-api
```

Access the API documentation at: http://localhost:5000/swagger

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     BackupChrono System                     │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────┐      ┌──────────────────┐      ┌────────┐ │
│  │   API       │◄────►│  Infrastructure  │◄────►│ Restic │ │
│  │ (REST/WS)   │      │  (Git, Protocols)│      │        │ │
│  └─────────────┘      └──────────────────┘      └────────┘ │
│        │                      │                      │      │
│        ▼                      ▼                      ▼      │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              Core Domain Layer                      │   │
│  │  (Entities, Value Objects, Interfaces)              │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
         │                      │                      │
         ▼                      ▼                      ▼
   [Git Repo]            [SMB/SFTP Shares]        [Restic Repo]
   /config                                        /repository
```

### Technology Stack

- **Backend**: ASP.NET Core 8.0 (C#)
- **Backup Engine**: Restic v0.17.3
- **Configuration Store**: Git (LibGit2Sharp)
- **Scheduler**: Quartz.NET
- **Protocols**: SSH.NET (SFTP), SMBLibrary (SMB)
- **Real-time**: SignalR
- **Containerization**: Docker
- **Testing**: xUnit, Moq, FluentAssertions, Testcontainers

## Development Setup

### Build from Source

```bash
# Navigate to backend
cd src/backend

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test

# Run API locally
cd BackupChrono.Api
dotnet run
```

### Project Structure

```
BackupChrono/
├── src/
│   ├── backend/
│   │   ├── BackupChrono.Core/          # Domain models, interfaces
│   │   ├── BackupChrono.Infrastructure/ # Restic, Git, protocols
│   │   ├── BackupChrono.Api/           # REST API, SignalR hubs
│   │   ├── BackupChrono.UnitTests/     # Fast unit tests (Core+Infrastructure+Api)
│   │   ├── BackupChrono.Infrastructure.Restic.Tests/  # Restic-specific tests (Docker)
│   │   └── BackupChrono.IntegrationTests/  # E2E tests (full stack)
│   ├── frontend/                       # React 18 frontend (coming soon)
│   └── plugins/                        # Protocol plugins
├── docker/
│   ├── Dockerfile.backend
│   ├── Dockerfile.frontend
│   ├── docker-compose.yml              # Development
│   └── docker-compose.prod.yml         # Production
├── docs/                               # Architecture documentation
├── specs/                              # Feature specifications
└── README.md
```

## Docker Deployment

### Development Mode

```bash
cd docker
docker-compose up -d
```

- API: http://localhost:5000
- Frontend: http://localhost:8080
- Config volume: `./config`
- Repository volume: `./repository`

### Production Mode

**Prerequisites:**
Before running production mode, create the required directories on the host:

```bash
# Linux/macOS
sudo mkdir -p /var/backupchrono/config /var/backupchrono/repository
sudo chown -R 1000:1000 /var/backupchrono  # Match container user

# Windows (using WSL2 for Docker Desktop)
mkdir -p /mnt/c/backupchrono/config /mnt/c/backupchrono/repository
# Then update docker-compose.prod.yml volumes to use Windows paths
```

**Start production deployment:**
```bash
cd docker
docker-compose -f docker-compose.prod.yml up -d
```

**Production configuration includes:**
- Health checks
- Resource limits (2 CPU, 4GB RAM)
- Secret management
- Structured logging
- Auto-restart policies

**Volume Mappings:**
```yaml
volumes:
  - /var/backupchrono/config:/config       # Git configuration repository
  - /var/backupchrono/repository:/repository # Restic backup data
```

**Notes:**
- `/var/backupchrono/config`: Stores Git-versioned backup job configurations (~100MB)
- `/var/backupchrono/repository`: Stores Restic backup data (size depends on backups)
- Both directories must exist before starting containers
- Ensure proper permissions for Docker to read/write these paths
- For Windows: Adjust paths in docker-compose.prod.yml or use WSL2 paths

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Development/Production | Development |
| `GIT_CONFIG_PATH` | Git repository path | /config |
| `RESTIC_REPOSITORY` | Restic backup storage | /repository |
| `RESTIC_PASSWORD_FILE` | Restic encryption password file | /app/restic-password |

### Restic Password

**Development:**
```bash
export RESTIC_PASSWORD=your-secure-password
```

**Production:**
```bash
# Create secret file
echo "your-secure-password" > docker/secrets/restic_password.txt
chmod 600 docker/secrets/restic_password.txt
```

## API Documentation

Once running, access interactive API documentation:
- Swagger UI: http://localhost:5000/swagger
- OpenAPI spec: http://localhost:5000/swagger/v1/swagger.json

### Example API Calls

```bash
# Create a backup job
curl -X POST http://localhost:5000/api/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Daily Documents Backup",
    "deviceId": "device-001",
    "shareId": "share-001",
    "schedule": "0 2 * * *",
    "retention": {
      "keepDaily": 7,
      "keepWeekly": 4,
      "keepMonthly": 12
    }
  }'

# Trigger immediate backup
curl -X POST http://localhost:5000/api/backups/trigger/{jobId}

# Get backup status
curl http://localhost:5000/api/backups/{backupId}
```

## Monitoring & Logs

```bash
# View API logs
docker-compose logs -f backupchrono-api

# Check health endpoint
curl http://localhost:5000/health

# Monitor real-time backup progress (WebSocket)
# Connect to: ws://localhost:5000/hubs/backup
```

## Testing

```bash
# Run all tests
dotnet test

# Run fast unit tests only (< 10 seconds)
dotnet test BackupChrono.UnitTests/

# Run Restic tests (requires Docker, ~30 seconds)
dotnet test BackupChrono.Infrastructure.Restic.Tests/

# Run integration tests (requires Docker, ~60 seconds)
dotnet test BackupChrono.IntegrationTests/

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportFormat=opencover
```

## Roadmap

### Phase 1: MVP (Completed)
- ✅ ASP.NET Core solution structure
- ✅ Docker deployment
- ✅ NuGet dependencies
- ✅ CI/CD pipeline

### Phase 2: Core Features (In Progress)
- Domain entities and value objects
- Restic integration
- Git configuration management
- Protocol plugins (SMB, SFTP)

### Phase 3: Scheduling & Automation
- Quartz.NET scheduler
- Backup job execution
- Real-time progress tracking (SignalR)

### Phase 4: Frontend
- React 18 dashboard
- Job management UI
- Backup history viewer

See [tasks.md](specs/001-backup-system/tasks.md) for complete implementation plan.

## Contributing

This project follows a specification-driven development approach:
1. Features defined in `specs/001-backup-system/`
2. Implementation tracked in `tasks.md`
3. All changes require passing tests and CI/CD validation

## License

See [LICENSE](LICENSE) file for details.

## Support

- Documentation: [docs/](docs/)
- Architecture: [specs/001-backup-system/plan.md](specs/001-backup-system/plan.md)
- Issues: (GitHub Issues URL - to be added)

---

**Note**: This is an active development project. Production deployment requires completed security hardening and comprehensive testing.
