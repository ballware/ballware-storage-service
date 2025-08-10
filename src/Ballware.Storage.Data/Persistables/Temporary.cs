using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ballware.Storage.Data.Persistables;

[Table("temporary")]
public class Temporary : Blob
{
    [Required]
    [Column("expiry_date")]
    public DateTime ExpiryDate { get; set; }
}