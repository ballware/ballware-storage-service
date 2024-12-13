using Ballware.Storage.Provider;
using Azure.Storage.Files.Shares;

namespace Ballware.Storage.Azure.Internal;

class AzureFileStorage : IFileStorage
{
    private ShareClient GetFileShare(string connectionString, string shareName)
    {
        return new ShareClient(connectionString, shareName);
    }

    private string ConnectionString { get; }
    private string Share { get; }

    public AzureFileStorage(string connectionString, string share) {
        ConnectionString = connectionString;
        Share = share;
    }

    public virtual async Task<IEnumerable<FileMetadata>> EnumerateFilesAsync(string owner) {
        
        var files = new List<FileMetadata>();
        
        var share = GetFileShare(ConnectionString, Share);
        
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

    public virtual async Task<Stream?> OpenFileAsync(string owner, string fileName) {
        var share = GetFileShare(ConnectionString, Share);

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

    public virtual async Task UploadFileAsync(string owner, string fileName, Stream content) {
        
        var share = GetFileShare(ConnectionString, Share);

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

    public virtual async Task DropFileAsync(string owner, string fileName) {
        var share = GetFileShare(ConnectionString, Share);

        var contextDirectory =
            share.GetDirectoryClient(owner);

        if (!await contextDirectory.ExistsAsync())
        {
            await contextDirectory.CreateAsync();
        }
            
        var fileRef = contextDirectory.GetFileClient(fileName);

        await fileRef.DeleteIfExistsAsync();
    }

    public virtual async Task DropAllAsync(string owner) {
        
        var share = GetFileShare(ConnectionString, Share);

        var contextDirectory =
            share.GetDirectoryClient(owner);

        await contextDirectory.DeleteIfExistsAsync();
    }
}