using Ballware.Storage.Jobs.Configuration;
using Ballware.Storage.Jobs.Internal;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.AspNetCore;

namespace Ballware.Storage.Jobs;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareStorageBackgroundJobs(this IServiceCollection services, TriggerOptions options)
    {
        services.AddQuartz(q =>
        {
            q.AddJob<TemporaryCleanupJob>(TemporaryCleanupJob.Key, configurator => configurator.StoreDurably());

            q.AddTrigger(triggerConfigurator =>
            {
                triggerConfigurator
                    .ForJob(TemporaryCleanupJob.Key)
                    .WithIdentity("TemporaryCleanup", "temporary")
                    .WithCronSchedule(options.TemporaryCleanupCron);
            });
        });

        services.AddQuartzServer(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        return services;
    }
}