namespace Ballware.Storage.Data.Persistables;

public interface IEntity
{
    long? Id { get; set; }
    Guid Uuid { get; set; }
}

