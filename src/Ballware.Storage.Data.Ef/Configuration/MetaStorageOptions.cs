using System.ComponentModel.DataAnnotations;

namespace Ballware.Storage.Data.Ef.Configuration;

public sealed class MetaStorageOptions
{
    [Required]
    public required string Provider { get; set; }
    public bool AutoMigrations { get; set; } = false;
}