using BackupChrono.Core.Entities;

namespace BackupChrono.Core.Interfaces;

/// <summary>
/// Service for managing device configurations (CRUD operations).
/// </summary>
public interface IDeviceService
{
    /// <summary>
    /// Creates a new device configuration.
    /// </summary>
    Task<Device> CreateDevice(Device device);

    /// <summary>
    /// Gets a device by ID.
    /// </summary>
    Task<Device?> GetDevice(Guid id);

    /// <summary>
    /// Gets a device by name.
    /// </summary>
    Task<Device?> GetDeviceByName(string name);

    /// <summary>
    /// Lists all devices.
    /// </summary>
    Task<IEnumerable<Device>> ListDevices();

    /// <summary>
    /// Updates an existing device.
    /// </summary>
    Task<Device> UpdateDevice(Device device);

    /// <summary>
    /// Deletes a device and all associated shares.
    /// </summary>
    Task DeleteDevice(Guid id);

    /// <summary>
    /// Tests connectivity to a device.
    /// </summary>
    Task<bool> TestConnection(Guid id);
}
