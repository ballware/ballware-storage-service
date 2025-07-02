using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Ballware.Storage.Data.Persistables;

[Table("temporary")]
public class Temporary : IEntity, IAuditable, ITenantable
{
    [JsonIgnore]
    [Column("id")]
    public virtual long? Id { get; set; }

    [JsonPropertyName(nameof(Id))]
    [Column("uuid")]
    public virtual Guid Uuid { get; set; }

    [JsonIgnore]
    [Column("tenant_id")]
    public virtual Guid TenantId { get; set; }
    
    [JsonIgnore]
    [Column("creator_id")]
    public virtual Guid? CreatorId { get; set; }

    [JsonIgnore]
    [Column("create_stamp")]
    public virtual DateTime? CreateStamp { get; set; }

    [JsonIgnore]
    [Column("last_changer_id")]
    public virtual Guid? LastChangerId { get; set; }

    [JsonIgnore]
    [Column("last_change_stamp")]
    public virtual DateTime? LastChangeStamp { get; set; }
    
    [Required]
    [Column("file_name")]
    public string? FileName { get; set; }
    
    [Required]
    [Column("content_type")]
    public string? ContentType { get; set; }
    
    [Required]
    [Column("storage_path")]
    public string? StoragePath { get; set; }
    
    [Required]
    [Column("expiry_date")]
    public DateTime ExpiryDate { get; set; }
    
    [Required]
    [Column("file_size")]
    public long FileSize { get; set; }
}