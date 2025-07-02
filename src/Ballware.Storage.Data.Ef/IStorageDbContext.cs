using Ballware.Storage.Data.Persistables;
using Microsoft.EntityFrameworkCore;

namespace Ballware.Storage.Data.Ef;

public interface IStorageDbContext
{
    DbSet<Attachment> Attachments { get; }
    DbSet<Temporary> Temporaries { get; }
    
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}