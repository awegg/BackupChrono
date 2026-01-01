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
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add services to the container
builder.Services.AddControllers();
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
builder.Services.AddSingleton<GitConfigService>(sp => 
    new GitConfigService(builder.Configuration["ConfigRepository:Path"] ?? "./config"));
builder.Services.AddSingleton<ResticClient>(sp => 
{
    var resticPassword = builder.Configuration["Restic:Password"];
    
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
        builder.Configuration["Restic:BinaryPath"] ?? "restic",
        builder.Configuration["Restic:RepositoryPath"] ?? "/restic-repo",
        resticPassword);
});
builder.Services.AddSingleton<IResticService, ResticService>();

// Register application services (Phase 3 - User Story 1)
builder.Services.AddSingleton<IMappingService, MappingService>();

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
    return new BackupJobRepository(gitConfig.RepositoryPath, logger);
});

// Quartz Scheduler
builder.Services.AddSingleton<IQuartzSchedulerService, QuartzSchedulerService>();

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
            var activeJobs = await orchestrator.ListJobs().ConfigureAwait(false);
            var runningCount = activeJobs.Count(j => j.Status == BackupJobStatus.Running);
            
            if (runningCount == 0)
            {
                app.Logger.LogInformation("All backup jobs completed successfully");
                break;
            }
            
            app.Logger.LogWarning(
                "Waiting for {Count} backup job(s) to complete... ({Elapsed}s elapsed)", 
                runningCount, 
                (DateTime.UtcNow - waitStart).TotalSeconds);
            await Task.Delay(1000).ConfigureAwait(false); // Check every second
        }
        
        var finalJobs = await orchestrator.ListJobs().ConfigureAwait(false);
        var stillRunning = finalJobs.Count(j => j.Status == BackupJobStatus.Running);
        
        if (stillRunning > 0)
        {
            app.Logger.LogWarning(
                "Graceful shutdown timeout - {Count} backup job(s) still running. " +
                "Jobs will be marked as cancelled. Consider increasing Docker stop timeout if needed.",
                stillRunning);
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
