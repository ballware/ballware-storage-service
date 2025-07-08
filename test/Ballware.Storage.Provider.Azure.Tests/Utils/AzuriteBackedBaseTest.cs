using Ballware.Storage.Provider.Azure.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Testcontainers.Azurite;

namespace Ballware.Storage.Provider.Azure.Tests.Utils;

public abstract class AzuriteBackedBaseTest
{
    private AzuriteContainer? _azuriteContainer;

    protected virtual string AdditionalSettingsFile { get; } = "appsettings.additional.json";
    
    protected WebApplicationBuilder PreparedBuilder { get; set; } = null!;

    protected AzureStorageOptions Options { get; set; } = null!;
    
    [OneTimeSetUp]
    public async Task AzuriteSetUp()
    {   
        _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite")
            .Build();

        await _azuriteContainer.StartAsync();

        Options = new AzureStorageOptions()
        {
            ConnectionString = _azuriteContainer.GetConnectionString(),
            ContainerName = "storage"
        };
    }

    [OneTimeTearDown]
    public async Task AzuriteTearDown()
    {
        if (_azuriteContainer != null)
        {
            await _azuriteContainer.DisposeAsync();
        }
    }

    [SetUp]
    public void SetupApplicationBuilder()
    {
        PreparedBuilder = WebApplication.CreateBuilder();

        PreparedBuilder.Configuration.Sources.Clear();
        PreparedBuilder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), optional: false);
        PreparedBuilder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"appsetting.{PreparedBuilder.Environment.EnvironmentName}.json"), true, true);
        PreparedBuilder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AdditionalSettingsFile), optional: true);
        PreparedBuilder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"appsettings.local.json"), true, true);
        PreparedBuilder.Configuration.AddEnvironmentVariables();
    }
}