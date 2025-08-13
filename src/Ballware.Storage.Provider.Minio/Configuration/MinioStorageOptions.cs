namespace Ballware.Storage.Provider.Minio.Configuration;

public class MinioStorageOptions
{
    public required string Endpoint { get; set; }
    public required string AccessKey { get; set; }
    public required string SecretKey { get; set; }
    public required string BucketName { get; set; }
    public bool UseSSL { get; set; } = true;
}