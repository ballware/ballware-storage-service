using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ballware.Storage.Api.Endpoints;
using Ballware.Storage.Api.Tests.Utils;
using Ballware.Storage.Data.Repository;
using Ballware.Storage.Metadata;
using Bogus;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ballware.Storage.Api.Tests.Attachment;

public class AttachmentServiceApiTest : ApiMappingBaseTest
{
    [Test]
    public async Task HandleAllForTenantEntityAndOwner_succeeds()
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
        
        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();
        
        attachmentRepositoryMock
            .Setup(r => r.AllByEntityAndOwnerIdAsync(expectedTenantId, expectedEntity, expectedOwnerId))
            .ReturnsAsync(expectedEntries);
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentServiceApi("attachment");
            });
        });
        
        // Act
        var response = await client.GetAsync($"attachment/allfortenantentityandowner/{expectedTenantId}/{expectedEntity}/{expectedOwnerId}");
        
        // Assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            
            var result = JsonSerializer.Deserialize<IEnumerable<Data.Public.Attachment>>(await response.Content.ReadAsStringAsync())?.ToList();

            Assert.That(DeepComparer.AreListsEqual(expectedEntries, result, TestContext.WriteLine));
        });
    }
    
    [Test]
    public async Task HandleDownloadForTenantEntityAndOwner_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();
        var expectedAttachmentId = Guid.NewGuid();
        var expectedFilename = "file_1.txt";
        var expectedMediaType = "application/json";
        var expectedStoragePath = "fake/storage/path/file_1.txt"; 
        var expectedFilePayload = Encoding.UTF8.GetBytes("{ \"key\": \"value\" }");
        
        var expectedEntry = new Data.Public.Attachment
        {
            Id = expectedAttachmentId,
            Entity = expectedEntity,
            OwnerId = expectedOwnerId,
            FileName = expectedFilename,
            ContentType = expectedMediaType,
            FileSize = 312,
            StoragePath = expectedStoragePath
        };
        
        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();

        storageProviderMock
            .Setup(p => p.DownloadForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId,
                expectedStoragePath))
            .ReturnsAsync(() => new MemoryStream(expectedFilePayload));
        
        attachmentRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(),expectedAttachmentId))
            .ReturnsAsync(expectedEntry);
        
        attachmentRepositoryMock
            .Setup(r => r.SingleByEntityOwnerAndFileNameAsync(expectedTenantId, expectedEntity, expectedOwnerId, expectedFilename))
            .ReturnsAsync(expectedEntry);

        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentServiceApi("attachment");
            });
        });
        
        // Act
        var idResponse = await client.GetAsync($"attachment/downloadfortenantentityandownerbyid/{expectedTenantId}/{expectedEntity}/{expectedOwnerId}/{expectedAttachmentId}");
        var filenameResponse = await client.GetAsync($"attachment/downloadfortenantentityandownerbyfilename/{expectedTenantId}/{expectedEntity}/{expectedOwnerId}/{expectedFilename}");
        
        // Assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(idResponse.StatusCode,Is.EqualTo(HttpStatusCode.OK));
            Assert.That(idResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo(expectedMediaType));
            Assert.That(idResponse.Content.Headers.ContentDisposition, Is.Not.Null);
            Assert.That(idResponse.Content.Headers.ContentDisposition!.FileName, Is.EqualTo(expectedFilename));
            
            var idPayload = await idResponse.Content.ReadAsByteArrayAsync();
            
            Assert.That(idPayload, Is.EqualTo(expectedFilePayload));
            
            Assert.That(filenameResponse.StatusCode,Is.EqualTo(HttpStatusCode.OK));
            Assert.That(filenameResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo(expectedMediaType));
            Assert.That(filenameResponse.Content.Headers.ContentDisposition, Is.Not.Null);
            Assert.That(filenameResponse.Content.Headers.ContentDisposition!.FileName, Is.EqualTo(expectedFilename));
            
            var filenamePayload = await filenameResponse.Content.ReadAsByteArrayAsync();
            
            Assert.That(filenamePayload, Is.EqualTo(expectedFilePayload));
        });
    }
    
    [Test]
    public async Task HandleDownloadForTenantEntityAndOwner_AttachmentNotFound()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();
        var expectedAttachmentId = Guid.NewGuid();
        var expectedFilename = "fake_file.txt";
        
        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();
        
        attachmentRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(),expectedAttachmentId))
            .ReturnsAsync(null as Data.Public.Attachment);
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentServiceApi("attachment");
            });
        });
        
        // Act
        var idResponse = await client.GetAsync($"attachment/downloadfortenantentityandownerbyid/{expectedTenantId}/{expectedEntity}/{expectedOwnerId}/{expectedAttachmentId}");
        var filenameResponse = await client.GetAsync($"attachment/downloadfortenantentityandownerbyfilename/{expectedTenantId}/{expectedEntity}/{expectedOwnerId}/{expectedFilename}");
        
        Assert.That(idResponse.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(filenameResponse.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleDownloadForTenantEntityAndOwner_FileNotFound()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();
        var expectedAttachmentId = Guid.NewGuid();
        var expectedFilename = "file_1.txt";
        var expectedMediaType = "application/json";
        var expectedStoragePath = "fake/storage/path/file_1.txt"; 

        var expectedEntry = new Data.Public.Attachment
        {
            Id = expectedAttachmentId,
            Entity = expectedEntity,
            OwnerId = expectedOwnerId,
            FileName = expectedFilename,
            ContentType = expectedMediaType,
            FileSize = 312,
            StoragePath = expectedStoragePath
        };
        
        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();
        
        storageProviderMock
            .Setup(p => p.DownloadForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId,
                expectedStoragePath))
            .ReturnsAsync(null as Stream);
        
        attachmentRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(),expectedAttachmentId))
            .ReturnsAsync(expectedEntry);
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentServiceApi("attachment");
            });
        });
        
        // Act
        var idResponse = await client.GetAsync($"attachment/downloadfortenantentityandownerbyid/{expectedTenantId}/{expectedEntity}/{expectedOwnerId}/{expectedAttachmentId}");
        var filenameResponse = await client.GetAsync($"attachment/downloadfortenantentityandownerbyfilename/{expectedTenantId}/{expectedEntity}/{expectedOwnerId}/{expectedFilename}");
        
        // Assert
        Assert.That(idResponse.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(filenameResponse.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleUploadForTenantEntityAndOwner_NewSucceeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
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
        
        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();
        
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
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentServiceApi("attachment");
            });
        });
        
        // Act
        var payload = new MultipartFormDataContent();

        var content = new StreamContent(new MemoryStream(expectedFilePayload));

        content.Headers.ContentType = new MediaTypeHeaderValue(expectedMediaType);
        
        payload.Add(content, "files", expectedFileName);
        
        var response = await client.PostAsync($"attachment/uploadfortenantentityandownerbehalfofuser/{expectedTenantId}/{expectedUserId}/{expectedEntity}/{expectedOwnerId}", payload);
        
        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        
        storageProviderMock.Verify(p => p.UploadForEntityAndOwnerAsync(expectedTenantId, expectedEntity, expectedOwnerId, expectedFileName, expectedMediaType, It.IsAny<Stream>()), Times.Once);
    }
    
    [Test]
    public async Task HandleUploadForTenantEntityAndOwner_UpdateExistingSucceeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
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
        
        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();
        
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
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentServiceApi("attachment");
            });
        });
        
        // Act
        var payload = new MultipartFormDataContent();

        var content = new StreamContent(new MemoryStream(expectedFilePayload));

        content.Headers.ContentType = new MediaTypeHeaderValue(expectedMediaType);
        
        payload.Add(content, "files", expectedFileName);
        
        var response = await client.PostAsync($"attachment/uploadfortenantentityandownerbehalfofuser/{expectedTenantId}/{expectedUserId}/{expectedEntity}/{expectedOwnerId}", payload);
        
        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        
        storageProviderMock.Verify(p => p.UploadForEntityAndOwnerAsync(expectedTenantId, expectedEntity, expectedOwnerId, expectedFileName, expectedMediaType, It.IsAny<Stream>()), Times.Once);
    }
    
    [Test]
    public async Task HandleDropForTenantEntityAndOwnerById_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
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
        
        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();
        
        storageProviderMock
            .Setup(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId, expectedStoragePath));
        
        attachmentRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(),expectedAttachmentId))
            .ReturnsAsync(expectedEntry);
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentServiceApi("attachment");
            });
        });
        
        // Act
        var response = await client.DeleteAsync($"attachment/dropfortenantentityandownerbyidbehalfofuser/{expectedTenantId}/{expectedUserId}/{expectedEntity}/{expectedOwnerId}/{expectedAttachmentId}");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        
        storageProviderMock.Verify(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId, expectedStoragePath), Times.Once);
    }
    
    [Test]
    public async Task HandleDropForTenantEntityAndOwnerById_AttachmentNotFound()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();
        var expectedAttachmentId = Guid.NewGuid();
        var expectedStoragePath = "fake/storage/path/file_1.txt"; 

        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();

        storageProviderMock
            .Setup(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId, expectedStoragePath));
        
        attachmentRepositoryMock
            .Setup(r => r.ByIdAsync(expectedTenantId, "primary", It.IsAny<IDictionary<string, object>>(),expectedAttachmentId))
            .ReturnsAsync(null as Data.Public.Attachment);
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentServiceApi("attachment");
            });
        });
        
        // Act
        var response = await client.DeleteAsync($"attachment/dropfortenantentityandownerbyidbehalfofuser/{expectedTenantId}/{expectedUserId}/{expectedEntity}/{expectedOwnerId}/{expectedAttachmentId}");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
        
        storageProviderMock.Verify(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId, expectedStoragePath), Times.Never);
    }
    
    [Test]
    public async Task HandleDropAllForTenantEntityAndOwner_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
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
        
        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();
        
        storageProviderMock
            .Setup(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId, It.IsAny<string>()));
        
        attachmentRepositoryMock
            .Setup(r => r.AllByEntityAndOwnerIdAsync(expectedTenantId, expectedEntity, expectedOwnerId))
            .ReturnsAsync(expectedEntries);

        attachmentRepositoryMock
            .Setup(r => r.RemoveAsync(expectedTenantId, It.IsAny<Guid>(), It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IDictionary<string, object>>()));
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentServiceApi("attachment");
            });
        });
        
        // Act
        var response = await client.DeleteAsync($"attachment/dropallfortenantentityandownerbehalfofuser/{expectedTenantId}/{expectedUserId}/{expectedEntity}/{expectedOwnerId}");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        
        attachmentRepositoryMock
            .Verify(r => r.RemoveAsync(expectedTenantId, It.IsAny<Guid>(), It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IDictionary<string, object>>()), Times.Exactly(3));
        storageProviderMock.Verify(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, expectedOwnerId, It.IsAny<string>()), Times.Exactly(3));
    }
    
    [Test]
    public async Task HandleDropAllForTenantAndEntity_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedEntity = "fake_entity";

        var expectedEntries = new List<Data.Public.Attachment>()
        {
            new Data.Public.Attachment
            {
                Id = Guid.NewGuid(),
                Entity = expectedEntity,
                OwnerId = Guid.NewGuid(),
                FileName = "file_1.txt",
                ContentType = "plain/text",
                FileSize = 312,
                StoragePath = "fake/storage/path/file_1.txt"
            },
            new Data.Public.Attachment
            {
                Id = Guid.NewGuid(),
                Entity = expectedEntity,
                OwnerId = Guid.NewGuid(),
                FileName = "file_2.txt",
                ContentType = "plain/text",
                FileSize = 512,
                StoragePath = "fake/storage/path/file_2.txt"
            },
            new Data.Public.Attachment
            {
                Id = Guid.NewGuid(),
                Entity = expectedEntity,
                OwnerId = Guid.NewGuid(),
                FileName = "file_3.txt",
                ContentType = "plain/text",
                FileSize = 311,
                StoragePath = "fake/storage/path/file_3.txt"
            }
        };
        
        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();
        
        storageProviderMock
            .Setup(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, It.IsAny<Guid>(), It.IsAny<string>()));
        
        attachmentRepositoryMock
            .Setup(r => r.AllByEntityAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(expectedEntries);

        attachmentRepositoryMock
            .Setup(r => r.RemoveAsync(expectedTenantId, It.IsAny<Guid>(), It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IDictionary<string, object>>()));
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentServiceApi("attachment");
            });
        });
        
        // Act
        var response = await client.DeleteAsync($"attachment/dropallfortenantandentitybehalfofuser/{expectedTenantId}/{expectedUserId}/{expectedEntity}");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        
        attachmentRepositoryMock
            .Verify(r => r.RemoveAsync(expectedTenantId, It.IsAny<Guid>(), It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IDictionary<string, object>>()), Times.Exactly(3));
        storageProviderMock.Verify(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, expectedEntity, It.IsAny<Guid>(), It.IsAny<string>()), Times.Exactly(3));
    }
    
    [Test]
    public async Task HandleDropAllForTenant_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var faker = new Faker();

        var expectedEntries = new List<Data.Public.Attachment>()
        {
            new Data.Public.Attachment
            {
                Id = Guid.NewGuid(),
                Entity = faker.Random.String(),
                OwnerId = Guid.NewGuid(),
                FileName = "file_1.txt",
                ContentType = "plain/text",
                FileSize = 312,
                StoragePath = "fake/storage/path/file_1.txt"
            },
            new Data.Public.Attachment
            {
                Id = Guid.NewGuid(),
                Entity = faker.Random.String(),
                OwnerId = Guid.NewGuid(),
                FileName = "file_2.txt",
                ContentType = "plain/text",
                FileSize = 512,
                StoragePath = "fake/storage/path/file_2.txt"
            },
            new Data.Public.Attachment
            {
                Id = Guid.NewGuid(),
                Entity = faker.Random.String(),
                OwnerId = Guid.NewGuid(),
                FileName = "file_3.txt",
                ContentType = "plain/text",
                FileSize = 311,
                StoragePath = "fake/storage/path/file_3.txt"
            }
        };
        
        var storageProviderMock = new Mock<IAttachmentStorageProvider>();
        var attachmentRepositoryMock = new Mock<IAttachmentRepository>();
        
        storageProviderMock
            .Setup(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()));
        
        attachmentRepositoryMock
            .Setup(r => r.AllAsync(expectedTenantId))
            .ReturnsAsync(expectedEntries);

        attachmentRepositoryMock
            .Setup(r => r.RemoveAsync(expectedTenantId, It.IsAny<Guid>(), It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IDictionary<string, object>>()));
        
        var client = await CreateApplicationClientAsync("serviceApi", services =>
        {
            services.AddSingleton(storageProviderMock.Object);
            services.AddSingleton(attachmentRepositoryMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAttachmentServiceApi("attachment");
            });
        });
        
        // Act
        var response = await client.DeleteAsync($"attachment/dropallfortenantbehalfofuser/{expectedTenantId}/{expectedUserId}");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        
        attachmentRepositoryMock
            .Verify(r => r.RemoveAsync(expectedTenantId, It.IsAny<Guid>(), It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IDictionary<string, object>>()), Times.Exactly(3));
        storageProviderMock.Verify(p => p.DropForEntityAndOwnerByPathAsync(expectedTenantId, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Exactly(3));
    }
}