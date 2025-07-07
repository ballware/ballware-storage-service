using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ballware.Storage.Data.Ef.Model;

public class StorageModelBaseCustomizer : RelationalModelCustomizer
{
    public StorageModelBaseCustomizer(ModelCustomizerDependencies dependencies) 
        : base(dependencies)
    {
    }
    
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(new ValueConverter<DateTime, DateTime>(
                        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                        v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
                }

                if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(new ValueConverter<DateTime?, DateTime?>(
                        v => v.HasValue ? v.Value.ToUniversalTime() : v,
                        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v));
                }
            }
        }
        
        modelBuilder.Entity<Persistables.Attachment>().HasKey(d => d.Id);
        modelBuilder.Entity<Persistables.Attachment>().HasIndex(d => new { d.TenantId, d.Uuid }).IsUnique();
        modelBuilder.Entity<Persistables.Attachment>().HasIndex(d => d.TenantId);
        modelBuilder.Entity<Persistables.Attachment>().HasIndex(d => new { d.TenantId, d.Entity, d.OwnerId, d.FileName }).IsUnique();
        
        modelBuilder.Entity<Persistables.Temporary>().HasKey(d => d.Id);
        modelBuilder.Entity<Persistables.Temporary>().HasIndex(d => new { d.TenantId, d.Uuid }).IsUnique();
        modelBuilder.Entity<Persistables.Temporary>().HasIndex(d => d.TenantId);
    }
}