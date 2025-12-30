# Quickstart Guide: BackupChrono

**Last Updated**: 2025-12-30  
**Target Audience**: Developers setting up local development environment

## Prerequisites

### Required Software
- **Docker Desktop**: 4.25+ (with Docker Compose V2)
- **Git**: 2.40+
- **.NET SDK**: 8.0+ (for backend development)
- **Node.js**: 20+ (for frontend development)
- **PowerShell**: 7.4+ (Windows) or Bash (Linux/macOS)

### Recommended IDE
- **Visual Studio Code** with extensions:
  - C# Dev Kit
  - Docker
  - REST Client
  - YAML
- **Visual Studio 2022** (17.8+) - alternative for backend
- **Rider** (2023.3+) - alternative for backend

### System Requirements
- **RAM**: 8GB minimum, 16GB recommended
- **Disk Space**: 10GB for Docker images + development tools
- **Network**: Internet access for pulling Docker images and NuGet/npm packages

---

## Quick Start (5 Minutes)

```powershell
# Clone repository
git clone https://github.com/yourusername/BackupChrono.git
cd BackupChrono

# Start development environment
docker-compose up -d

# Initialize Git configuration repository
docker exec backupchrono-api restic init

# Access web UI
# http://localhost:3000
```

---

## Detailed Setup

### 1. Clone Repository

```bash
git clone https://github.com/yourusername/BackupChrono.git
cd BackupChrono
```

### 2. Configure Environment Variables

Create `.env` file in repository root:

```bash
# Backend Configuration
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:5000

# Restic Repository
RESTIC_REPOSITORY=/repository
RESTIC_PASSWORD=your-strong-password-here

# Git Configuration Repository
GIT_CONFIG_PATH=/config

# SMTP (Optional - for email notifications)
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USERNAME=your-email@gmail.com
SMTP_PASSWORD=your-app-password
SMTP_FROM=backupchrono@example.com

# SignalR
SIGNALR_HUB_URL=http://localhost:5000/hubs/backup

# Logging
SERILOG__MinimumLevel=Debug
```

### 3. Initialize Configuration Repository

The configuration repository is a Git repo that stores device/share configurations.

```powershell
# Create config directory
mkdir -p config

# Initialize as Git repository
cd config
git init
git config user.name "BackupChrono System"
git config user.email "system@backupchrono.local"

# Create initial global configuration
@"
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
  exclude_if_present:
    - ".nobackup"

repository:
  path: "/repository"
  password_env: "RESTIC_PASSWORD"
"@ | Out-File -FilePath global.yaml -Encoding UTF8

# Commit initial config
git add global.yaml
git commit -m "Initial global configuration"

cd ..
```

### 4. Initialize Backup Repository

The backup repository is where restic stores backup data.

```powershell
# Create repository directory
mkdir -p repository

# Initialize restic repository
docker run --rm \
  -v ${PWD}/repository:/repository \
  -e RESTIC_REPOSITORY=/repository \
  -e RESTIC_PASSWORD=your-strong-password-here \
  restic/restic:0.16.5 init

# Verify initialization
docker run --rm \
  -v ${PWD}/repository:/repository \
  -e RESTIC_REPOSITORY=/repository \
  -e RESTIC_PASSWORD=your-strong-password-here \
  restic/restic:0.16.5 snapshots
```

### 5. Start Development Environment

#### Using Docker Compose (Recommended)

```bash
# Start all services (backend, frontend, database)
docker-compose up -d

# View logs
docker-compose logs -f

# Check service status
docker-compose ps

# Stop services
docker-compose down
```

#### Manual Backend Development

```bash
# Navigate to backend
cd backend/src/BackupChrono.Api

# Restore NuGet packages
dotnet restore

# Run migrations (if database used in future)
# dotnet ef database update

# Run backend API
dotnet run

# API will be available at http://localhost:5000
# Swagger UI at http://localhost:5000/swagger
```

#### Manual Frontend Development

```bash
# Navigate to frontend
cd frontend

# Install npm packages
npm install

# Run development server
npm run dev

# Frontend will be available at http://localhost:3000
```

### 6. Verify Installation

#### Check Backend Health

```bash
# Health check endpoint
curl http://localhost:5000/health

# Expected response: {"status":"Healthy"}
```

#### Check API Endpoints

```bash
# List devices (should be empty initially)
curl http://localhost:5000/api/devices

# Expected response: []
```

#### Check Frontend

Open browser to `http://localhost:3000`

