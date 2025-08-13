using System.Text;
using Ballware.Storage.Metadata;
using Ballware.Storage.Provider.Minio.Tests.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Storage.Provider.Minio.Tests.Temporary;

public class TemporaryStorageProviderTest : MinioBackedBaseTest
{
    private WebApplication Application { get; set; }
    
    [SetUp]
    public void Setup()
    {
        base.SetupApplicationBuilder();

        PreparedBuilder.Services.AddBallwareMinioBlobStorage(Options);
        
        Application = PreparedBuilder.Build();
    }

    [TearDown]
    public async Task TearDown()
    {
        await Application.DisposeAsync();
    }
    
    [Test]
    public async Task UploadDownloadAndDropTemporary_Succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedTemporaryId = Guid.NewGuid();
        var expectedFileName = "testfile.txt";
        var expectedContentType = "plain/text";
        var expectedFilePayload = Encoding.UTF8.GetBytes("Fake text content");
        
        using var scope = Application.Services.CreateScope();
        
        var provider = scope.ServiceProvider.GetRequiredService<ITemporaryStorageProvider>();
        
        // Act
        var actualStoragePath = await provider.UploadForIdAsync(expectedTenantId, expectedTemporaryId, expectedFileName, expectedContentType, new MemoryStream(expectedFilePayload));
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actualStoragePath, Is.Not.Null);
            Assert.That(actualStoragePath, Is.EqualTo($"{expectedTenantId}/temporary/{expectedTemporaryId}/{expectedFileName}"));    
        });
        
        // Act
        var actualStream = await provider.DownloadByPathAsync(expectedTenantId, actualStoragePath);
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actualStream, Is.Not.Null);
            
            using var streamContent = new MemoryStream();
            actualStream?.CopyTo(streamContent);
            Assert.That(streamContent.ToArray(), Is.EqualTo(expectedFilePayload));
        });
        
        // Act
        await provider.DropByPathAsync(expectedTenantId, actualStoragePath);
        
        actualStream = await provider.DownloadByPathAsync(expectedTenantId, actualStoragePath);
        
        // Assert
        Assert.That(actualStream, Is.Null);
    }
}