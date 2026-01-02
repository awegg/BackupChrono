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
    private readonly Dictionary<IJob, IServiceScope> _scopes = new();

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
            scope.Dispose();
            throw new InvalidOperationException(
                $"Job type {bundle.JobDetail.JobType.FullName} could not be resolved as IJob");
        }

        _scopes[job] = scope;
        return job;
    }
    /// <summary>
    /// Disposes the job instance and its associated scope.
    /// </summary>
    public void ReturnJob(IJob job)
    {
        if (_scopes.TryGetValue(job, out var scope))
        {
            scope.Dispose();
            _scopes.Remove(job);
        }
        
        (job as IDisposable)?.Dispose();
    }
}
