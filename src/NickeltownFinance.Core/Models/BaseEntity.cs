using LiteDB;

namespace NickeltownFinance.Core.Models;

public abstract class BaseEntity
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
}
