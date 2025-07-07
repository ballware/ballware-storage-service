using Ballware.Storage.Data.Public;
using Ballware.Storage.Data.Repository;
using Ballware.Storage.Jobs.Internal;
using Ballware.Storage.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Quartz;

namespace Ballware.Storage.Jobs.Tests;

public class TemporaryCleanupJobTest
{
    private Mock<ITemporaryRepository> TemporaryRepositoryMock { get; set; }
    private Mock<ITemporaryStorageProvider> StorageProviderMock { get; set; }
    private Mock<IJobExecutionContext> JobExecutionContextMock { get; set; }
    private ServiceProvider ServiceProvider { get; set; }
    
    [SetUp]
    public void Setup()
    {   
        TemporaryRepositoryMock = new Mock<ITemporaryRepository>();
        StorageProviderMock = new Mock<ITemporaryStorageProvider>();
        
        JobExecutionContextMock = new Mock<IJobExecutionContext>();
        
        var triggerMock = new Mock<ITrigger>();
        
        triggerMock
            .Setup(trigger => trigger.JobKey)
            .Returns(JobKey.Create("cleanup", "temporary"));
        
        JobExecutionContextMock
            .Setup(c => c.Trigger)
            .Returns(triggerMock.Object);
        
        var serviceCollection = new ServiceCollection();
        
        ServiceProvider = serviceCollection
            .BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        ServiceProvider.Dispose();
    }
    
    [Test]
    public async Task Execute_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        
        var expectedExpiredList = new List<Temporary>()
        {
            new Temporary()
            {
                Id = Guid.NewGuid(),
                ContentType = "application/json",
                FileName = "temporary1.json",
                FileSize = 300,
                ExpiryDate = DateTime.UtcNow.AddMinutes(-10),
                StoragePath = "path/to/tempoary1.json"
            },
            new Temporary()
            {
                Id = Guid.NewGuid(),
                ContentType = "application/json",
                FileName = "temporary2.json",
                FileSize = 300,
                ExpiryDate = DateTime.UtcNow.AddMinutes(-10),
                StoragePath = "path/to/tempoary2.json"
            },
            new Temporary()
            {
                Id = Guid.NewGuid(),
                ContentType = "application/json",
                FileName = "temporary3.json",
                FileSize = 300,
                ExpiryDate = DateTime.UtcNow.AddMinutes(-10),
                StoragePath = "path/to/tempoary3.json"
            }
        };
        
        TemporaryRepositoryMock
            .Setup(r => r.AllExpiredAsync())
            .ReturnsAsync(expectedExpiredList.Select(tx => (TenantId: expectedTenantId, Entry: tx)));
        
        var jobDataMap = new JobDataMap();

        JobExecutionContextMock
            .Setup(c => c.MergedJobDataMap)
            .Returns(jobDataMap);
        
        var job = new TemporaryCleanupJob(TemporaryRepositoryMock.Object, StorageProviderMock.Object);
        
        // Act
        await job.Execute(JobExecutionContextMock.Object);
        
        // Assert
        TemporaryRepositoryMock
            .Verify(r => r.RemoveAsync(expectedTenantId, null, It.IsAny<IDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>()), Times.Exactly(3));
        
        StorageProviderMock
            .Verify(p => p.DropByPathAsync(expectedTenantId, It.IsAny<string>()), Times.Exactly(3));
    }
}