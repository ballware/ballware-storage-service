using Azure.Storage.Files.Shares;

namespace Ballware.Storage.Azure.Internal;

class DefaultShareClientFactory : IShareClientFactory
{
    public ShareClient GetFileShare(string connectionString, string shareName)
    {
        return new ShareClient(connectionString, shareName);
    }
}