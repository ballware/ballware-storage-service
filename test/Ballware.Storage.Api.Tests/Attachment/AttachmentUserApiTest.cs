using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Ballware.Shared.Authorization;
using Ballware.Storage.Api.Endpoints;
using Ballware.Storage.Api.Tests.Utils;
using Ballware.Storage.Data.Repository;
using Ballware.Storage.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ballware.Storage.Api.Tests.Attachment;

public class AttachmentUserApiTest : ApiMappingBaseTest
{
    [Test]
    public async Task HandleAllForEntityAndOwner_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();

        var expectedEntries = new List<Data.Public.Attachment>()
        {
            new Data.Public.Attachment
            {
                Id = Guid.NewGuid(),
                Entity = expectedEntity,
                OwnerId = expectedOwnerId,
                FileName = "file_1.txt",
                ContentType = "plain/text",
                FileSize = 312,
                StoragePath = "fake/storage/path/file_1.txt"
            },
            new Data.Public.Attachment
            {
                Id = Guid.NewGuid(),
                Entity = expectedEntity,
                OwnerId = expectedOwnerId,
                FileName = "file_2.txt",
                ContentType = "plain/text",
                FileSize = 512,
                StoragePath = "fake/storage/path/file_2.txt"
            },
            new Data.Public.Attachment
            {
                Id = Guid.NewGuid(),
                Entity = expectedEntity,
                OwnerId = expectedOwnerId,
                FileName = "file_3.txt",
                ContentType = "plain/text",
                FileSize = 311,
                StoragePath = "fake/storage/path/file_3.txt"
            }
        };
        
        var principalUtilsMock = new Mock<IPrincipalUtils>();
        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();
        
        principalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        attachmentRepositoryMock
            .Setup(r => r.AllByEntityAndOwnerIdAsync(expectedTenantId, expectedEntity, expectedOwnerId))
            .ReturnsAsync(expectedEntries);
        
