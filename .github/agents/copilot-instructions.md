﻿# BackupChrono Development Guidelines

Auto-generated from all feature plans. Last updated: 2025-12-30

## Active Technologies

- C# / .NET 8.0 (ASP.NET Core), JavaScript/TypeScript (React 18+) (001-backup-system)

## Project Structure

```text
backend/
frontend/
tests/
```

## Commands

npm test; npm run lint

## Code Style

C# / .NET 8.0 (ASP.NET Core), JavaScript/TypeScript (React 18+): Follow standard conventions

## Testing Guidelines

### Unit Tests (xUnit)

### CRITICAL: Never write meaningless tests

❌ **BAD - Meaningless placeholder:**
```csharp
[Fact]
public async Task ScheduleDeviceBackup_SchedulesJob()
{
    await _schedulerService.ScheduleDeviceBackup(device, schedule);
    Assert.True(true); // ❌ NEVER DO THIS
}
```

✅ **GOOD - Verify actual behavior:**
```csharp
[Fact]
public async Task ScheduleDeviceBackup_SchedulesJob()
{
    var device = new Device { Id = Guid.NewGuid(), Name = "TestNAS" };
    var schedule = new Schedule { CronExpression = "0 2 * * *" };
    
    await _schedulerService.ScheduleDeviceBackup(device, schedule);
    
    // Verify job was persisted and can be queried
    var scheduledJobs = await _schedulerService.GetScheduledJobs();
    scheduledJobs.Should().ContainSingle(j => j.DeviceId == device.Id);
}
```

**Test Assertions Must Verify:**
1. **State changes** - Mock.Verify() calls were made, objects were mutated
2. **Return values** - Correct data returned, correct status codes
3. **Side effects** - Files created, events raised, exceptions thrown
4. **Behavior** - Can trigger scheduled jobs, can't trigger removed jobs

**Examples of Real Assertions:**
- `_mockService.Verify(x => x.Method(), Times.Once)` - Verify interaction
- `result.Should().BeOfType<OkObjectResult>()` - Verify return type
- `await Assert.ThrowsAsync<Exception>()` - Verify exceptions
- `await service.GetItem(id)` - Verify state persisted
- `File.Exists(path).Should().BeTrue()` - Verify side effects

**If you can't think of a real assertion, the test shouldn't exist.**

## Recent Changes

- 001-backup-system: Added C# / .NET 8.0 (ASP.NET Core), JavaScript/TypeScript (React 18+)

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
