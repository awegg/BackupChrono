using BackupChrono.Api.Hubs;
using BackupChrono.Api.Middleware;
using BackupChrono.Api.Services;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Git;
using BackupChrono.Infrastructure.Plugins;
using BackupChrono.Infrastructure.Protocols;
using BackupChrono.Infrastructure.Repositories;
using BackupChrono.Infrastructure.Restic;
using BackupChrono.Infrastructure.Scheduling;
using BackupChrono.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure host shutdown timeout to allow backup jobs to cancel gracefully
builder.Host.ConfigureServices((context, services) =>
{
    services.Configure<HostOptions>(options =>
    {
        options.ShutdownTimeout = TimeSpan.FromSeconds(30);
    });
});

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() 
    { 
        Title = "BackupChrono API", 
        Version = "v1" 
    });
});

// Add SignalR
builder.Services.AddSignalR();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            // Fallback: allow any origin if not configured (Development only)
            if (builder.Environment.IsDevelopment())
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            }
            else
            {
                // In production, require explicit origins to be configured
                throw new InvalidOperationException("CORS origins must be configured in production environment.");
            }
        }    });
});

// Register application services
var configPath = builder.Configuration["ConfigRepository:Path"] ?? "./config";
// Make the path absolute if it's relative, resolving from the application directory
if (!Path.IsPathRooted(configPath))
{
    configPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, configPath));
}

// Configure ResticOptions
builder.Services.Configure<BackupChrono.Infrastructure.Services.ResticOptions>(options =>
{
    var repositoryBasePath = builder.Configuration["Restic:RepositoryPath"] ?? "./repositories";
    // Convert to absolute path if relative
    options.RepositoryBasePath = Path.IsPathRooted(repositoryBasePath)
        ? repositoryBasePath
        : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, repositoryBasePath));
    
    options.BinaryPath = builder.Configuration["Restic:BinaryPath"] ?? "restic";
    options.Password = builder.Configuration["Restic:Password"];
});

builder.Services.AddSingleton<GitConfigService>(sp => 
    new GitConfigService(configPath));

// Register ResticClient both as concrete type and interface because StorageMonitor depends on the concrete type
builder.Services.AddSingleton<ResticClient>(sp =>
{
    var resticOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BackupChrono.Infrastructure.Services.ResticOptions>>().Value;
    var resticPassword = resticOptions.Password;
    
    if (string.IsNullOrWhiteSpace(resticPassword))
    {
        if (builder.Environment.IsDevelopment())
        {
            // In development, allow empty password but log a warning
            var logger = sp.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Restic password is not configured. Set Restic:Password in appsettings.json or environment variable. Using empty password for development only.");
            resticPassword = "development-password-changeme";
        }
        else
        {
            // In production, require password to be configured
            throw new InvalidOperationException("Restic password must be configured in production environment. Set Restic:Password in configuration or RESTIC_PASSWORD environment variable.");
        }
    }
    
    return new ResticClient(
        resticOptions.BinaryPath,
        resticOptions.RepositoryBasePath,
        resticPassword,
        sp.GetRequiredService<ILogger<ResticClient>>());
});

builder.Services.AddSingleton<IResticClient>(sp => sp.GetRequiredService<ResticClient>());
builder.Services.AddSingleton<IResticService, ResticService>();

// Register application services (Phase 3 - User Story 1)
builder.Services.AddSingleton<IMappingService, MappingService>();
builder.Services.AddSingleton<IBackupLogService, InMemoryBackupLogService>();

// Protocol plugins
builder.Services.AddSingleton<SmbPlugin>();
builder.Services.AddSingleton<SshPlugin>();
builder.Services.AddSingleton<RsyncPlugin>();

// Protocol plugin loader (discovers and instantiates plugins)
builder.Services.AddSingleton<IProtocolPluginLoader, ProtocolPluginLoader>();

// Device and Share services
builder.Services.AddSingleton<IDeviceService, DeviceService>();
builder.Services.AddSingleton<IShareService, ShareService>();

// Backup orchestration - Singleton to preserve job state during shutdown
builder.Services.AddSingleton<IBackupOrchestrator, BackupOrchestrator>();

// Storage monitoring
builder.Services.AddSingleton<IStorageMonitor, StorageMonitor>();

