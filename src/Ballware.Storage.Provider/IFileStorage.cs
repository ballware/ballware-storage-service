namespace Ballware.Storage.Provider;

public interface IFileStorage
{
    Task<IEnumerable<FileMetadata>> EnumerateFilesAsync(string owner);

    Task<Stream?> OpenFileAsync(string owner, string fileName);

    Task UploadFileAsync(string owner, string fileName, Stream content);

    Task DropFileAsync(string owner, string fileName);

    Task DropAllAsync(string owner);
}