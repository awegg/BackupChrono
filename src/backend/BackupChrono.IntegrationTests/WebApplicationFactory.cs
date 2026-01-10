using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Git;
using BackupChrono.Infrastructure.Repositories;
using BackupChrono.Infrastructure.Restic;
using BackupChrono.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace BackupChrono.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for integration testing the BackupChrono API.
/// Provides isolated test instances with mocked dependencies for controlled testing.
/// </summary>
public class BackupChronoWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _repositoryPath;
    
    // Mocks that can be configured by tests
    public Mock<IDeviceService> MockDeviceService { get; } = new();
    public Mock<IShareService> MockShareService { get; } = new();
    public Mock<IBackupOrchestrator> MockBackupOrchestrator { get; } = new();
    public Mock<IQuartzSchedulerService> MockSchedulerService { get; } = new();

    public BackupChronoWebApplicationFactory()
    {
        _repositoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_repositoryPath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing service registrations
            var descriptors = services.Where(d => 
                d.ServiceType == typeof(IDeviceService) ||
                d.ServiceType == typeof(IShareService) ||
                d.ServiceType == typeof(IBackupOrchestrator) ||
                d.ServiceType == typeof(IQuartzSchedulerService) ||
                d.ServiceType == typeof(IBackupJobRepository) ||
                d.ServiceType == typeof(GitConfigService) ||
                d.ServiceType == typeof(ResticClient) ||
                d.ServiceType == typeof(IResticClient)
            ).ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            // Register mocked services
            services.AddScoped(_ => MockDeviceService.Object);
            services.AddScoped(_ => MockShareService.Object);
            // BackupOrchestrator must be Singleton for graceful shutdown (see Program.cs line 107)
            services.AddSingleton(_ => MockBackupOrchestrator.Object);
            services.AddSingleton(_ => MockSchedulerService.Object);
            
            // Provide a real GitConfigService with test repository
            var gitConfig = new GitConfigService(_repositoryPath);
            services.AddSingleton(gitConfig);
            
            // Provide a real ResticClient
            services.AddSingleton<ResticClient>(sp => 
            {
                var logger = sp.GetRequiredService<ILogger<ResticClient>>();
                return new ResticClient("restic", _repositoryPath, "test-password", logger);
            });
            services.AddSingleton<IResticClient>(sp => sp.GetRequiredService<ResticClient>());
            
            // Provide a real BackupJobRepository with the mocked services
            services.AddSingleton<IBackupJobRepository>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<BackupJobRepository>>();
                return new BackupJobRepository(
                    gitConfig.RepositoryPath, 
                    logger,
                    MockDeviceService.Object,
                    MockShareService.Object);
            });
            
            // Provide a real ResticClient with test repository
            services.AddSingleton(sp => new ResticClient("restic", _repositoryPath, "test-password", sp.GetRequiredService<ILogger<ResticClient>>()));
        });
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            // Clean up test repository
            if (Directory.Exists(_repositoryPath))
            {
                Directory.Delete(_repositoryPath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        await base.DisposeAsync();
    }
}
