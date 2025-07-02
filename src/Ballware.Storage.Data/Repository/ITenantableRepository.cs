namespace Ballware.Storage.Data.Repository;

public struct RemoveResult<TEditable> where TEditable : class
{
    public bool Result { get; init; }
    public IEnumerable<string> Messages { get; init; }
    public TEditable? Value { get; init; }
}

public struct ExportResult
{
    public string FileName { get; init; }
    public string MediaType { get; init; }
    public byte[] Data { get; init; }
}

public interface ITenantableRepository<TEditable> where TEditable : class
{
    Task<IEnumerable<TEditable>> AllAsync(Guid tenantId, string identifier, IDictionary<string, object> claims);
    Task<IEnumerable<TEditable>> QueryAsync(Guid tenantId, string identifier, IDictionary<string, object> claims, IDictionary<string, object> queryParams);

    Task<long> CountAsync(Guid tenantId, string identifier, IDictionary<string, object> claims, IDictionary<string, object> queryParams);

    Task<TEditable?> ByIdAsync(Guid tenantId, string identifier, IDictionary<string, object> claims, Guid id);
    Task<TEditable> NewAsync(Guid tenantId, string identifier, IDictionary<string, object> claims);
    Task<TEditable> NewQueryAsync(Guid tenantId, string identifier, IDictionary<string, object> claims, IDictionary<string, object> queryParams);

    Task SaveAsync(Guid tenantId, Guid? userId, string identifier, IDictionary<string, object> claims, TEditable value);

    Task<RemoveResult<TEditable>> RemoveAsync(Guid tenantId, Guid? userId, IDictionary<string, object> claims, IDictionary<string, object> removeParams);

    Task ImportAsync(Guid tenantId,
        Guid? userId,
        string identifier,
        IDictionary<string, object> claims,
        Stream importStream,
        Func<TEditable, Task<bool>> authorized);

    Task<ExportResult> ExportAsync(Guid tenantId, string identifier, IDictionary<string, object> claims, IDictionary<string, object> queryParams);
}