        var client = await CreateApplicationClientAsync("storageApi", services =>
        {
            services.AddSingleton(principalUtilsMock.Object);
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentUserApi("attachment");
            });
        });
        
        // Act
        var response = await client.GetAsync($"attachment/allforentityandowner/{expectedEntity}/{expectedOwnerId}");
        
        // Assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            
            var result = JsonSerializer.Deserialize<IEnumerable<Data.Public.Attachment>>(await response.Content.ReadAsStringAsync())?.ToList();

            Assert.That(DeepComparer.AreListsEqual(expectedEntries, result, TestContext.WriteLine));
        });
    }
    
    [Test]
    public async Task HandleDownloadForTenantEntityAndOwnerById_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();
        var expectedAttachmentId = Guid.NewGuid();
        var expectedFileName = "file_1.txt";
        var expectedMediaType = "application/json";
        var expectedStoragePath = "fake/storage/path/file_1.txt"; 
        var expectedFilePayload = Encoding.UTF8.GetBytes("{ \"key\": \"value\" }");

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
            .Setup(p => p.DownloadForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId,
                expectedStoragePath))
            .ReturnsAsync(new MemoryStream(expectedFilePayload));
        
        attachmentRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(),expectedAttachmentId))
            .ReturnsAsync(expectedEntry);
        
        var client = await CreateApplicationClientAsync("storageApi", services =>
        {
            services.AddSingleton(principalUtilsMock.Object);
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentUserApi("attachment");
            });
        });
        
        // Act
        var response = await client.GetAsync($"attachment/downloadforentityandownerbyid/{expectedTenantId}/{expectedEntity}/{expectedOwnerId}/{expectedAttachmentId}");
        
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
    public async Task HandleDownloadForTenantEntityAndOwnerById_AttachmentNotFound()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();
        var expectedAttachmentId = Guid.NewGuid();
        
        var principalUtilsMock = new Mock<IPrincipalUtils>();
        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();
        
        principalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        attachmentRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(),expectedAttachmentId))
            .ReturnsAsync(null as Data.Public.Attachment);
        
        var client = await CreateApplicationClientAsync("storageApi", services =>
        {
            services.AddSingleton(principalUtilsMock.Object);
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentUserApi("attachment");
            });
        });
        
        // Act
        var response = await client.GetAsync($"attachment/downloadforentityandownerbyid/{expectedTenantId}/{expectedEntity}/{expectedOwnerId}/{expectedAttachmentId}");
        
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleDownloadForTenantEntityAndOwnerById_FileNotFound()
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
            .Setup(p => p.DownloadForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId,
                expectedStoragePath))
            .ReturnsAsync(null as Stream);
        
        attachmentRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(),expectedAttachmentId))
            .ReturnsAsync(expectedEntry);
        
        var client = await CreateApplicationClientAsync("storageApi", services =>
        {
            services.AddSingleton(principalUtilsMock.Object);
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentUserApi("attachment");
            });
        });
        
        // Act
        var response = await client.GetAsync($"attachment/downloadforentityandownerbyid/{expectedTenantId}/{expectedEntity}/{expectedOwnerId}/{expectedAttachmentId}");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleUploadForEntityAndOwner_NewSucceeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();
        var expectedAttachmentId = Guid.NewGuid();
        var expectedFileName = "file_1.txt";
        var expectedMediaType = "application/json";
        var expectedStoragePath = "fake/storage/path/file_1.txt"; 
        var expectedFilePayload = Encoding.UTF8.GetBytes("{ \"key\": \"value\" }");

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
            .Setup(p => p.UploadForEntityAndOwnerAsync(expectedTenantId, expectedEntity, expectedOwnerId,
                expectedFileName, expectedMediaType, It.IsAny<Stream>()))
            .Callback((Guid tenantId, string entity, Guid ownerId, string fileName, string mediaType,
                Stream stream) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenantId, Is.EqualTo(expectedTenantId));
                    Assert.That(entity, Is.EqualTo(expectedEntity));
                    Assert.That(ownerId, Is.EqualTo(expectedOwnerId));
                    Assert.That(fileName, Is.EqualTo(expectedFileName));
                    Assert.That(mediaType, Is.EqualTo(expectedMediaType));
                    
                    using var streamContent = new MemoryStream();
                    stream.CopyTo(streamContent);
                    Assert.That(streamContent.ToArray(), Is.EqualTo(expectedFilePayload));
                });
            });
        
        attachmentRepositoryMock
            .Setup(r => r.SingleByEntityOwnerAndFileNameAsync(expectedTenantId, expectedEntity, expectedOwnerId,
                expectedFileName))
            .ReturnsAsync(null as Data.Public.Attachment);
        
        attachmentRepositoryMock
            .Setup(r => r.NewAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync(expectedEntry);

        attachmentRepositoryMock
            .Setup(r => r.SaveAsync(expectedTenantId, It.IsAny<Guid>(), "primary",
                It.IsAny<IDictionary<string, object>>(), expectedEntry))
            .Callback((Guid _, Guid? _, string _, IDictionary<string, object> _, Data.Public.Attachment attachment) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(attachment.Id, Is.EqualTo(expectedEntry.Id));
                    Assert.That(attachment.Entity, Is.EqualTo(expectedEntry.Entity));
                    Assert.That(attachment.OwnerId, Is.EqualTo(expectedEntry.OwnerId));
                    Assert.That(attachment.FileName, Is.EqualTo(expectedFileName));
                    Assert.That(attachment.ContentType, Is.EqualTo(expectedEntry.ContentType));
                    Assert.That(attachment.FileSize, Is.EqualTo(expectedEntry.FileSize));
                });
            });
        
        var client = await CreateApplicationClientAsync("storageApi", services =>
        {
            services.AddSingleton(principalUtilsMock.Object);
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentUserApi("attachment");
            });
        });
        
        // Act
        var payload = new MultipartFormDataContent();

        var content = new StreamContent(new MemoryStream(expectedFilePayload));

        content.Headers.ContentType = new MediaTypeHeaderValue(expectedMediaType);
        
        payload.Add(content, "files", expectedFileName);
        
        var response = await client.PostAsync($"attachment/uploadforentityandowner/{expectedEntity}/{expectedOwnerId}", payload);
        
        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        
        storageProviderMock.Verify(p => p.UploadForEntityAndOwnerAsync(expectedTenantId, expectedEntity, expectedOwnerId, expectedFileName, expectedMediaType, It.IsAny<Stream>()), Times.Once);
    }
    
    [Test]
    public async Task HandleUploadForEntityAndOwner_UpdateExistingSucceeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();
        var expectedAttachmentId = Guid.NewGuid();
        var expectedFileName = "file_1.txt";
        var expectedMediaType = "application/json";
        var expectedStoragePath = "fake/storage/path/file_1.txt"; 
        var expectedFilePayload = Encoding.UTF8.GetBytes("{ \"key\": \"value\" }");

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
            .Setup(p => p.UploadForEntityAndOwnerAsync(expectedTenantId, expectedEntity, expectedOwnerId,
                expectedFileName, expectedMediaType, It.IsAny<Stream>()))
            .Callback((Guid tenantId, string entity, Guid ownerId, string fileName, string mediaType,
                Stream stream) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenantId, Is.EqualTo(expectedTenantId));
                    Assert.That(entity, Is.EqualTo(expectedEntity));
                    Assert.That(ownerId, Is.EqualTo(expectedOwnerId));
                    Assert.That(fileName, Is.EqualTo(expectedFileName));
                    Assert.That(mediaType, Is.EqualTo(expectedMediaType));
                    
                    using var streamContent = new MemoryStream();
                    stream.CopyTo(streamContent);
                    Assert.That(streamContent.ToArray(), Is.EqualTo(expectedFilePayload));
                });
            });
        
        attachmentRepositoryMock
            .Setup(r => r.SingleByEntityOwnerAndFileNameAsync(expectedTenantId, expectedEntity, expectedOwnerId,
                expectedFileName))
            .ReturnsAsync(expectedEntry);
        
        attachmentRepositoryMock
            .Setup(r => r.SaveAsync(expectedTenantId, It.IsAny<Guid>(), "primary",
                It.IsAny<IDictionary<string, object>>(), expectedEntry))
            .Callback((Guid _, Guid? _, string _, IDictionary<string, object> _, Data.Public.Attachment attachment) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(attachment.Id, Is.EqualTo(expectedEntry.Id));
                    Assert.That(attachment.Entity, Is.EqualTo(expectedEntry.Entity));
                    Assert.That(attachment.OwnerId, Is.EqualTo(expectedEntry.OwnerId));
                    Assert.That(attachment.FileName, Is.EqualTo(expectedFileName));
                    Assert.That(attachment.ContentType, Is.EqualTo(expectedEntry.ContentType));
                    Assert.That(attachment.FileSize, Is.EqualTo(expectedEntry.FileSize));
                });
            });
        
        var client = await CreateApplicationClientAsync("storageApi", services =>
        {
            services.AddSingleton(principalUtilsMock.Object);
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentUserApi("attachment");
            });
        });
        
        // Act
        var payload = new MultipartFormDataContent();

        var content = new StreamContent(new MemoryStream(expectedFilePayload));

        content.Headers.ContentType = new MediaTypeHeaderValue(expectedMediaType);
        
        payload.Add(content, "files", expectedFileName);
        
        var response = await client.PostAsync($"attachment/uploadforentityandowner/{expectedEntity}/{expectedOwnerId}", payload);
        
        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        
        storageProviderMock.Verify(p => p.UploadForEntityAndOwnerAsync(expectedTenantId, expectedEntity, expectedOwnerId, expectedFileName, expectedMediaType, It.IsAny<Stream>()), Times.Once);
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
            services.AddSingleton(principalUtilsMock.Object);
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
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
    
    [Test]
    public async Task HandleDropForEntityAndOwnerById_AttachmentNotFound()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();
        var expectedAttachmentId = Guid.NewGuid();
        var expectedStoragePath = "fake/storage/path/file_1.txt"; 

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
            .ReturnsAsync(null as Data.Public.Attachment);
        
        var client = await CreateApplicationClientAsync("storageApi", services =>
        {
            services.AddSingleton(principalUtilsMock.Object);
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
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
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
        
        storageProviderMock.Verify(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId, expectedStoragePath), Times.Never);
    }
    
    [Test]
    public async Task HandleDropAllForEntityAndOwner_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();

        var expectedEntries = new List<Data.Public.Attachment>()
        {
            new Data.Public.Attachment
            {
                Id = Guid.NewGuid(),
                Entity = expectedEntity,
                OwnerId = expectedOwnerId,
                FileName = "file_1.txt",
                ContentType = "plain/text",
                FileSize = 312,
                StoragePath = "fake/storage/path/file_1.txt"
            },
            new Data.Public.Attachment
            {
                Id = Guid.NewGuid(),
                Entity = expectedEntity,
                OwnerId = expectedOwnerId,
                FileName = "file_2.txt",
                ContentType = "plain/text",
                FileSize = 512,
                StoragePath = "fake/storage/path/file_2.txt"
            },
            new Data.Public.Attachment
            {
                Id = Guid.NewGuid(),
                Entity = expectedEntity,
                OwnerId = expectedOwnerId,
                FileName = "file_3.txt",
                ContentType = "plain/text",
                FileSize = 311,
                StoragePath = "fake/storage/path/file_3.txt"
            }
        };
        
        var principalUtilsMock = new Mock<IPrincipalUtils>();
        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();
        
        principalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);

        storageProviderMock
            .Setup(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId, It.IsAny<string>()));
        
        attachmentRepositoryMock
            .Setup(r => r.AllByEntityAndOwnerIdAsync(expectedTenantId, expectedEntity, expectedOwnerId))
            .ReturnsAsync(expectedEntries);

        attachmentRepositoryMock
            .Setup(r => r.RemoveAsync(expectedTenantId, It.IsAny<Guid>(), It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IDictionary<string, object>>()));
        
        var client = await CreateApplicationClientAsync("storageApi", services =>
        {
            services.AddSingleton(principalUtilsMock.Object);
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentUserApi("attachment");
            });
        });
        
        // Act
        var response = await client.DeleteAsync($"attachment/dropallforentityandowner/{expectedEntity}/{expectedOwnerId}");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        
        attachmentRepositoryMock
            .Verify(r => r.RemoveAsync(expectedTenantId, It.IsAny<Guid>(), It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IDictionary<string, object>>()), Times.Exactly(3));
        storageProviderMock.Verify(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId, It.IsAny<string>()), Times.Exactly(3));
    }
}