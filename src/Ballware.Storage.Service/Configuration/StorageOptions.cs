using System.ComponentModel.DataAnnotations;

namespace Ballware.Storage.Service.Configuration;

public class StorageOptions
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
    
    [Required]
    public string Share { get; set; } = string.Empty;
}