using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Utilities;
using SMBLibrary;
using SMBLibrary.Client;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BackupChrono.Infrastructure.Plugins;

/// <summary>
/// SMB/CIFS protocol plugin for network shares.
/// Uses native Windows 'net use' on Windows, mount.cifs on Linux, or SMBLibrary as fallback.
/// </summary>
public class SmbPlugin : IProtocolPlugin
{
    private static readonly Dictionary<string, string> _mountedPaths = new();
    private static readonly object _mountLock = new object();
    public string ProtocolName => "SMB";

    public bool SupportsWakeOnLan => true;

    public bool RequiresAuthentication => true;

    public Task<bool> TestConnection(Device device)
    {
        // Use SMBLibrary for cross-platform connection testing
        // Note: SMBLibrary Connect() doesn't support custom ports - it always uses 445
        // This limitation is documented in integration tests
        var client = new SMB2Client();
        try
        {
            var connected = client.Connect(device.Host, SMBTransportType.DirectTCPTransport);
            if (!connected)
            {
                return Task.FromResult(false);
            }

            var status = client.Login(string.Empty, device.Username, device.Password.GetPlaintext());
            if (status != NTStatus.STATUS_SUCCESS)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
        finally
        {
            client.Disconnect();
        }
    }

    public async Task<string> MountShare(Device device, Share share)
    {
        var uncPath = BuildUncPath(device, share);
        var password = device.Password.GetPlaintext();

        lock (_mountLock)
        {
            // Check if already mounted
            if (_mountedPaths.ContainsKey(uncPath))
            {
                return _mountedPaths[uncPath];
            }
        }

        string mountPoint;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            mountPoint = await MountShareWindows(device, share, uncPath, password);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            mountPoint = await MountShareLinux(device, share, uncPath, password);
        }
        else
        {
            throw new PlatformNotSupportedException($"SMB mounting not supported on {RuntimeInformation.OSDescription}");
        }

        lock (_mountLock)
        {
            _mountedPaths[uncPath] = mountPoint;
        }

        return mountPoint;
    }

    private async Task<string> MountShareWindows(Device device, Share share, string uncPath, string password)
    {
        // Find an available drive letter
        var driveLetter = FindAvailableDriveLetter();
        if (driveLetter == null)
        {
            throw new InvalidOperationException("No available drive letters for SMB mount");
        }

        var mountPoint = $"{driveLetter}:";

        // Mount using net use
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "net",
                Arguments = $"use {mountPoint} \"{uncPath}\" /user:{device.Username} \"{password}\" /persistent:no",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(outputTask, errorTask);
            process.WaitForExit();

            var output = outputTask.Result;
            var error = errorTask.Result;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to mount SMB share '{uncPath}'. Exit code: {process.ExitCode}. Error: {error}");
            }

            return mountPoint;
        }
        finally
        {
            process.Dispose();
        }
    }

    private async Task<string> MountShareLinux(Device device, Share share, string uncPath, string password)
    {
        // Create a temporary mount point
        var mountPoint = Path.Combine("/tmp", $"backupchrono-smb-{Guid.NewGuid()}");
        Directory.CreateDirectory(mountPoint);

        // Create credentials file for security
        var credFile = Path.Combine("/tmp", $".smbcreds-{Guid.NewGuid()}");
        File.WriteAllText(credFile, $"username={device.Username}\npassword={password}\n");

        try
        {
            // Set restrictive permissions on credential file
            var chmodProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"600 {credFile}",
                RedirectStandardError = true,
                UseShellExecute = false
            });
            chmodProcess?.WaitForExit();

            // Mount using mount.cifs with port support
            var portOption = device.Port.HasValue && device.Port.Value != 445 
                ? $",port={device.Port.Value}" 
                : "";
            
            // Check if we need sudo - always use it if available on Linux for mounting
            // This is needed in CI environments where tests run with sudo but child processes don't inherit it
            var useSudo = File.Exists("/usr/bin/sudo");
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = useSudo ? "sudo" : "mount",
                    Arguments = useSudo 
                        ? $"mount -n -t cifs \"{uncPath}\" \"{mountPoint}\" -o credentials={credFile},vers=3.0,noperm{portOption}"
                        : $"-n -t cifs \"{uncPath}\" \"{mountPoint}\" -o credentials={credFile},vers=3.0,noperm{portOption}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(outputTask, errorTask);
            process.WaitForExit();

            var output = outputTask.Result;
            var error = errorTask.Result;

            if (process.ExitCode != 0)
            {
                // Cleanup on failure
                try { Directory.Delete(mountPoint); } catch { }
                
                throw new InvalidOperationException(
                    $"Failed to mount SMB share '{uncPath}'. Exit code: {process.ExitCode}. Error: {error}. " +
                    $"Make sure cifs-utils is installed: apt-get install cifs-utils");
            }

            return mountPoint;
        }
        finally
        {
            // Clean up credentials file
            try { File.Delete(credFile); } catch { }
        }
    }

    public async Task UnmountShare(string mountPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await UnmountShareWindows(mountPath);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            await UnmountShareLinux(mountPath);
        }

        lock (_mountLock)
        {
            // Remove from tracking (even if unmount failed, don't track it)
            var entry = _mountedPaths.FirstOrDefault(x => x.Value == mountPath);
            if (entry.Key != null)
            {
                _mountedPaths.Remove(entry.Key);
            }
        }
    }

    private async Task UnmountShareWindows(string mountPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "net",
                Arguments = $"use {mountPath} /delete /yes",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(outputTask, errorTask);
            process.WaitForExit();

            var output = outputTask.Result;
            var error = errorTask.Result;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to unmount SMB share at '{mountPath}'. Exit code: {process.ExitCode}. Error: {error}");
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    private async Task UnmountShareLinux(string mountPath)
    {
        // Check if we need sudo - always use it if available on Linux for unmounting
        // This is needed in CI environments where tests run with sudo but child processes don't inherit it
        var useSudo = File.Exists("/usr/bin/sudo");
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = useSudo ? "sudo" : "umount",
                Arguments = useSudo ? $"umount {mountPath}" : mountPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(outputTask, errorTask);
            process.WaitForExit();

            var output = outputTask.Result;
            var error = errorTask.Result;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to unmount SMB share at '{mountPath}'. Exit code: {process.ExitCode}. Error: {error}");
            }

            // Clean up the mount point directory
            try
            {
                if (Directory.Exists(mountPath))
                {
                    Directory.Delete(mountPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    public async Task WakeDevice(Device device)
    {
        if (string.IsNullOrWhiteSpace(device.WakeOnLanMacAddress))
        {
            throw new InvalidOperationException("Device does not have Wake-on-LAN MAC address configured");
        }

        await WakeOnLanHelper.SendMagicPacket(device.WakeOnLanMacAddress);
    }

    private string BuildUncPath(Device device, Share? share)
    {
        var host = device.Host;
        
        // UNC path format: \\host\sharepath
        if (share == null)
        {
            return $"\\\\{host}";
        }

        // Clean up the share path - remove leading/trailing slashes and backslashes
        var sharePath = share.Path.Trim('\\', '/');
        
        // Build UNC path
        return $"\\\\{host}\\{sharePath}";
    }

    private char? FindAvailableDriveLetter()
    {
        var usedDrives = DriveInfo.GetDrives().Select(d => d.Name[0]).ToHashSet();
        
        // Start from Z and work backwards to avoid conflicts with common drives
        for (char letter = 'Z'; letter >= 'D'; letter--)
        {
            if (!usedDrives.Contains(letter))
            {
                return letter;
            }
        }

        return null;
    }
}
