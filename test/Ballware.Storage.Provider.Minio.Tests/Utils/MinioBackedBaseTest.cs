using Ballware.Storage.Provider.Minio.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Testcontainers.Minio;

namespace Ballware.Storage.Provider.Minio.Tests.Utils;

public abstract class MinioBackedBaseTest
{
    private MinioContainer? _minioContainer;

    protected virtual string AdditionalSettingsFile { get; } = "appsettings.additional.json";
    
    protected WebApplicationBuilder PreparedBuilder { get; set; } = null!;

    protected MinioStorageOptions Options { get; set; } = null!;
    
    [OneTimeSetUp]
    public async Task MinioSetUp()
    {   
        var configurationBuilder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), optional: false)
            .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AdditionalSettingsFile), optional: true)
            .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"appsettings.local.json"), true, true)
            .AddEnvironmentVariables();
        
        var config = configurationBuilder.Build();
        
        var accessKey = config.GetValue<string>("TestSettings:AccessKey");
        var secretKey = config.GetValue<string>("TestSettings:SecretKey");
        
        _minioContainer = new MinioBuilder()
            .WithImage("minio/minio:latest")
            .WithUsername(accessKey)
            .WithPassword(secretKey)
            .Build();

        await _minioContainer.StartAsync();

        Options = new MinioStorageOptions()
        {
            Endpoint = $"{_minioContainer.Hostname}:{_minioContainer.GetMappedPublicPort(9000)}",
            BucketName = "storage",
            AccessKey = accessKey,
            SecretKey = secretKey,
            UseSSL = false
        };
    }

    [OneTimeTearDown]
    public async Task AzuriteTearDown()
    {
        if (_minioContainer != null)
        {
            await _minioContainer.DisposeAsync();
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