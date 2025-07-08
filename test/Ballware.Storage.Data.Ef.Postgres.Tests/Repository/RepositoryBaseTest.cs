using System.Diagnostics;
using Ballware.Storage.Data.Ef.Configuration;
using Ballware.Storage.Data.Ef.Postgres.Tests.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ballware.Storage.Data.Ef.Postgres.Tests.Repository;

public sealed class NUnitLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new NUnitLogger(categoryName);

    public void Dispose() { }

    private class NUnitLogger : ILogger
    {
        private readonly string _categoryName;

        public NUnitLogger(string categoryName)
        {
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception, string> formatter)
        {
            if (exception != null)
            {
                TestContext.Progress.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");    
            }
        }
    }
}

public class RepositoryBaseTest : DatabaseBackedBaseTest
{
    protected Guid TenantId { get; private set; }

    protected WebApplication Application { get; private set; }
    
    [OneTimeSetUp]
    public async Task SetupApplication()
    {
        await base.MssqlSetUp();
        
        Trace.Listeners.Add(new ConsoleTraceListener());
    }

    [OneTimeTearDown]
    public async Task TearDownApplication()
    {
        await Application.DisposeAsync();
        await base.MssqlTearDown();
    }

    [SetUp]
    public async Task SetupTenantId()
    {
        base.SetupApplicationBuilder();
        
        var storageOptions = PreparedBuilder.Configuration.GetSection("Meta").Get<MetaStorageOptions>();
        var connectionString = MasterConnectionString;

        Assert.Multiple(() =>
        {
            Assert.That(storageOptions, Is.Not.Null);
            Assert.That(connectionString, Is.Not.Null);
        });

        PreparedBuilder.Services.AddLogging(config =>
        {
            config.AddProvider(new NUnitLoggerProvider());
        });
            
        PreparedBuilder.Services.AddBallwareMetaStorageForPostgres(storageOptions, connectionString);
        PreparedBuilder.Services.AddAutoMapper(config =>
        {
            config.AddBallwareMetaStorageMappings();
        });

        Application = PreparedBuilder.Build();
        
        TenantId = Guid.NewGuid();

        using var scope = Application.Services.CreateScope();
        
        var dbContext = scope.ServiceProvider.GetRequiredService<StorageDbContext>();

        await dbContext.Database.MigrateAsync();
    }
    
    [TearDown]
    public async Task TearDownTenantId()
    {
        await Application.DisposeAsync();
    }
}