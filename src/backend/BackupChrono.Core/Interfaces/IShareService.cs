using BackupChrono.Core.Entities;

namespace BackupChrono.Core.Interfaces;

/// <summary>
/// Service for managing share configurations (CRUD operations).
/// </summary>
public interface IShareService
{
    /// <summary>
    /// Creates a new share configuration.
    /// </summary>
    Task<Share> CreateShare(Share share);

    /// <summary>
    /// Gets a share by ID.
    /// </summary>
    Task<Share?> GetShare(Guid id);

    /// <summary>
    /// Lists all shares for a specific device.
    /// </summary>
    Task<IEnumerable<Share>> ListShares(Guid deviceId);

    /// <summary>
    /// Lists all shares across all devices.
    /// </summary>
    Task<IEnumerable<Share>> ListAllShares();

    /// <summary>
    /// Updates an existing share.
    /// </summary>
    Task<Share> UpdateShare(Share share);

    /// <summary>
    /// Deletes a share.
    /// </summary>
    Task DeleteShare(Guid id);

    /// <summary>
    /// Enables or disables a share.
    /// </summary>
    Task SetShareEnabled(Guid id, bool enabled);
}
