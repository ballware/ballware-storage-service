using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Ballware.Storage.Data.Public;

public class Temporary : IEditable
{
    public Guid Id { get; set; }
    
    public required string FileName { get; set; }
    
    public required string ContentType { get; set; }
    
    public required DateTime ExpiryDate { get; set; }
    
    public required long FileSize { get; set; }
    
    public required string StoragePath { get; set; }
}