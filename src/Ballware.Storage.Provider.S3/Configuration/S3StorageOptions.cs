using System.ComponentModel.DataAnnotations;

namespace Ballware.Storage.Provider.S3.Configuration;

public class S3StorageOptions : IValidatableObject
{
    // host:port without scheme — the scheme is derived exclusively from UseSSL
    [Required] public required string Endpoint { get; set; }
    [Required] public required string AccessKey { get; set; }
    [Required] public required string SecretKey { get; set; }
    [Required] public required string BucketName { get; set; }
    public bool UseSSL { get; set; } = true;
    public string Region { get; set; } = "us-east-1";
    public bool ForcePathStyle { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Endpoint.Contains("://", StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                $"{nameof(S3StorageOptions)}.{nameof(Endpoint)} must be host:port without a URI scheme; the scheme is derived from {nameof(UseSSL)}.",
                new[] { nameof(Endpoint) });
        }
    }
}
