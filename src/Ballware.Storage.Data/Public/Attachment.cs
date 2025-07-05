using Ballware.Shared.Data.Public;

namespace Ballware.Storage.Data.Public;

public class Attachment : IEditable
{
    public Guid Id { get; set; }
    
    public required string Entity { get; set; }
    
    public required Guid OwnerId { get; set; }
    
    public required string FileName { get; set; }
    
    public required string ContentType { get; set; }
    
    public long FileSize { get; set; }
    
    public required string StoragePath { get; set; }
}