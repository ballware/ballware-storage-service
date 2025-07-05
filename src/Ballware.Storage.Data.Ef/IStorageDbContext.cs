using Ballware.Shared.Data.Ef;
using Ballware.Storage.Data.Persistables;
using Microsoft.EntityFrameworkCore;

namespace Ballware.Storage.Data.Ef;

public interface IStorageDbContext : IDbContext
{
    DbSet<Attachment> Attachments { get; }
    DbSet<Temporary> Temporaries { get; }
}