using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ISessionService _sessionService;

    public AuditService(IAuditLogRepository auditLogRepository, ISessionService sessionService)
    {
        _auditLogRepository = auditLogRepository;
        _sessionService = sessionService;
    }

    public Task LogAsync(AuditAction action, ObjectId? targetUserId, string targetUsername, string details = "")
    {
        var actor = _sessionService.CurrentUser;
        _auditLogRepository.Insert(new AuditLogEntry
        {
            ActorUserId = actor?.Id,
            ActorUsername = actor?.Username ?? "system",
            Action = action,
            TargetUserId = targetUserId,
            TargetUsername = targetUsername,
            Details = details
        });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int limit = 100) =>
        Task.FromResult<IReadOnlyList<AuditLogEntry>>(_auditLogRepository.GetRecent(limit).ToList());
}
