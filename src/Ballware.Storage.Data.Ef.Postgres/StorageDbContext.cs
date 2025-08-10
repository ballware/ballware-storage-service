using Microsoft.EntityFrameworkCore;

namespace Ballware.Storage.Data.Ef.Postgres;

public class StorageDbContext : DbContext, IStorageDbContext
{
    public DbSet<Persistables.Attachment> Attachments => Set<Persistables.Attachment>();
    public DbSet<Persistables.Temporary> Temporaries => Set<Persistables.Temporary>();
    
    public StorageDbContext(DbContextOptions<StorageDbContext> options) 
        : base(options)
    {
    }
}