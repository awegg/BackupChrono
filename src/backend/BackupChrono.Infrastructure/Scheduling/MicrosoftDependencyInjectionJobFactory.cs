using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Spi;

namespace BackupChrono.Infrastructure.Scheduling;

/// <summary>
/// A Quartz job factory that uses Microsoft.Extensions.DependencyInjection
/// to create job instances with resolved dependencies.
/// </summary>
public class MicrosoftDependencyInjectionJobFactory : IJobFactory
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public MicrosoftDependencyInjectionJobFactory(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
    }

    /// <summary>
    /// Creates a new job instance using the service provider.
    /// </summary>
    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        var scope = _serviceScopeFactory.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService(bundle.JobDetail.JobType) as IJob;
        
        if (job == null)
        {
            throw new InvalidOperationException(
                $"Job type {bundle.JobDetail.JobType.FullName} could not be resolved as IJob");
        }

        return job;
    }

    /// <summary>
    /// Disposes the job instance (no-op for DI-managed jobs).
    /// </summary>
    public void ReturnJob(IJob job)
    {
        // Jobs are scoped and will be disposed by the DI container
        (job as IDisposable)?.Dispose();
    }
}
