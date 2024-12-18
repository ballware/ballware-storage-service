namespace Ballware.Storage.Service.Tests.Helper;

public class MockedServicesRepository
{
    private Dictionary<Type, (Type, object)> Services { get; } = new Dictionary<Type, (Type, object)>();

    public IEnumerable<(Type, object)> GetMocks()
    {
        return Services.Values;
    }

    public void AddMock<T>(object v)
    {
        Services[typeof(T)] = (typeof(T), v);
    }
}