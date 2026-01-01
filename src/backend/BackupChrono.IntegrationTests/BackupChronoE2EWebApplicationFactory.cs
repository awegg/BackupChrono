using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Git;
using BackupChrono.Infrastructure.Plugins;
using BackupChrono.Infrastructure.Protocols;
using BackupChrono.Infrastructure.Restic;
using BackupChrono.Infrastructure.Services;
using LibGit2Sharp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BackupChrono.IntegrationTests;

/// <summary>
/// WebApplicationFactory for end-to-end integration testing.
/// Uses REAL services and a real Git repository - no mocks.
/// Tests the complete call stack from HTTP to persistence.
/// </summary>
public class BackupChronoE2EWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _testRepositoryPath;
    private Repository? _gitRepo;

    public string RepositoryPath => _testRepositoryPath;

    public BackupChronoE2EWebApplicationFactory()
    {
        _testRepositoryPath = Path.Combine(Path.GetTempPath(), $"backup-chrono-e2e-{Guid.NewGuid()}");
    }

    public async Task InitializeAsync()
    {
        // Create test directory
        Directory.CreateDirectory(_testRepositoryPath);

        // Initialize a real Git repository
        try
        {
            _gitRepo = new Repository(Repository.Init(_testRepositoryPath));
            
            // Configure Git user for commits
            _gitRepo.Config.Set("user.name", "Test User");
            _gitRepo.Config.Set("user.email", "test@example.com");
            
            // Create necessary directories in Git
            Directory.CreateDirectory(Path.Combine(_testRepositoryPath, "devices"));
            Directory.CreateDirectory(Path.Combine(_testRepositoryPath, "shares"));
            Directory.CreateDirectory(Path.Combine(_testRepositoryPath, "backup-jobs"));
            
            // Create initial commit
            var readme = Path.Combine(_testRepositoryPath, "README.md");
            await File.WriteAllTextAsync(readme, "# Backup Repository\n");
            _gitRepo.Index.Add("README.md");
            _gitRepo.Index.Write();
            var signature = new Signature("Test User", "test@example.com", DateTimeOffset.UtcNow);
            _gitRepo.Commit("Initial commit", signature, signature);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize test Git repository at {_testRepositoryPath}", ex);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        // Dispose Git repository
        _gitRepo?.Dispose();

        // Clean up test directory
        try
        {
            if (Directory.Exists(_testRepositoryPath))
            {
                // Delete all files first (Git may lock them)
                var di = new DirectoryInfo(_testRepositoryPath);
                foreach (var file in di.GetFiles("*", SearchOption.AllDirectories))
                {
                    try { file.Delete(); }
                    catch { /* ignore */ }
                }
                
                Directory.Delete(_testRepositoryPath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
        await base.DisposeAsync().ConfigureAwait(false);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove in-memory services from testing
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(GitConfigService) ||
                d.ServiceType == typeof(ResticClient) ||
                d.ServiceType == typeof(IDeviceService) ||
                d.ServiceType == typeof(IShareService) ||
                d.ServiceType == typeof(IProtocolPluginLoader) ||
                d.ServiceType == typeof(IBackupOrchestrator) ||
                d.ServiceType == typeof(IQuartzSchedulerService)
            ).ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            // Register REAL services with test repository (NO MOCKS for Device/Share)
            services.AddSingleton(new GitConfigService(_testRepositoryPath));
            services.AddSingleton(new ResticClient("restic", _testRepositoryPath, "test-password"));
            
            // Register actual service implementations for device/share management
            services.AddSingleton<IDeviceService, DeviceService>();
            services.AddSingleton<IShareService, ShareService>();

            // Register protocol plugin loader and plugins
            services.AddSingleton<IProtocolPluginLoader, ProtocolPluginLoader>();
            
            // Register protocol plugins (needed by ProtocolPluginLoader)
            services.AddSingleton<SmbPlugin>();
            services.AddSingleton<SshPlugin>();
            services.AddSingleton<RsyncPlugin>();
            
            // BackupOrchestrator is also required - use mock for it
            var mockOrchestrator = new Mock<IBackupOrchestrator>();
            services.AddSingleton(mockOrchestrator.Object);
            
            // Still need QuartzSchedulerService mock since it's required by Program
            var mockScheduler = new Mock<IQuartzSchedulerService>();
            services.AddSingleton(mockScheduler.Object);
        });
    }
}
