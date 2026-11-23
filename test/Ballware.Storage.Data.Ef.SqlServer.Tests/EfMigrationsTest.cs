using Ballware.Storage.Data.Ef.Configuration;
using Ballware.Storage.Data.Ef.SqlServer.Tests.Utils;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Storage.Data.Ef.SqlServer.Tests;

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

        PreparedBuilder.Services.AddBallwareMetaStorageForSqlServer(storageOptions, connectionString);
        var mapsterConfig = new TypeAdapterConfig()
            .AddBallwareMetaStorageMappings();
        
        PreparedBuilder.Services.AddSingleton(mapsterConfig);
        PreparedBuilder.Services.AddScoped<IMapper, ServiceMapper>();

        var app = PreparedBuilder.Build();

        await app.StartAsync();
        await app.StopAsync();
    }
}