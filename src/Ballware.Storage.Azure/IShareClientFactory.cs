using Azure.Storage.Files.Shares;

namespace Ballware.Storage.Azure;

public interface IShareClientFactory
{
    ShareClient GetFileShare(string connectionString, string shareName);
}