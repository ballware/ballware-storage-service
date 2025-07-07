using System.Collections.Immutable;
using Ballware.Storage.Data.Repository;
using Ballware.Storage.Metadata;
using Quartz;

namespace Ballware.Storage.Jobs.Internal;

public class TemporaryCleanupJob : IJob
{
    public static readonly JobKey Key = new JobKey("cleanup", "temporary");

    private ITemporaryRepository Repository { get; }
    private ITemporaryStorageProvider StorageProvider { get; }
    
    public TemporaryCleanupJob(ITemporaryRepository repository, ITemporaryStorageProvider storageProvider) 
    {
        Repository = repository;
        StorageProvider = storageProvider;
    }
    
    public async Task Execute(IJobExecutionContext context)
    {
        var expiredTemporaries = await Repository.AllExpired();

        foreach (var expired in expiredTemporaries)
        {
            await StorageProvider.DropByPathAsync(expired.TenantId, expired.Entry.StoragePath);
            
            await Repository.RemoveAsync(expired.TenantId, null, ImmutableDictionary<string, object>.Empty,
                new Dictionary<string, object>
                {
                    { "Id", expired.Entry.Id }
                });
        }
    }
}