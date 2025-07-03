using System.Net;
using System.Security.Claims;
using System.Text;
using Ballware.Storage.Api.Endpoints;
using Ballware.Storage.Api.Tests.Utils;
using Ballware.Storage.Authorization;
using Ballware.Storage.Data.Repository;
using Ballware.Storage.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ballware.Storage.Api.Tests.Temporary;

public class TemporaryUserApiTest : ApiMappingBaseTest
{
    [Test]
    public async Task HandleDownloadById_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedTemporaryId = Guid.NewGuid();
        var expectedFileName = "file_1.txt";
        var expectedMediaType = "application/json";
        var expectedStoragePath = "fake/storage/path/file_1.txt"; 
        var expectedFilePayload = Encoding.UTF8.GetBytes("{ \"key\": \"value\" }");

        var expectedEntry = new Data.Public.Temporary
        {
            Id = expectedTemporaryId,
            FileName = expectedFileName,
            ContentType = expectedMediaType,
            FileSize = 312,
            StoragePath = expectedStoragePath,
            ExpiryDate = DateTime.Now.AddDays(2)
        };
        
        var principalUtilsMock = new Mock<IPrincipalUtils>();
        var storageProviderMock = new Mock<ITemporaryStorageProvider>();
        var temporaryRepositoryMock = new Mock<ITemporaryRepository>();
        
        principalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);

        storageProviderMock
            .Setup(p => p.DownloadByPathAsync(expectedTenantId, 
                expectedStoragePath))
            .ReturnsAsync(new MemoryStream(expectedFilePayload));
        
        temporaryRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(), expectedTemporaryId))
            .ReturnsAsync(expectedEntry);
        
        var client = await CreateApplicationClientAsync("storageApi", services =>
        {
            services.AddSingleton(principalUtilsMock.Object);
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(temporaryRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapTemporaryUserApi("temporary");
            });
        });
        
        // Act
        var response = await client.GetAsync($"temporary/downloadbyid/{expectedTemporaryId}");
        
        // Assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo(expectedMediaType));
            Assert.That(response.Content.Headers.ContentDisposition, Is.Not.Null);
            Assert.That(response.Content.Headers.ContentDisposition!.FileName, Is.EqualTo(expectedFileName));
            
            var payload = await response.Content.ReadAsByteArrayAsync();
            
            Assert.That(payload, Is.EqualTo(expectedFilePayload));
        });
    }
    
    [Test]
    public async Task HandleDownloadById_TemporaryNotFound()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedTemporaryId = Guid.NewGuid();
        
        var principalUtilsMock = new Mock<IPrincipalUtils>();
        var storageProviderMock = new Mock<ITemporaryStorageProvider>();
        var temporaryRepositoryMock = new Mock<ITemporaryRepository>();
        
        principalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        temporaryRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(), expectedTemporaryId))
            .ReturnsAsync(null as Data.Public.Temporary);
        
        var client = await CreateApplicationClientAsync("storageApi", services =>
        {
            services.AddSingleton(principalUtilsMock.Object);
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(temporaryRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapTemporaryUserApi("temporary");
            });
        });
        
        // Act
        var response = await client.GetAsync($"temporary/downloadbyid/{expectedTemporaryId}");
        
        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleDownloadById_FileNotFound()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedTemporaryId = Guid.NewGuid();
        var expectedFileName = "file_1.txt";
        var expectedMediaType = "application/json";
        var expectedStoragePath = "fake/storage/path/file_1.txt"; 

        var expectedEntry = new Data.Public.Temporary
        {
            Id = expectedTemporaryId,
            FileName = expectedFileName,
            ContentType = expectedMediaType,
            FileSize = 312,
            StoragePath = expectedStoragePath,
            ExpiryDate = DateTime.Now.AddDays(2)
        };
        
        var principalUtilsMock = new Mock<IPrincipalUtils>();
        var storageProviderMock = new Mock<ITemporaryStorageProvider>();
        var temporaryRepositoryMock = new Mock<ITemporaryRepository>();
        
        principalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);

        storageProviderMock
            .Setup(p => p.DownloadByPathAsync(expectedTenantId, 
                expectedStoragePath))
            .ReturnsAsync(null as Stream);
        
        temporaryRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(), expectedTemporaryId))
            .ReturnsAsync(expectedEntry);
        
        var client = await CreateApplicationClientAsync("storageApi", services =>
        {
            services.AddSingleton(principalUtilsMock.Object);
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(temporaryRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapTemporaryUserApi("temporary");
            });
        });
        
        // Act
        var response = await client.GetAsync($"temporary/downloadbyid/{expectedTemporaryId}");
        
        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleDropForEntityAndOwnerById_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();
        var expectedAttachmentId = Guid.NewGuid();
        var expectedFileName = "file_1.txt";
        var expectedMediaType = "application/json";
        var expectedStoragePath = "fake/storage/path/file_1.txt"; 

        var expectedEntry = new Data.Public.Attachment
        {
            Id = expectedAttachmentId,
            Entity = expectedEntity,
            OwnerId = expectedOwnerId,
            FileName = expectedFileName,
            ContentType = expectedMediaType,
            FileSize = 312,
            StoragePath = expectedStoragePath
        };
        
        var principalUtilsMock = new Mock<IPrincipalUtils>();
        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();
        
        principalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);

        storageProviderMock
            .Setup(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId, expectedStoragePath));
        
        attachmentRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(),expectedAttachmentId))
            .ReturnsAsync(expectedEntry);
        
        var client = await CreateApplicationClientAsync("storageApi", services =>
        {
            services.AddSingleton<IPrincipalUtils>(principalUtilsMock.Object);
            services.AddSingleton<IAttachmentStorageProvider>(storageProviderMock.Object);
            services.AddSingleton<IAttachmentRepository>(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentUserApi("attachment");
            });
        });
        
        // Act
        var response = await client.DeleteAsync($"attachment/dropforentityandownerbyid/{expectedEntity}/{expectedOwnerId}/{expectedAttachmentId}");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        
        storageProviderMock.Verify(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId, expectedStoragePath), Times.Once);
    }
}