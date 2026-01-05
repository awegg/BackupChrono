using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace BackupChrono.Infrastructure.Restic;

/// <summary>
/// Low-level client for executing restic commands and parsing JSON output.
/// </summary>
public class ResticClient : IResticClient
{
    private readonly string _resticPath;
    private readonly string _repositoryPath;
    private readonly string _password;
    private readonly ILogger<ResticClient> _logger;

    public string RepositoryPath => _repositoryPath;

    public ResticClient(string resticPath, string repositoryPath, string password, ILogger<ResticClient> logger)
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
        _logger = logger;
    }

    /// <summary>
    /// Executes a restic command and returns the JSON output.
    /// </summary>
    public async Task<string> ExecuteCommand(string[] args, CancellationToken cancellationToken = default, TimeSpan? timeout = null, Action<string>? onOutputLine = null, string? repositoryPathOverride = null, Action<string>? onErrorLine = null)
    {
        // Default timeout: 30 minutes for long-running operations
        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(30);
        using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        // Set environment variables - use override if provided, otherwise use default
        var effectiveRepositoryPath = repositoryPathOverride ?? _repositoryPath;
        _logger.LogInformation("ExecuteCommand: args={Args}, effectiveRepositoryPath={RepositoryPath}", string.Join(" ", args), effectiveRepositoryPath);
        
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

        startInfo.Environment["RESTIC_REPOSITORY"] = effectiveRepositoryPath;
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

                // Invoke callback for each error line if provided
                onErrorLine?.Invoke(e.Data);
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

        _logger.LogInformation("Restic command exit code: {ExitCode}, output length: {OutputLength}, error length: {ErrorLength}", process.ExitCode, output.Length, error.Length);

        if (process.ExitCode != 0)
        {
            _logger.LogError("Restic command failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
            throw new InvalidOperationException(
                $"Restic command failed with exit code {process.ExitCode}. Error: {error}");
        }

        return output;
    }

    /// <summary>
    /// Executes a restic command and returns binary output (for dump command).
    /// </summary>
    public async Task<byte[]> ExecuteCommandBinary(
        string[] args,
        CancellationToken cancellationToken = default,
        string? repositoryPathOverride = null)
    {
        var effectiveTimeout = TimeSpan.FromMinutes(30);
        using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        var startInfo = new ProcessStartInfo
        {
            FileName = _resticPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var effectiveRepositoryPath = repositoryPathOverride ?? _repositoryPath;
        startInfo.Environment["RESTIC_REPOSITORY"] = effectiveRepositoryPath;
        startInfo.Environment["RESTIC_PASSWORD"] = _password;

        using var process = new Process { StartInfo = startInfo };
        var errorBuilder = new StringBuilder();

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        // Read binary output
        using var memoryStream = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(memoryStream, linkedCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await Task.Delay(500);
                }
            }
            catch { }

            if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Restic command timed out after {effectiveTimeout.TotalMinutes:F1} minutes");
            }
            
            throw;
        }

        if (process.ExitCode != 0)
        {
            var error = errorBuilder.ToString();
            throw new InvalidOperationException(
                $"Restic command failed with exit code {process.ExitCode}. Error: {error}");
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Executes a restic command and returns a stream (for memory-efficient dump command).
    /// </summary>
    public Task<Stream> ExecuteCommandStream(
        string[] args,
        CancellationToken cancellationToken = default,
        string? repositoryPathOverride = null)
    {
        var effectiveTimeout = TimeSpan.FromMinutes(30);
        var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        var startInfo = new ProcessStartInfo
        {
            FileName = _resticPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var effectiveRepositoryPath = repositoryPathOverride ?? _repositoryPath;
        startInfo.Environment["RESTIC_REPOSITORY"] = effectiveRepositoryPath;
        startInfo.Environment["RESTIC_PASSWORD"] = _password;

        var process = new Process { StartInfo = startInfo };
        var errorBuilder = new StringBuilder();

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        // Create a stream wrapper that will clean up the process when disposed
        var processStream = new ProcessOutputStream(
            process.StandardOutput.BaseStream,
            process,
            errorBuilder,
            linkedCts);

        return Task.FromResult<Stream>(processStream);
    }

    /// <summary>
    /// Stream wrapper that ensures process cleanup.
    /// </summary>
    private class ProcessOutputStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly Process _process;
        private readonly StringBuilder _errorBuilder;
        private readonly CancellationTokenSource _cts;
        private bool _disposed;

        public ProcessOutputStream(
            Stream baseStream,
            Process process,
            StringBuilder errorBuilder,
            CancellationTokenSource cts)
        {
            _baseStream = baseStream;
            _process = process;
            _errorBuilder = errorBuilder;
            _cts = cts;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _baseStream.Read(buffer, offset, count);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                _baseStream?.Dispose();
                
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.WaitForExit(5000);
                        
                        // If still not exited, force kill
                        if (!_process.HasExited)
                        {
                            _process.Kill(entireProcessTree: true);
                            _process.WaitForExit(1000);
                        }
                    }

                    // Only check ExitCode if process has exited
                    if (_process.HasExited && _process.ExitCode != 0)
                    {
                        var error = _errorBuilder.ToString();
                        // Can't throw in Dispose, log instead
                        Console.Error.WriteLine($"Restic process exited with code {_process.ExitCode}: {error}");
                    }
                }
                finally
                {
                    _process?.Dispose();
                    _cts?.Dispose();
                }
            }

            base.Dispose(disposing);
        }
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
    public async IAsyncEnumerable<T> ExecuteCommandJsonStream<T>(
        string[] args,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? repositoryPathOverride = null)
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

        var effectiveRepositoryPath = repositoryPathOverride ?? _repositoryPath;
        startInfo.Environment["RESTIC_REPOSITORY"] = effectiveRepositoryPath;
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
