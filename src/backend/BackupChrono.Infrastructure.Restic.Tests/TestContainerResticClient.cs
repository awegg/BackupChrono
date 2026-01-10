using System.Text;
using System.Text.Json;
using BackupChrono.Infrastructure.Restic;
using DotNet.Testcontainers.Containers;

namespace BackupChrono.Infrastructure.Restic.Tests;

public class TestContainerResticClient : IResticClient
{
    private readonly IContainer _container;
    private readonly string _password;

    public string RepositoryPath { get; }

    public TestContainerResticClient(IContainer container, string repositoryPath, string password)
    {
        _container = container;
        RepositoryPath = repositoryPath;
        _password = password;
    }

    public async Task<string> ExecuteCommand(string[] args, CancellationToken cancellationToken = default, TimeSpan? timeout = null, Action<string>? onOutputLine = null, string? repositoryPathOverride = null, Action<string>? onErrorLine = null)
    {
        var repoPath = repositoryPathOverride ?? RepositoryPath;
        var commandParts = new List<string> { "/bin/sh", "-c" };
        
        var resticCmd = new StringBuilder();
        resticCmd.Append($"export RESTIC_PASSWORD={_password} && restic");
        
        foreach (var arg in args)
        {
            resticCmd.Append($" \"{arg}\"");
        }
        
        resticCmd.Append($" --repo {repoPath}");

        commandParts.Add(resticCmd.ToString());

        var result = await _container.ExecAsync(commandParts, cancellationToken);
        
        // Feed output to callbacks if provided
        if (onOutputLine != null && !string.IsNullOrEmpty(result.Stdout))
        {
            foreach (var line in result.Stdout.Split('\n'))
            {
                onOutputLine(line);
            }
        }

        if (onErrorLine != null && !string.IsNullOrEmpty(result.Stderr))
        {
             foreach (var line in result.Stderr.Split('\n'))
            {
                onErrorLine(line);
            }
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Restic command failed with exit code {result.ExitCode}: {result.Stderr}");
        }

        return result.Stdout;
    }

    public Task<byte[]> ExecuteCommandBinary(string[] args, CancellationToken cancellationToken = default, string? repositoryPathOverride = null)
    {
        // For now, simpler implementation assuming we can get stdout as string (text files)
        // or just throwing not implemented if not critical for initial coverage.
        // But ResticService uses it for DumpFile.
        // ExecAsync returns string.
        throw new NotImplementedException("Binary execution not fully supported by TestContainer ExecAsync wrapper yet");
    }

    public Task<Stream> ExecuteCommandStream(string[] args, CancellationToken cancellationToken = default, string? repositoryPathOverride = null)
    {
         throw new NotImplementedException();
    }

    public IAsyncEnumerable<T> ExecuteCommandJsonStream<T>(string[] args, CancellationToken cancellationToken = default, string? repositoryPathOverride = null)
    {
         throw new NotImplementedException();
    }

    public Task<T?> ExecuteCommandJson<T>(string[] args, CancellationToken cancellationToken = default)
    {
         throw new NotImplementedException();
    }
}
