using Ballware.Storage.Provider;

namespace Ballware.Storage.Azure.Internal;

class AzureFileStorage : IFileStorage
{
    private IShareClientFactory ClientFactory { get; }
    private string ConnectionString { get; }
    private string Share { get; }

    public AzureFileStorage(IShareClientFactory clientFactory, string connectionString, string share)
    {
        ClientFactory = clientFactory;
        ConnectionString = connectionString;
        Share = share;
    }

    public async Task<IEnumerable<FileMetadata>> EnumerateFilesAsync(string owner)
    {
        var files = new List<FileMetadata>();

        var share = ClientFactory.GetFileShare(ConnectionString, Share);

        var contextDirectory =
            share.GetDirectoryClient(owner);

        if (await contextDirectory.ExistsAsync())
        {
            var contextItems = contextDirectory.GetFilesAndDirectoriesAsync();

            await foreach (var item in contextItems)
            {
                if (!item.IsDirectory)
                {
                    files.Add(new FileMetadata() { Filename = item.Name });
                }
            }
        }

        return files;
    }

    public async Task<Stream?> OpenFileAsync(string owner, string fileName)
    {
        var share = ClientFactory.GetFileShare(ConnectionString, Share);

        var contextDirectory =
            share.GetDirectoryClient(owner);

        if (await contextDirectory.ExistsAsync())
        {
            var fileRef = contextDirectory.GetFileClient(fileName);

            if (await fileRef.ExistsAsync())
            {
                var stream = await fileRef.OpenReadAsync();

                stream.Position = 0;

                return stream;
            }
        }

        return null;
    }

    public async Task UploadFileAsync(string owner, string fileName, Stream content)
    {
        var share = ClientFactory.GetFileShare(ConnectionString, Share);

        var contextDirectory =
            share.GetDirectoryClient(owner);

        if (!await contextDirectory.ExistsAsync())
            await contextDirectory.CreateAsync();

        var fileRef = contextDirectory.GetFileClient(fileName);

        if (!await fileRef.ExistsAsync())
        {
            await fileRef.UploadAsync(content);
        }
    }

    public async Task DropFileAsync(string owner, string fileName)
    {
        var share = ClientFactory.GetFileShare(ConnectionString, Share);

        var contextDirectory =
            share.GetDirectoryClient(owner);

        if (!await contextDirectory.ExistsAsync())
        {
            await contextDirectory.CreateAsync();
        }

        var fileRef = contextDirectory.GetFileClient(fileName);

        await fileRef.DeleteIfExistsAsync();
    }

    public async Task DropAllAsync(string owner)
    {
        var share = ClientFactory.GetFileShare(ConnectionString, Share);

        var contextDirectory =
            share.GetDirectoryClient(owner);

        await contextDirectory.DeleteIfExistsAsync();
    }
}