// Repositories
builder.Services.AddSingleton<IBackupJobRepository, BackupJobRepository>(sp =>
{
    var gitConfig = sp.GetRequiredService<GitConfigService>();
    var logger = sp.GetRequiredService<ILogger<BackupJobRepository>>();
    var deviceService = sp.GetRequiredService<IDeviceService>();
    var shareService = sp.GetRequiredService<IShareService>();
    return new BackupJobRepository(gitConfig.RepositoryPath, logger, deviceService, shareService);
});

// Quartz Scheduler
builder.Services.AddSingleton<IQuartzSchedulerService, QuartzSchedulerService>();

// Register BackupJob for DI (required by Quartz job factory)
builder.Services.AddTransient<BackupChrono.Infrastructure.Scheduling.BackupJob>();

// BackupProgressBroadcaster - bridges BackupOrchestrator events to SignalR (must be after all dependencies)
builder.Services.AddHostedService<BackupProgressBroadcaster>();

// JobCleanupService - marks stale "Running" jobs as Failed on startup
builder.Services.AddHostedService<JobCleanupService>();

// BackupShutdownHandler - ensures graceful cancellation of running jobs on app shutdown
builder.Services.AddHostedService<BackupShutdownHandler>();

var app = builder.Build();

// Start the Quartz scheduler (which also schedules all backups)
var schedulerService = app.Services.GetRequiredService<IQuartzSchedulerService>();
await schedulerService.Start();
app.Logger.LogInformation("Quartz scheduler started with all configured backups");

// Configure the HTTP request pipeline
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "BackupChrono API v1");
    });
}

app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
// TODO: Add authentication/authorization when implementing user management and access control
// app.UseAuthentication();
// app.UseAuthorization();
app.MapControllers();
app.MapHub<BackupProgressHub>("/hubs/backup-progress");
app.MapHub<RestoreProgressHub>("/hubs/restore-progress");

// Register graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    // Wrap async shutdown logic in Task.Run to avoid sync-over-async deadlocks
    Task.Run(async () =>
    {
        app.Logger.LogInformation("Application shutdown requested - stopping scheduler and checking for running backups");
        
        // Get the scheduler service
        var schedulerService = app.Services.GetRequiredService<IQuartzSchedulerService>();    
        // Stop the scheduler (prevents new jobs from starting)
        await schedulerService.Stop().ConfigureAwait(false);
        
        app.Logger.LogInformation("Scheduler stopped - no new backups will start");
        
        // Check for active jobs but don't wait long (Docker typically gives 10s before SIGKILL)
        var orchestrator = app.Services.GetRequiredService<IBackupOrchestrator>();
        var timeout = TimeSpan.FromSeconds(8); // Conservative timeout for Docker containers
        var waitStart = DateTime.UtcNow;
        
        while (DateTime.UtcNow - waitStart < timeout)
        {
            var activeCount = orchestrator.GetActiveJobCount();
            
            if (activeCount == 0)
            {
                app.Logger.LogInformation("All backup jobs completed successfully");
                break;
            }
            
            app.Logger.LogWarning(
                "Waiting for {Count} backup job(s) to complete... ({Elapsed}s elapsed)", 
                activeCount, 
                (DateTime.UtcNow - waitStart).TotalSeconds);
            await Task.Delay(1000).ConfigureAwait(false); // Check every second
        }
        
        var stillRunning = orchestrator.GetActiveJobCount();
        
        if (stillRunning > 0)
        {
            app.Logger.LogWarning(
                "Graceful shutdown timeout - {Count} backup job(s) still running. Force-cancelling all jobs.",
                stillRunning);
            
            // Force cancel all remaining jobs
            await orchestrator.CancelAllJobs().ConfigureAwait(false);
            
            // Give cancellation tokens a moment to propagate
            await Task.Delay(500).ConfigureAwait(false);
            
            app.Logger.LogInformation("All backup jobs cancelled");
        }
        else
        {
            app.Logger.LogInformation("Graceful shutdown complete - all jobs finished");
        }
    }).GetAwaiter().GetResult();
});

app.Run();

/// <summary>
/// Partial Program class to enable WebApplicationFactory for integration testing.
/// </summary>
public partial class Program { }
