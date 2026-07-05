using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Services;

/// <summary>
/// Closes the database and background workers before restore or application exit.
/// </summary>
public sealed class DatabaseShutdownService : IDatabaseShutdownService
{
    private readonly LiteDbContext _context;
    private readonly IReceiptImportQueue _queue;
    private bool _closed;

    public DatabaseShutdownService(LiteDbContext context, IReceiptImportQueue queue)
    {
        _context = context;
        _queue = queue;
    }

    public void Close()
    {
        if (_closed)
            return;

        _closed = true;

        if (_queue is IDisposable disposableQueue)
            disposableQueue.Dispose();

        _context.Dispose();
    }
}
