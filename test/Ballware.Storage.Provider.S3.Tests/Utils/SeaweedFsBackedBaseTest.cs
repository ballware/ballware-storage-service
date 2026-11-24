using Ballware.Storage.Provider.S3.Configuration;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Ballware.Storage.Provider.S3.Tests.Utils;

public abstract class SeaweedFsBackedBaseTest
{
    // tag pinned on purpose, no "latest"
    private const string Image = "chrislusf/seaweedfs:4.55";
    private const int S3Port = 8333;

    private IContainer? _container;

    protected virtual string AdditionalSettingsFile { get; } = "appsettings.additional.json";

    protected WebApplicationBuilder PreparedBuilder { get; set; } = null!;

    protected S3StorageOptions Options { get; set; } = null!;

    [OneTimeSetUp]
    public async Task SeaweedFsSetUp()
    {
        var configurationBuilder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), optional: false)
            .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AdditionalSettingsFile), optional: true)
            .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.local.json"), true, true)
            .AddEnvironmentVariables();

        var config = configurationBuilder.Build();

        var accessKey = config.GetValue<string>("TestSettings:AccessKey");
        var secretKey = config.GetValue<string>("TestSettings:SecretKey");

        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "TestSettings:AccessKey and TestSettings:SecretKey are required for SeaweedFS-backed tests. " +
                "Provide them via appsettings.additional.json, appsettings.local.json, or the " +
                "TestSettings__AccessKey / TestSettings__SecretKey environment variables.");
        }

        _container = new ContainerBuilder(Image)
            .WithCommand("mini", "-dir=/data", "-s3.port=8333")
            .WithEnvironment("AWS_ACCESS_KEY_ID", accessKey)
            .WithEnvironment("AWS_SECRET_ACCESS_KEY", secretKey)
            .WithEnvironment("S3_BUCKET", "storage")
            .WithPortBinding(S3Port, true)
            // with credentials configured SeaweedFS enforces signatures, so GET / answers
            // AccessDenied (403) instead of 200 — any answered HTTP status means "ready"
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r
                .ForPort(S3Port)
                .ForPath("/")
                .ForStatusCodeMatching(code => (int)code >= 200 && (int)code < 500)))
            .Build();

        await _container.StartAsync();

        Options = new S3StorageOptions
        {
            Endpoint = $"{_container.Hostname}:{_container.GetMappedPublicPort(S3Port)}",
            BucketName = "storage",
            AccessKey = accessKey,
            SecretKey = secretKey,
            UseSSL = false,
            Region = "us-east-1",
            ForcePathStyle = true
        };
    }

    [OneTimeTearDown]
    public async Task SeaweedFsTearDown()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
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
        PreparedBuilder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.local.json"), true, true);
        PreparedBuilder.Configuration.AddEnvironmentVariables();
    }
}
