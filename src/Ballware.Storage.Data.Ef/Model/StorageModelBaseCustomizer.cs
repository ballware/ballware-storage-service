using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Ballware.Storage.Data.Ef.Model;

public class StorageModelBaseCustomizer : RelationalModelCustomizer
{
    public StorageModelBaseCustomizer(ModelCustomizerDependencies dependencies) 
        : base(dependencies)
    {
    }
    
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        modelBuilder.Entity<Persistables.Attachment>().HasKey(d => d.Id);
        modelBuilder.Entity<Persistables.Attachment>().HasIndex(d => new { d.TenantId, d.Uuid }).IsUnique();
        modelBuilder.Entity<Persistables.Attachment>().HasIndex(d => d.TenantId);
        modelBuilder.Entity<Persistables.Attachment>().HasIndex(d => new { d.TenantId, d.Entity, d.OwnerId, d.FileName }).IsUnique();
        
        modelBuilder.Entity<Persistables.Temporary>().HasKey(d => d.Id);
        modelBuilder.Entity<Persistables.Temporary>().HasIndex(d => new { d.TenantId, d.Uuid }).IsUnique();
        modelBuilder.Entity<Persistables.Temporary>().HasIndex(d => d.TenantId);
    }
}