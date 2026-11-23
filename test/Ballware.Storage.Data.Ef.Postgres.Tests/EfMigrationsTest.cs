using Ballware.Storage.Data.Ef.Configuration;
using Ballware.Storage.Data.Ef.Postgres.Tests.Utils;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Storage.Data.Ef.Postgres.Tests;

[TestFixture]
public class EfMigrationsTest : DatabaseBackedBaseTest
{
    [Test]
    public async Task Initialization_with_migrations_up_succeeds()
    {
        var storageOptions = PreparedBuilder.Configuration.GetSection("Meta").Get<MetaStorageOptions>();
        var connectionString = MasterConnectionString;

        Assert.Multiple(() =>
        {
            Assert.That(storageOptions, Is.Not.Null);
            Assert.That(connectionString, Is.Not.Null);
        });

        PreparedBuilder.Services.AddBallwareMetaStorageForPostgres(storageOptions, connectionString);
        var mapsterConfig = new TypeAdapterConfig()
            .AddBallwareMetaStorageMappings();
        
        PreparedBuilder.Services.AddSingleton(mapsterConfig);
        PreparedBuilder.Services.AddScoped<IMapper, ServiceMapper>();

        var app = PreparedBuilder.Build();

        await app.StartAsync();
        await app.StopAsync();
    }
}