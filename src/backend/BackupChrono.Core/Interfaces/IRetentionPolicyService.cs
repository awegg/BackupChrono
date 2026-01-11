using BackupChrono.Core.DTOs;
using BackupChrono.Core.ValueObjects;

namespace BackupChrono.Core.Interfaces;

public interface IRetentionPolicyService
{
    Task<IEnumerable<RetentionRunResult>> ApplyForDevice(string deviceName, bool dryRun = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<RetentionRunResult>> ApplyForAll(bool dryRun = false, CancellationToken cancellationToken = default);
    Task<RetentionLastRun?> GetLastRun();
}