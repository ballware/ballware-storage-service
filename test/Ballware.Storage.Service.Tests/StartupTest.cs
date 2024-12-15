using Ballware.Storage.Service.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Ballware.Storage.Service.Tests;

public class StartupTest
{
    [Test]
    public void Startup_missing_configuration_throws_configuration_exception()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings_missing_authorization.json"), optional: false);
        
        var startup = new Startup(builder.Configuration, builder.Services);

        Assert.Throws<ConfigurationException>(() => startup.InitializeServices());
    }
}