You should see the BackupChrono dashboard with:
- Empty device list
- "Add Device" button
- Navigation sidebar (Dashboard, Configuration, Notifications)

---

## First Backup

### 1. Add Test Device

Create a test SMB device configuration:

```powershell
# Create device configuration file
mkdir -p config/devices

@"
id: "$(New-Guid)"
name: "test-device"
protocol: "SMB"
host: "192.168.1.100"
port: 445
username: "testuser"
password: "<encrypted-later>"
wake_on_lan_enabled: false

schedule:
  cron_expression: "0 */1 * * *"  # Every hour for testing

retention_policy:
  keep_latest: 3

created_at: "$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')"
updated_at: "$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')"
"@ | Out-File -FilePath config/devices/test-device.yaml -Encoding UTF8

# Commit to Git
cd config
git add devices/test-device.yaml
git commit -m "Added test device"
cd ..
```

### 2. Add Test Share

```powershell
# Create share configuration file
mkdir -p config/shares/test-device

@"
id: "$(New-Guid)"
device_id: "<device-id-from-above>"
name: "test-share"
path: "/test-data"
enabled: true

created_at: "$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')"
updated_at: "$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')"
"@ | Out-File -FilePath config/shares/test-device/test-share.yaml -Encoding UTF8

# Commit to Git
cd config
git add shares/test-device/test-share.yaml
git commit -m "Added test share"
cd ..
```

### 3. Trigger Manual Backup

Using the Web UI:
1. Navigate to http://localhost:3000
2. Click on "test-device" in dashboard
3. Click "Backup Now" button
4. Watch real-time progress via SignalR updates

Using API:
```bash
# Trigger manual backup
curl -X POST http://localhost:5000/api/backup-jobs \
  -H "Content-Type: application/json" \
  -d '{"deviceId": "<device-id>", "shareId": "<share-id>"}'

# Check backup job status
curl http://localhost:5000/api/backup-jobs/<job-id>
```

### 4. Browse Backup

```bash
# List backups for device
curl http://localhost:5000/api/devices/<device-id>/backups

# Browse files in latest backup
curl "http://localhost:5000/api/backups/<backup-id>/files?path=/"

# View file history
curl "http://localhost:5000/api/backups/files/history?deviceId=<device-id>&filePath=/test-data/file.txt"
```

---

## Running Tests

### Backend Tests

```bash
cd backend

# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportsDirectory=./coverage

# Run specific test project
dotnet test tests/BackupChrono.Core.Tests

# Run Restic integration tests (requires restic installed)
dotnet test tests/BackupChrono.Infrastructure.Restic.Tests
```

### Frontend Tests

```bash
cd frontend

# Run unit tests (Vitest)
npm run test

# Run tests in watch mode
npm run test:watch

# Run tests with coverage
npm run test:coverage

# Run E2E tests (Playwright)
npm run test:e2e

# Run E2E tests in headed mode (see browser)
npm run test:e2e:headed
```

### Integration Tests

```bash
# Run full integration tests (requires Docker)
cd backend
dotnet test tests/BackupChrono.Integration.Tests

# These tests use TestContainers to spin up:
# - Restic repository in container
# - Mock SMB server
# - Full API stack
```

---

## Development Workflow

### 1. Create New Feature Branch

```bash
git checkout -b feature/add-nfs-support
```

### 2. Backend Development

```bash
# Watch for file changes and auto-rebuild
cd backend/src/BackupChrono.Api
dotnet watch run

# Backend hot-reload enabled - edit code and save
# API restarts automatically
```

### 3. Frontend Development

```bash
# Vite dev server with HMR (Hot Module Replacement)
cd frontend
npm run dev

# Edit React components - changes appear instantly
# No page refresh needed
```

### 4. Code Quality

```bash
# Format code (backend)
cd backend
dotnet format

# Lint code (frontend)
cd frontend
npm run lint
npm run lint:fix

# Type check (frontend)
npm run type-check
```

### 5. Commit Changes

```bash
git add .
git commit -m "feat: add NFS protocol plugin support"
git push origin feature/add-nfs-support
```

---

## Troubleshooting

### Backend Won't Start

**Problem**: `dotnet run` fails with dependency errors

**Solution**:
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore packages
dotnet restore

# Rebuild
dotnet build
```

### Frontend Build Errors

**Problem**: `npm run dev` fails with module errors

**Solution**:
```bash
# Delete node_modules and package-lock.json
rm -rf node_modules package-lock.json

