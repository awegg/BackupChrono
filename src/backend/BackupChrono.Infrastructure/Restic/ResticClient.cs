using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace BackupChrono.Infrastructure.Restic;

/// <summary>
/// Low-level client for executing restic commands and parsing JSON output.
/// </summary>
public class ResticClient
{
    private readonly string _resticPath;
    private readonly string _repositoryPath;
    private readonly string _password;

    public ResticClient(string resticPath, string repositoryPath, string password)
    {
        if (string.IsNullOrWhiteSpace(resticPath))
            throw new ArgumentException("Restic binary path cannot be empty.", nameof(resticPath));
        
        if (string.IsNullOrWhiteSpace(repositoryPath))
            throw new ArgumentException("Repository path cannot be empty.", nameof(repositoryPath));
        
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Restic password cannot be empty. Restic requires a non-empty password for repository initialization and operations.", nameof(password));
        
        _resticPath = resticPath;
        _repositoryPath = repositoryPath;
        _password = password;
    }

    /// <summary>
    /// Executes a restic command and returns the JSON output.
    /// </summary>
    public async Task<string> ExecuteCommand(string[] args, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _resticPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Add arguments individually to prevent command injection
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        // Set environment variables
        startInfo.Environment["RESTIC_REPOSITORY"] = _repositoryPath;
        startInfo.Environment["RESTIC_PASSWORD"] = _password;

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Restic command failed with exit code {process.ExitCode}: {errorBuilder}");
        }

        return outputBuilder.ToString();
    }

    /// <summary>
    /// Executes a restic command and parses the JSON output.
    /// </summary>
    public async Task<T?> ExecuteCommandJson<T>(string[] args, CancellationToken cancellationToken = default)
    {
        var output = await ExecuteCommand(args, cancellationToken);
        
        if (string.IsNullOrWhiteSpace(output))
            return default;

        return JsonSerializer.Deserialize<T>(output);
    }

    /// <summary>
    /// Executes a restic command with streaming JSON output (for progress monitoring).
    /// </summary>
    public async IAsyncEnumerable<T> ExecuteCommandJsonStream<T>(string[] args, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _resticPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        startInfo.Environment["RESTIC_REPOSITORY"] = _repositoryPath;
        startInfo.Environment["RESTIC_PASSWORD"] = _password;

        using var process = new Process { StartInfo = startInfo };
        var errorBuilder = new StringBuilder();
        
        // Asynchronously capture stderr to prevent buffer blocking
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                errorBuilder.AppendLine(e.Data);
        };
        
        process.Start();
        process.BeginErrorReadLine();

        using var reader = process.StandardOutput;
        
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            T? item = default;
            try
            {
                item = JsonSerializer.Deserialize<T>(line);
            }
            catch (JsonException)
            {
                // Skip non-JSON lines
                continue;
            }

            if (item != null)
                yield return item;
        }

        await process.WaitForExitAsync(cancellationToken);
        
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Restic command failed with exit code {process.ExitCode}: {errorBuilder}");
        }
    }
}
