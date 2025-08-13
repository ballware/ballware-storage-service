using System.ComponentModel.DataAnnotations;

namespace Ballware.Storage.Service.Configuration;

public class StorageOptions
{
    [Required]
    public required string Provider { get; set; }
}