using System.Collections.Immutable;
using System.Text;
using Ballware.Storage.Data.Public;
using Ballware.Storage.Data.Repository;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Ballware.Storage.Data.Ef.SqlServer.Tests.Repository;

public class AttachmentRepositoryTest : RepositoryBaseTest
{
    [Test]
    public async Task Save_and_remove_value_succeeds()
    {
        using var scope = Application.Services.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IAttachmentRepository>();

        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();
        
        var expectedValue = await repository.NewQueryAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, ImmutableDictionary<string, object>.Empty);
        
        expectedValue.Entity = expectedEntity;
        expectedValue.OwnerId = expectedOwnerId;
        expectedValue.ContentType = "text/plain";
        expectedValue.FileName = "fake_file.txt";
        expectedValue.FileSize = 128;
        expectedValue.StoragePath = $"{TenantId}/{expectedEntity}/{expectedOwnerId}/fake_file.txt";
        
        await repository.SaveAsync(TenantId, null, "primary", ImmutableDictionary<string, object>.Empty, expectedValue);

        var actualValue = await repository.ByIdAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, expectedValue.Id);
        var actualValueByFileName = await repository.SingleByEntityOwnerAndFileNameAsync(TenantId, expectedEntity, expectedOwnerId, "fake_file.txt");
        
        Assert.Multiple(() =>
        {
            Assert.That(actualValue, Is.Not.Null);
            Assert.That(actualValue?.Id, Is.EqualTo(expectedValue.Id));
            Assert.That(actualValue?.Entity, Is.EqualTo(expectedValue.Entity));
            Assert.That(actualValue?.OwnerId, Is.EqualTo(expectedValue.OwnerId));
            Assert.That(actualValue?.ContentType, Is.EqualTo(expectedValue.ContentType));
            Assert.That(actualValue?.FileName, Is.EqualTo(expectedValue.FileName));
            Assert.That(actualValue?.FileSize, Is.EqualTo(expectedValue.FileSize));
            Assert.That(actualValue?.StoragePath, Is.EqualTo(expectedValue.StoragePath));
            
            Assert.That(actualValueByFileName, Is.Not.Null);
            Assert.That(actualValueByFileName?.Id, Is.EqualTo(expectedValue.Id));
            Assert.That(actualValueByFileName?.Entity, Is.EqualTo(expectedValue.Entity));
            Assert.That(actualValueByFileName?.OwnerId, Is.EqualTo(expectedValue.OwnerId));
            Assert.That(actualValueByFileName?.ContentType, Is.EqualTo(expectedValue.ContentType));
            Assert.That(actualValueByFileName?.FileName, Is.EqualTo(expectedValue.FileName));
            Assert.That(actualValueByFileName?.FileSize, Is.EqualTo(expectedValue.FileSize));
            Assert.That(actualValueByFileName?.StoragePath, Is.EqualTo(expectedValue.StoragePath));
        });

        actualValue.FileSize = 256;
        
        await repository.SaveAsync(TenantId, null, "primary", ImmutableDictionary<string, object>.Empty, actualValue);

        actualValue = await repository.ByIdAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, expectedValue.Id);
        
        Assert.Multiple(() =>
        {
            Assert.That(actualValue, Is.Not.Null);
            Assert.That(actualValue?.Id, Is.EqualTo(expectedValue.Id));
            Assert.That(actualValue?.Entity, Is.EqualTo(expectedValue.Entity));
            Assert.That(actualValue?.OwnerId, Is.EqualTo(expectedValue.OwnerId));
            Assert.That(actualValue?.ContentType, Is.EqualTo(expectedValue.ContentType));
            Assert.That(actualValue?.FileName, Is.EqualTo(expectedValue.FileName));
            Assert.That(actualValue?.FileSize, Is.EqualTo(256));
            Assert.That(actualValue?.StoragePath, Is.EqualTo(expectedValue.StoragePath));
        });

        
        var removeParams = new Dictionary<string, object>([new KeyValuePair<string, object>("Id", expectedValue.Id)]);

        var removeResult = await repository.RemoveAsync(TenantId, null, ImmutableDictionary<string, object>.Empty, removeParams);

        Assert.Multiple(() =>
        {
            Assert.That(removeResult.Result, Is.True);
        });

        actualValue = await repository.ByIdAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, expectedValue.Id);
        
        Assert.That(actualValue, Is.Null);
    }

    [Test]
    public async Task Query_tenant_items_succeeds()
    {
        using var scope = Application.Services.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IAttachmentRepository>();

        var fakeTenantIds = new[] { Guid.NewGuid(), Guid.NewGuid(), TenantId, Guid.NewGuid() };
        var fakeOwnerId = Guid.NewGuid();
        
        foreach (var fakeTenant in fakeTenantIds)
        {
            for (var i = 0; i < 10; i++)
            {
                var fakeValue = await repository.NewAsync(fakeTenant, "primary", ImmutableDictionary<string, object>.Empty);

                fakeValue.Entity = $"fake_entity";
                fakeValue.OwnerId = fakeOwnerId;
                fakeValue.ContentType = "text/plain";
                fakeValue.FileName = $"fake_file_{i}.txt";
                fakeValue.FileSize = 128;
                fakeValue.StoragePath = $"{TenantId}/{fakeValue.Entity}/{fakeValue.OwnerId}/{fakeValue.FileName}";
                
                await repository.SaveAsync(fakeTenant, null, "primary", ImmutableDictionary<string, object>.Empty, fakeValue);
            }
        }

        var actualTenantItemsCount = await repository.CountAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, ImmutableDictionary<string, object>.Empty);
        var actualTenantAllItems = await repository.AllAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty);
        var actualTenantQueryItems = await repository.QueryAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, ImmutableDictionary<string, object>.Empty);
        var actualOwnerItems = await repository.AllByEntityAndOwnerIdAsync(TenantId, "fake_entity", fakeOwnerId);
        var actualEntityItems = await repository.AllByEntityAsync(TenantId, "fake_entity");
        var actualTenantItems = await repository.AllAsync(TenantId);

        Assert.Multiple(() =>
        {
            Assert.That(actualTenantItemsCount, Is.EqualTo(10));
            Assert.That(actualTenantAllItems.Count(), Is.EqualTo(10));
            Assert.That(actualTenantQueryItems.Count(), Is.EqualTo(10));
            Assert.That(actualOwnerItems.Count(), Is.EqualTo(10));
            Assert.That(actualEntityItems.Count(), Is.EqualTo(10));
            Assert.That(actualTenantItems.Count(), Is.EqualTo(10));
        });
    }
    
    [Test]
    public async Task Import_values_succeeds()
    {
        var fakeOwnerId = Guid.NewGuid();
        
        using var scope = Application.Services.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IAttachmentRepository>();

        var importList = new List<Attachment>();

        for (var i = 0; i < 10; i++)
        {
            var fakeValue = await repository.NewAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty);

            fakeValue.Entity = $"fake_entity";
            fakeValue.OwnerId = fakeOwnerId;
            fakeValue.ContentType = "text/plain";
            fakeValue.FileName = $"fake_file_{i}.txt";
            fakeValue.FileSize = 128;
            fakeValue.StoragePath = $"{TenantId}/{fakeValue.Entity}/{fakeValue.OwnerId}/{fakeValue.FileName}";

            importList.Add(fakeValue);
        }

        var importBinary = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(importList));

        using var importStream = new MemoryStream(importBinary);

        await repository.ImportAsync(TenantId, null, "primary", ImmutableDictionary<string, object>.Empty, importStream, (_) => Task.FromResult(true));

        var actualTenantItemsCount = await repository.CountAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, ImmutableDictionary<string, object>.Empty);
        var actualTenantAllItems = await repository.AllAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty);
        var actualTenantQueryItems = await repository.QueryAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, ImmutableDictionary<string, object>.Empty);
        var actualOwnerItems = await repository.AllByEntityAndOwnerIdAsync(TenantId, "fake_entity", fakeOwnerId);
        
        Assert.Multiple(() =>
        {
            Assert.That(actualTenantItemsCount, Is.EqualTo(10));
            Assert.That(actualTenantAllItems.Count(), Is.EqualTo(10));
            Assert.That(actualTenantQueryItems.Count(), Is.EqualTo(10));
            Assert.That(actualOwnerItems.Count(), Is.EqualTo(10));
        });
    }

    [Test]
    public async Task Export_values_succeeds()
    {
        var fakeOwnerId = Guid.NewGuid();
        
        using var scope = Application.Services.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IAttachmentRepository>();

        var exportIdList = new List<Guid>();
        var exportItemList = new List<Attachment>();

        for (var i = 0; i < 10; i++)
        {
            var fakeValue = await repository.NewAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty);

            fakeValue.Entity = $"fake_entity";
            fakeValue.OwnerId = fakeOwnerId;
            fakeValue.ContentType = "text/plain";
            fakeValue.FileName = $"fake_file_{i}.txt";
            fakeValue.FileSize = 128;
            fakeValue.StoragePath = $"{TenantId}/{fakeValue.Entity}/{fakeValue.OwnerId}/{fakeValue.FileName}";

            await repository.SaveAsync(TenantId, null, "primary", ImmutableDictionary<string, object>.Empty, fakeValue);

            if (i % 2 == 0)
            {
                exportIdList.Add(fakeValue.Id);
                exportItemList.Add(fakeValue);
            }
        }

        var exportResult = await repository.ExportAsync(TenantId, "exportjson", ImmutableDictionary<string, object>.Empty, new Dictionary<string, object>(new[] { new KeyValuePair<string, object>("id", exportIdList.Select(id => id.ToString()).ToArray()) }));

        Assert.Multiple(() =>
        {
            Assert.That(exportResult.FileName, Is.EqualTo("exportjson.json"));
            Assert.That(exportResult.MediaType, Is.EqualTo("application/json"));
            Assert.That(exportResult.Data, Is.Not.Null);

            using var inputStream = new MemoryStream(exportResult.Data);
            using var streamReader = new StreamReader(inputStream);

            var actualItems = JsonConvert.DeserializeObject<IEnumerable<Attachment>>(streamReader.ReadToEnd())?.ToList();

            Assert.That(actualItems, Is.Not.Null);
            Assert.That(actualItems?.Count, Is.EqualTo(5));
            Assert.That(actualItems?.Select(item => item.Id), Is.EquivalentTo(exportItemList.Select(item => item.Id)));
        });
    }
}