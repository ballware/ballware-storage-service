using System.Text;
using Ballware.Storage.Metadata;
using Ballware.Storage.Provider.Azure.Internal;
using Ballware.Storage.Provider.Azure.Tests.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Storage.Provider.Azure.Tests.Attachment;

public class AttachmentStorageProviderTest : AzuriteBackedBaseTest
{
    private WebApplication Application { get; set; }
    
    [SetUp]
    public void Setup()
    {
        base.SetupApplicationBuilder();

        PreparedBuilder.Services.AddBallwareAzureBlobStorage(Options);
        
        Application = PreparedBuilder.Build();
    }

    [TearDown]
    public async Task TearDown()
    {
        await Application.DisposeAsync();
    }
    
    [Test]
    public async Task UploadDownloadAndDropForEntityAndOwner_Succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();
        var expectedFileName = "testfile.txt";
        var expectedContentType = "plain/text";
        var expectedFilePayload = Encoding.UTF8.GetBytes("Fake text content");
        
        using var scope = Application.Services.CreateScope();
        
        var provider = scope.ServiceProvider.GetRequiredService<IAttachmentStorageProvider>();
        
        // Act
        var actualStoragePath = await provider.UploadForEntityAndOwnerAsync(expectedTenantId, expectedEntity, expectedOwnerId, expectedFileName, expectedContentType, new MemoryStream(expectedFilePayload));
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actualStoragePath, Is.Not.Null);
            Assert.That(actualStoragePath, Is.EqualTo($"{expectedTenantId}/{expectedEntity}/{expectedOwnerId}/{expectedFileName}"));    
        });
        
        // Act
        var actualStream = await provider.DownloadForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId, actualStoragePath);
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actualStream, Is.Not.Null);
            
            using var streamContent = new MemoryStream();
            actualStream?.CopyTo(streamContent);
            Assert.That(streamContent.ToArray(), Is.EqualTo(expectedFilePayload));
        });
        
        // Act
        await provider.DropForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId, actualStoragePath);
        
        actualStream = await provider.DownloadForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId, actualStoragePath);
        
        // Assert
        Assert.That(actualStream, Is.Null);
    }
}