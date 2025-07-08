using Ballware.Shared.Data.Repository;
using Ballware.Storage.Data.Ef.Configuration;
using Ballware.Storage.Data.Ef.Model;
using Ballware.Storage.Data.Ef.Repository;
using Ballware.Storage.Data.Ef.SqlServer.Internal;
using Ballware.Storage.Data.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Storage.Data.Ef.SqlServer;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareMetaStorageForSqlServer(this IServiceCollection services, MetaStorageOptions options, string connectionString)
    {
        services.AddSingleton(options);
        services.AddDbContext<StorageDbContext>(o =>
        {
            o.UseSqlServer(connectionString, o =>
            {
                o.MigrationsAssembly(typeof(StorageDbContext).Assembly.FullName);
            });

            o.ReplaceService<IModelCustomizer, StorageModelBaseCustomizer>();
        });

        services.AddScoped<IStorageDbContext, StorageDbContext>();
        
        services.AddScoped<ITenantableRepository<Public.Attachment>, AttachmentBaseRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentBaseRepository>();

        services.AddScoped<ITenantableRepository<Public.Temporary>, TemporaryBaseRepository>();
        services.AddScoped<ITemporaryRepository, TemporaryBaseRepository>();
        services.AddHostedService<InitializationWorker>();
        
        return services;
    }
}