# Reinstall
npm install

# Clear Vite cache
rm -rf .vite
```

### Restic Repository Locked

**Problem**: Backup fails with "repository is already locked"

**Solution**:
```bash
# Check for stale locks
docker exec backupchrono-api restic unlock

# Or manually in repository
docker run --rm \
  -v ${PWD}/repository:/repository \
  -e RESTIC_REPOSITORY=/repository \
  -e RESTIC_PASSWORD=your-password \
  restic/restic:0.16.5 unlock
```

### Git Configuration Conflicts

**Problem**: Configuration changes not appearing in UI

**Solution**:
```bash
# Check Git status
cd config
git status
git log

# Verify config files are committed
git add -A
git commit -m "Update configuration"

# Restart backend to reload config
docker-compose restart backupchrono-api
```

### Port Already in Use

**Problem**: `docker-compose up` fails with port conflicts

**Solution**:
```bash
# Find process using port 5000
netstat -ano | findstr :5000  # Windows
lsof -i :5000                  # Linux/macOS

# Kill process or change port in docker-compose.yml
```

### Docker Permission Errors

**Problem**: Volume mount permission denied

**Solution**:
```bash
# Linux: Fix volume permissions
sudo chown -R $USER:$USER config repository

# Windows: Ensure Docker Desktop file sharing is enabled
# Settings → Resources → File Sharing → Add C:\workspace\BackupChrono
```

---

## Useful Commands

### Docker Management

```bash
# View all BackupChrono containers
docker ps --filter "name=backupchrono"

# View logs for specific service
docker-compose logs -f backupchrono-api

# Execute command in running container
docker exec -it backupchrono-api bash

# Restart specific service
docker-compose restart backupchrono-api

# Rebuild images
docker-compose build --no-cache

# Clean up everything
docker-compose down -v --rmi all
```

### Restic Commands (Development)

```bash
# Check repository stats
docker exec backupchrono-api restic stats

# List all snapshots
docker exec backupchrono-api restic snapshots

# Check repository integrity
docker exec backupchrono-api restic check

# Prune old snapshots
docker exec backupchrono-api restic prune

# Verify snapshot contents
docker exec backupchrono-api restic ls <snapshot-id>
```

### Git Configuration Management

```bash
# View configuration history
cd config
git log --oneline

# View diff between commits
git diff HEAD~1 HEAD

# Rollback to previous configuration
git revert HEAD

# View configuration at specific commit
git show <commit-hash>:global.yaml
```

### Database Inspection (If Added Later)

```bash
# Currently BackupChrono is database-free (FR-066)
# All metadata in Git (config) and restic repository (snapshots)
# This section reserved for future if database is added
```

---

## Next Steps

### For Developers

1. **Read Architecture Docs**: `docs/architecture/README.md`
2. **Review Data Model**: `specs/001-backup-system/data-model.md`
3. **Explore API Contracts**: `specs/001-backup-system/contracts/openapi.yaml`
4. **Review Code Standards**: `docs/CONTRIBUTING.md`
5. **Join Development Chat**: Discord/Slack link

### For Contributors

1. **Check Open Issues**: https://github.com/yourusername/BackupChrono/issues
2. **Pick "Good First Issue"**: Look for `good-first-issue` label
3. **Read Contribution Guide**: `CONTRIBUTING.md`
4. **Submit Pull Request**: Follow PR template

### Advanced Topics

- **Adding Protocol Plugins**: `docs/plugins/protocol-plugins.md`
- **Restic Integration Deep Dive**: `docs/architecture/restic-integration.md`
- **SignalR Real-time Updates**: `docs/architecture/signalr.md`
- **Git Configuration Patterns**: `docs/architecture/git-config.md`

---

## Resources

### Documentation
- [Specification](./specs/001-backup-system/spec.md)
- [Data Model](./specs/001-backup-system/data-model.md)
- [API Reference](./specs/001-backup-system/contracts/openapi.yaml)
- [Implementation Plan](./specs/001-backup-system/plan.md)

### External Resources
- [Restic Documentation](https://restic.readthedocs.io/)
- [ASP.NET Core Docs](https://learn.microsoft.com/en-us/aspnet/core/)
- [React Documentation](https://react.dev/)
- [Docker Compose Reference](https://docs.docker.com/compose/)

### Community
- GitHub Issues: https://github.com/yourusername/BackupChrono/issues
- Discussions: https://github.com/yourusername/BackupChrono/discussions
- Discord: (link to server)

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
