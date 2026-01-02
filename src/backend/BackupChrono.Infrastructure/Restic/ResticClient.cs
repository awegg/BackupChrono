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

    public string RepositoryPath => _repositoryPath;

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
    public async Task<string> ExecuteCommand(string[] args, CancellationToken cancellationToken = default, TimeSpan? timeout = null, Action<string>? onOutputLine = null)
    {
        // Default timeout: 30 minutes for long-running operations
        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(30);
        using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        var startInfo = new ProcessStartInfo
        {
            FileName = _resticPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,  // Changed from true - we don't need stdin
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
        var outputLock = new object();
        var errorLock = new object();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                lock (outputLock)
                {
                    outputBuilder.AppendLine(e.Data);
                }
                
                // Invoke callback for each output line if provided
                onOutputLine?.Invoke(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                lock (errorLock)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Kill the process if it's still running
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    // Give it a moment to clean up
                    await Task.Delay(500);
                }
            }
            catch
            {
                // Ignore errors during kill
            }

            if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Restic command timed out after {effectiveTimeout.TotalMinutes:F1} minutes");
            }
            
            throw;
        }

        string output;
        string error;
        
        lock (outputLock) { output = outputBuilder.ToString(); }
        lock (errorLock) { error = errorBuilder.ToString(); }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Restic command failed with exit code {process.ExitCode}. Error: {error}");
        }

        return output;
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
