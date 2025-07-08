using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace Ballware.Storage.Data.Ef.SqlServer.Tests.Utils;

public class DelayWaitStrategy : IWaitUntil
{
    private readonly TimeSpan _delay;

    public DelayWaitStrategy(TimeSpan delay)
    {
        _delay = delay;
    }

    public async Task<bool> UntilAsync(IContainer container)
    {
        await Task.Delay(_delay);
        return true;
    }
}