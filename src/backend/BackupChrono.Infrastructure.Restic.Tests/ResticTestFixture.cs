using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace BackupChrono.Infrastructure.Restic.Tests;

/// <summary>
/// Shared test fixture for restic integration tests.
/// Provides a Docker container with restic installed and an initialized repository.
/// </summary>
public class ResticTestFixture : IAsyncLifetime
{
    public IContainer? Container { get; private set; }
    public string RepositoryPath { get; private set; } = "/tmp/test-repo";
    public readonly string ResticPassword = Guid.NewGuid().ToString();

    public async Task InitializeAsync()
    {
        // Build a container with restic installed (pinned to alpine:3.19 for reproducibility)
        Container = new ContainerBuilder()
            .WithImage("alpine:3.19")
            .WithCommand("/bin/sh", "-c", "while true; do sleep 30; done")
            .Build();

        await Container.StartAsync();

        // Install restic v0.18.1 in the container with retry logic
        var installResult = await Container.ExecAsync(new[] { "/bin/sh", "-c", 
            "apk add --no-cache wget ca-certificates bzip2 && " +
            "wget --tries=3 -q https://github.com/restic/restic/releases/download/v0.18.1/restic_0.18.1_linux_amd64.bz2 && " +
            "echo '680838f19d67151adba227e1570cdd8af12c19cf1735783ed1ba928bc41f363d  restic_0.18.1_linux_amd64.bz2' | sha256sum -c - && " +
            "bunzip2 restic_0.18.1_linux_amd64.bz2 && " +
            "mv restic_0.18.1_linux_amd64 /usr/local/bin/restic && " +
            "chmod +x /usr/local/bin/restic" });

        if (installResult.ExitCode != 0)
        {
            throw new Exception($"Failed to install restic: {installResult.Stderr}");
        }

        // Initialize a test repository
        await Container.ExecAsync(new[] { "/bin/sh", "-c", $"mkdir -p {RepositoryPath}" });
        
        var initResult = await Container.ExecAsync(new[] { "/bin/sh", "-c", 
            $"export RESTIC_PASSWORD={ResticPassword} && restic init --repo {RepositoryPath}" });
        
        if (initResult.ExitCode != 0)
        {
            throw new Exception($"Failed to initialize restic repository: {initResult.Stderr}");
        }
    }

    public async Task DisposeAsync()
    {
        if (Container != null)
        {
            await Container.DisposeAsync();
        }
    }
}
