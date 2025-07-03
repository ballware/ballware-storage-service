using System.Net;
using System.Net.Http.Headers;
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

public class TemporaryServiceApiTest : ApiMappingBaseTest
{
    [Test]
    public async Task HandleDownloadForTenantById_succeeds()
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
        
        var storageProviderMock = new Mock<ITemporaryStorageProvider>();
        var temporaryRepositoryMock = new Mock<ITemporaryRepository>();
        
        storageProviderMock
            .Setup(p => p.DownloadByPathAsync(expectedTenantId, 
                expectedStoragePath))
            .ReturnsAsync(new MemoryStream(expectedFilePayload));
        
        temporaryRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(), expectedTemporaryId))
            .ReturnsAsync(expectedEntry);
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(temporaryRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapTemporaryServiceApi("temporary");
            });
        });
        
        // Act
        var response = await client.GetAsync($"temporary/downloadfortenantbyid/{expectedTenantId}/{expectedTemporaryId}");
        
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
    public async Task HandleDownloadForTenantById_TemporaryNotFound()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedTemporaryId = Guid.NewGuid();
        
        var storageProviderMock = new Mock<ITemporaryStorageProvider>();
        var temporaryRepositoryMock = new Mock<ITemporaryRepository>();
        
        temporaryRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(), expectedTemporaryId))
            .ReturnsAsync(null as Data.Public.Temporary);
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(temporaryRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapTemporaryServiceApi("temporary");
            });
        });
        
        // Act
        var response = await client.GetAsync($"temporary/downloadfortenantbyid/{expectedTenantId}/{expectedTemporaryId}");
        
        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleDownloadForTenantById_FileNotFound()
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
        
        var storageProviderMock = new Mock<ITemporaryStorageProvider>();
        var temporaryRepositoryMock = new Mock<ITemporaryRepository>();

        storageProviderMock
            .Setup(p => p.DownloadByPathAsync(expectedTenantId, 
                expectedStoragePath))
            .ReturnsAsync(null as Stream);
        
        temporaryRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(), expectedTemporaryId))
            .ReturnsAsync(expectedEntry);
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(temporaryRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapTemporaryServiceApi("temporary");
            });
        });
        
        // Act
        var response = await client.GetAsync($"temporary/downloadfortenantbyid/{expectedTenantId}/{expectedTemporaryId}");
        
        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleUploadForTenantAndIdBehalfOfUser_NewSucceeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
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
        
        var storageProviderMock = new Mock<ITemporaryStorageProvider>();
        var temporaryRepositoryMock = new Mock<ITemporaryRepository>();
        
        storageProviderMock
            .Setup(p => p.UploadForIdAsync(expectedTenantId, expectedTemporaryId,
                expectedFileName, expectedMediaType, It.IsAny<Stream>()))
            .Callback((Guid tenantId, Guid id, string fileName, string mediaType,
                Stream stream) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenantId, Is.EqualTo(expectedTenantId));
                    Assert.That(id, Is.EqualTo(expectedTemporaryId));
                    Assert.That(fileName, Is.EqualTo(expectedFileName));
                    Assert.That(mediaType, Is.EqualTo(expectedMediaType));
                    
                    using var streamContent = new MemoryStream();
                    stream.CopyTo(streamContent);
                    Assert.That(streamContent.ToArray(), Is.EqualTo(expectedFilePayload));
                });
            });
        
        temporaryRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(),
                expectedTemporaryId))
            .ReturnsAsync(null as Data.Public.Temporary);
        
        temporaryRepositoryMock
            .Setup(r => r.NewAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync(expectedEntry);

        temporaryRepositoryMock
            .Setup(r => r.SaveAsync(expectedTenantId, It.IsAny<Guid>(), "primary",
                It.IsAny<IDictionary<string, object>>(), expectedEntry))
            .Callback((Guid tenantId, Guid? userId, string identifier, IDictionary<string, object> claims, Data.Public.Temporary temporary) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(temporary.Id, Is.EqualTo(expectedEntry.Id));
                    Assert.That(temporary.FileName, Is.EqualTo(expectedFileName));
                    Assert.That(temporary.ContentType, Is.EqualTo(expectedEntry.ContentType));
                    Assert.That(temporary.FileSize, Is.EqualTo(expectedEntry.FileSize));
                });
            });
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(temporaryRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapTemporaryServiceApi("temporary");
            });
        });
        
        // Act
        var payload = new MultipartFormDataContent();

        var content = new StreamContent(new MemoryStream(expectedFilePayload));

        content.Headers.ContentType = new MediaTypeHeaderValue(expectedMediaType);
        
        payload.Add(content, "files", expectedFileName);
        
        var response = await client.PostAsync($"temporary/uploadfortenantandidbehalfofuser/{expectedTenantId}/{expectedUserId}/{expectedTemporaryId}", payload);
        
        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        
        storageProviderMock.Verify(p => p.UploadForIdAsync(expectedTenantId, expectedTemporaryId, expectedFileName, expectedMediaType, It.IsAny<Stream>()), Times.Once);
    }
    
    [Test]
    public async Task HandleUploadForTenantAndIdBehalfOfUser_UpdateExistingSucceeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
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
        
        var storageProviderMock = new Mock<ITemporaryStorageProvider>();
        var temporaryRepositoryMock = new Mock<ITemporaryRepository>();
        
        storageProviderMock
            .Setup(p => p.UploadForIdAsync(expectedTenantId, expectedTemporaryId,
                expectedFileName, expectedMediaType, It.IsAny<Stream>()))
            .Callback((Guid tenantId, Guid id, string fileName, string mediaType,
                Stream stream) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenantId, Is.EqualTo(expectedTenantId));
                    Assert.That(id, Is.EqualTo(expectedTemporaryId));
                    Assert.That(fileName, Is.EqualTo(expectedFileName));
                    Assert.That(mediaType, Is.EqualTo(expectedMediaType));
                    
                    using var streamContent = new MemoryStream();
                    stream.CopyTo(streamContent);
                    Assert.That(streamContent.ToArray(), Is.EqualTo(expectedFilePayload));
                });
            });
        
        temporaryRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(),
                expectedTemporaryId))
            .ReturnsAsync(expectedEntry);
        
        temporaryRepositoryMock
            .Setup(r => r.SaveAsync(expectedTenantId, It.IsAny<Guid>(), "primary",
                It.IsAny<IDictionary<string, object>>(), expectedEntry))
            .Callback((Guid tenantId, Guid? userId, string identifier, IDictionary<string, object> claims, Data.Public.Temporary temporary) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(temporary.Id, Is.EqualTo(expectedEntry.Id));
                    Assert.That(temporary.FileName, Is.EqualTo(expectedFileName));
                    Assert.That(temporary.ContentType, Is.EqualTo(expectedEntry.ContentType));
                    Assert.That(temporary.FileSize, Is.EqualTo(expectedEntry.FileSize));
                });
            });
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(temporaryRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapTemporaryServiceApi("temporary");
            });
        });
        
        // Act
        var payload = new MultipartFormDataContent();

        var content = new StreamContent(new MemoryStream(expectedFilePayload));

        content.Headers.ContentType = new MediaTypeHeaderValue(expectedMediaType);
        
        payload.Add(content, "files", expectedFileName);
        
        var response = await client.PostAsync($"temporary/uploadfortenantandidbehalfofuser/{expectedTenantId}/{expectedUserId}/{expectedTemporaryId}", payload);
        
        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        
        storageProviderMock.Verify(p => p.UploadForIdAsync(expectedTenantId, expectedTemporaryId, expectedFileName, expectedMediaType, It.IsAny<Stream>()), Times.Once);
    }
    
    [Test]
    public async Task HandleDropForTenantAndIdBehalfOfUser_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
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
        
        var storageProviderMock = new Mock<ITemporaryStorageProvider>();
        var temporaryRepositoryMock = new Mock<ITemporaryRepository>();
        
        storageProviderMock
            .Setup(p => p.DropByPathAsync(expectedTenantId, expectedStoragePath));
        
        temporaryRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(), expectedTemporaryId))
            .ReturnsAsync(expectedEntry);
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(temporaryRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapTemporaryServiceApi("temporary");
            });
        });
        
        // Act
        var response = await client.DeleteAsync($"temporary/dropfortenantandidbehalfofuser/{expectedTenantId}/{expectedUserId}/{expectedTemporaryId}");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        
        storageProviderMock.Verify(p => p.DropByPathAsync(expectedTenantId, expectedStoragePath), Times.Once);
    }
    
    [Test]
    public async Task HandleDropForTenantAndIdBehalfOfUser_TemporaryNotFound()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedTemporaryId = Guid.NewGuid();
        var expectedStoragePath = "fake/storage/path/file_1.txt"; 

        var storageProviderMock = new Mock<ITemporaryStorageProvider>();
        var temporaryRepositoryMock = new Mock<ITemporaryRepository>();
        
        storageProviderMock
            .Setup(p => p.DropByPathAsync(expectedTenantId, expectedStoragePath));
        
        temporaryRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(), expectedTemporaryId))
            .ReturnsAsync(null as Data.Public.Temporary);
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(temporaryRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapTemporaryServiceApi("temporary");
            });
        });
        
        // Act
        var response = await client.DeleteAsync($"temporary/dropfortenantandidbehalfofuser/{expectedTenantId}/{expectedUserId}/{expectedTemporaryId}");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
        
        storageProviderMock.Verify(p => p.DropByPathAsync(expectedTenantId, expectedStoragePath), Times.Never);
    }
}