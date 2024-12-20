using Ballware.Storage.Provider;
using Ballware.Storage.Service.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Storage.Service.Tests;

[TestFixture]
[Category("UnitTest")]
public class StartupTest
{
    [Test]
    public void Startup_missing_configuration_throws_configuration_exception()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings_missing_authorization.json"), optional: false);

        var startup = new Startup(builder.Environment, builder.Configuration, builder.Services);

        Assert.Throws<ConfigurationException>(() => startup.InitializeServices());
    }

    [Test]
    public void Startup_complete_configuration_succeeds()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings_complete.json"), optional: false);

        var startup = new Startup(builder.Environment, builder.Configuration, builder.Services);

        startup.InitializeServices();

        var app = builder.Build();

        startup.InitializeApp(app);

        Assert.That(app.Services.GetService<IFileStorage>(), Is.Not.Null);
    }
}