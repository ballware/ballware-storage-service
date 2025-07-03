using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ballware.Storage.Data.Persistables;

[Table("attachment")]
public class Attachment : Blob
{
    [Required]
    [Column("entity")]
    public string? Entity { get; set; }
    
    [Required]
    [Column("owner_id")]
    public Guid OwnerId { get; set; }
}