namespace Ballware.Storage.Provider.Azure.Configuration;

public class AzureStorageOptions
{
    public required string ConnectionString { get; set; }
    public required string ContainerName { get; set; }
}