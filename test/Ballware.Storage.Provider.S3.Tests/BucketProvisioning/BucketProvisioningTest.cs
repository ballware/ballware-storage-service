using System.Net;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Ballware.Storage.Metadata;
using Ballware.Storage.Provider.S3.Configuration;
using Ballware.Storage.Provider.S3.Internal;
using Ballware.Storage.Provider.S3.Tests.Utils;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ballware.Storage.Provider.S3.Tests.BucketProvisioning;

public class BucketProvisioningTest : SeaweedFsBackedBaseTest
{
    private S3StorageOptions BuildAutoBucketOptions()
    {
        return new S3StorageOptions
        {
            Endpoint = Options.Endpoint,
            AccessKey = Options.AccessKey,
            SecretKey = Options.SecretKey,
            BucketName = "storage-auto",
            UseSSL = false,
            Region = "us-east-1",
            ForcePathStyle = true
        };
    }

    [Test]
    public async Task UploadDownloadAndDropWithAutomaticBucketCreation_Succeeds()
    {
        // bucket "storage-auto" is not provisioned by the container's S3_BUCKET setting —
        // the provider has to create it on first use
        var autoOptions = BuildAutoBucketOptions();

        var services = new ServiceCollection();
        services.AddBallwareS3BlobStorage(autoOptions);
        await using var serviceProvider = services.BuildServiceProvider();

        var expectedTenantId = Guid.NewGuid();
        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();
        var expectedFileName = "autobucket.txt";
        var expectedContentType = "plain/text";
        var expectedFilePayload = Encoding.UTF8.GetBytes("Fake text content");

        string actualStoragePath;

        using (var scope = serviceProvider.CreateScope())
        {
            var provider = scope.ServiceProvider.GetRequiredService<IAttachmentStorageProvider>();

            // Act
            actualStoragePath = await provider.UploadForEntityAndOwnerAsync(expectedTenantId, expectedEntity, expectedOwnerId, expectedFileName, expectedContentType, new MemoryStream(expectedFilePayload));

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
        }

        // second scope: the bucket provisioner is a cached singleton, so this must not
        // re-provision (no renewed HeadBucket/PutBucket) and still work
        var secondOwnerId = Guid.NewGuid();

        using (var scope = serviceProvider.CreateScope())
        {
            var provider = scope.ServiceProvider.GetRequiredService<IAttachmentStorageProvider>();

            // Act
            var secondStoragePath = await provider.UploadForEntityAndOwnerAsync(expectedTenantId, expectedEntity, secondOwnerId, expectedFileName, expectedContentType, new MemoryStream(expectedFilePayload));

            // Assert
            Assert.That(secondStoragePath, Is.EqualTo($"{expectedTenantId}/{expectedEntity}/{secondOwnerId}/{expectedFileName}"));

            // Act
            var secondStream = await provider.DownloadForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, secondOwnerId, secondStoragePath);

            // Assert
            Assert.That(secondStream, Is.Not.Null);

            using var secondContent = new MemoryStream();
            secondStream?.CopyTo(secondContent);
            Assert.That(secondContent.ToArray(), Is.EqualTo(expectedFilePayload));
        }
    }

    [Test]
    public async Task BucketProvisioningIsEnsuredOnceAcrossScopes()
    {
        var options = BuildAutoBucketOptions();

        var clientMock = new Mock<IAmazonS3>();

        clientMock
            .Setup(c => c.HeadBucketAsync(It.IsAny<HeadBucketRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not Found", ErrorType.Sender, "NoSuchBucket", "test-request-id", HttpStatusCode.NotFound));

        clientMock
            .Setup(c => c.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutBucketResponse());

        clientMock
            .Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());

        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton(clientMock.Object);
        services.AddSingleton<S3BucketProvisioner>();
        services.AddScoped<IAttachmentStorageProvider, S3AttachmentStorageProvider>();
        await using var serviceProvider = services.BuildServiceProvider();

        for (var i = 0; i < 2; i++)
        {
            using var scope = serviceProvider.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<IAttachmentStorageProvider>();
            await provider.UploadForEntityAndOwnerAsync(Guid.NewGuid(), "fake_entity", Guid.NewGuid(), "cache.txt", "plain/text", new MemoryStream("Fake text content"u8.ToArray()));
        }

        clientMock.Verify(c => c.HeadBucketAsync(It.IsAny<HeadBucketRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        clientMock.Verify(c => c.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        clientMock.Verify(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
