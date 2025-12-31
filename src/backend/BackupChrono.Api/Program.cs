using BackupChrono.Api.Middleware;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Git;
using BackupChrono.Infrastructure.Plugins;
using BackupChrono.Infrastructure.Restic;
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
builder.Services.AddSingleton<PluginLoader>();
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

// TODO: Register missing service implementations (Phase 3 - User Story 1)
// These interfaces exist but implementations are not yet created:
// - IDeviceService implementation needed (T051 - DeviceService)
// - IShareService implementation needed (T052 - ShareService)
// - IBackupOrchestrator implementation needed (T053 - BackupOrchestrator)
// builder.Services.AddScoped<IDeviceService, DeviceService>();
// builder.Services.AddScoped<IShareService, ShareService>();
// builder.Services.AddScoped<IBackupOrchestrator, BackupOrchestrator>();

var app = builder.Build();

// Initialize PluginLoader to register all protocol plugins
var pluginLoader = app.Services.GetRequiredService<PluginLoader>();
app.Logger.LogInformation("Loaded {PluginCount} protocol plugins", pluginLoader.GetAllPlugins().Count());

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

app.Run();
