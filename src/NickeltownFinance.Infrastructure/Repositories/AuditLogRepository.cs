using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly LiteDbContext _context;

    public AuditLogRepository(LiteDbContext context) => _context = context;

    public void Insert(AuditLogEntry entry)
    {
        if (entry.Id == LiteDB.ObjectId.Empty)
            entry.Id = LiteDB.ObjectId.NewObjectId();
        entry.TimestampUtc = DateTime.UtcNow;
        _context.AuditLogs.Insert(entry);
    }

    public IEnumerable<AuditLogEntry> GetRecent(int limit = 100) =>
        _context.AuditLogs.FindAll()
            .OrderByDescending(x => x.TimestampUtc)
            .Take(limit)
            .ToList();
}
