using System.Collections.Immutable;
using Ballware.Storage.Data.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Storage.Data.Ef.SqlServer.Tests.Repository;

public class TemporaryRepositoryTest : RepositoryBaseTest
{
    [Test]
    public async Task Save_and_remove_value_succeeds()
    {
        using var scope = Application.Services.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<ITemporaryRepository>();

        var expectedEntity = "fake_entity";
        var expectedOwnerId = Guid.NewGuid();
        
        var expectedValue = await repository.NewQueryAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, ImmutableDictionary<string, object>.Empty);
        
        expectedValue.ContentType = "text/plain";
        expectedValue.FileName = "fake_file.txt";
        expectedValue.FileSize = 128;
        expectedValue.StoragePath = $"{TenantId}/{expectedEntity}/{expectedOwnerId}/fake_file.txt";
        
        await repository.SaveAsync(TenantId, null, "primary", ImmutableDictionary<string, object>.Empty, expectedValue);

        var actualValue = await repository.ByIdAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, expectedValue.Id);

        Assert.Multiple(() =>
        {
            Assert.That(actualValue, Is.Not.Null);
            Assert.That(actualValue?.Id, Is.EqualTo(expectedValue.Id));
            Assert.That(actualValue?.ContentType, Is.EqualTo(expectedValue.ContentType));
            Assert.That(actualValue?.FileName, Is.EqualTo(expectedValue.FileName));
            Assert.That(actualValue?.FileSize, Is.EqualTo(expectedValue.FileSize));
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

        var repository = scope.ServiceProvider.GetRequiredService<ITemporaryRepository>();

        var fakeTenantIds = new[] { Guid.NewGuid(), Guid.NewGuid(), TenantId, Guid.NewGuid() };
        
        foreach (var fakeTenant in fakeTenantIds)
        {
            for (var i = 0; i < 10; i++)
            {
                var fakeValue = await repository.NewAsync(fakeTenant, "primary", ImmutableDictionary<string, object>.Empty);

                fakeValue.ContentType = "text/plain";
                fakeValue.FileName = $"fake_file_{i}.txt";
                fakeValue.FileSize = 128;
                fakeValue.StoragePath = $"{TenantId}/temporary/{fakeValue.FileName}";
                
                await repository.SaveAsync(fakeTenant, null, "primary", ImmutableDictionary<string, object>.Empty, fakeValue);
            }
        }

        var actualTenantItemsCount = await repository.CountAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, ImmutableDictionary<string, object>.Empty);
        var actualTenantAllItems = await repository.AllAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty);
        var actualTenantQueryItems = await repository.QueryAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, ImmutableDictionary<string, object>.Empty);
        
        Assert.Multiple(() =>
        {
            Assert.That(actualTenantItemsCount, Is.EqualTo(10));
            Assert.That(actualTenantAllItems.Count(), Is.EqualTo(10));
            Assert.That(actualTenantQueryItems.Count(), Is.EqualTo(10));
        });
    }
    
    [Test]
    public async Task Query_tenant_expired_items_succeeds()
    {
        using var scope = Application.Services.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<ITemporaryRepository>();

        var fakeTenantIds = new[] { Guid.NewGuid(), Guid.NewGuid(), TenantId, Guid.NewGuid() };
        
        foreach (var fakeTenant in fakeTenantIds)
        {
            for (var i = 0; i < 10; i++)
            {
                var fakeValue = await repository.NewAsync(fakeTenant, "primary", ImmutableDictionary<string, object>.Empty);

                fakeValue.ContentType = "text/plain";
                fakeValue.FileName = $"fake_file_{i}.txt";
                fakeValue.FileSize = 128;
                fakeValue.StoragePath = $"{TenantId}/temporary/{fakeValue.FileName}";
                fakeValue.ExpiryDate = DateTime.Now.AddDays(1);

                if (i == 8)
                {
                    fakeValue.ExpiryDate = DateTime.Now.AddDays(-1);
                }
                
                await repository.SaveAsync(fakeTenant, null, "primary", ImmutableDictionary<string, object>.Empty, fakeValue);
            }
        }

        var actualTenantItemsCount = await repository.CountAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, ImmutableDictionary<string, object>.Empty);
        var expiredItems = await repository.AllExpired();
        
        Assert.Multiple(() =>
        {
            Assert.That(actualTenantItemsCount, Is.EqualTo(10));
            Assert.That(expiredItems.Count(), Is.EqualTo(4));
        });
    }
}