using BellCenter.Api.Infrastructure;
using BellCenter.Api.Models;

namespace BellCenter.Api.Services;

public sealed class NotificationService(
    INotificationRepository notificationRepository,
    IUserAccessRepository userAccessRepository)
    : INotificationService
{
    private static readonly HashSet<string> AllowedSeverities = new(StringComparer.OrdinalIgnoreCase)
    {
        "info", "warning", "critical"
    };

    private readonly INotificationRepository _notificationRepository = notificationRepository;
    private readonly IUserAccessRepository _userAccessRepository = userAccessRepository;

    public async Task<NotificationListResponse> ListAsync(
        Guid userId,
        NotificationListRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureUserAccessAsync(userId, cancellationToken);
        var query = BuildListQuery(request);
        return await _notificationRepository.ListAsync(userId, query, cancellationToken);
    }

    public async Task<NotificationDetail?> GetAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        await EnsureUserAccessAsync(userId, cancellationToken);
        return await _notificationRepository.GetAsync(userId, notificationId, cancellationToken);
    }

    public async Task<bool> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        bool isRead,
        CancellationToken cancellationToken)
    {
        await EnsureUserAccessAsync(userId, cancellationToken);
        return await _notificationRepository.MarkReadAsync(userId, notificationId, isRead, cancellationToken);
    }

    public async Task<int> BulkReadAsync(Guid userId, BulkReadRequest request, CancellationToken cancellationToken)
    {
        await EnsureUserAccessAsync(userId, cancellationToken);

        if (!request.AllUnread && (request.Ids is null || request.Ids.Count == 0))
        {
            throw new NotificationValidationException(nameof(request.Ids), "Provide at least one notification id or set all_unread to true.");
        }

        BulkReadFilters? filters = request.Filters;
        if (filters is { Severity: { Length: > 0 } severity } && !AllowedSeverities.Contains(severity))
        {
            throw new NotificationValidationException("filters.severity", "Severity must be one of info, warning, or critical.");
        }

        if (filters is { Severity: { Length: > 0 } severityValue })
        {
            filters = filters with { Severity = severityValue.ToLowerInvariant() };
        }

        var sanitizedRequest = filters == request.Filters ? request : request with { Filters = filters };

        return await _notificationRepository.BulkReadAsync(userId, sanitizedRequest, cancellationToken);
    }

    public async Task<bool> HideAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        await EnsureUserAccessAsync(userId, cancellationToken);
        return await _notificationRepository.HideAsync(userId, notificationId, cancellationToken);
    }

    public async Task<NotificationStats> GetStatsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await EnsureUserAccessAsync(userId, cancellationToken);
        return await _notificationRepository.GetStatsAsync(userId, cancellationToken);
    }

    private async Task EnsureUserAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var hasAccess = await _userAccessRepository.UserHasNotificationAccessAsync(userId, cancellationToken);
        if (!hasAccess)
        {
            throw new NotificationAccessDeniedException(userId);
        }
    }

    private static NotificationListQuery BuildListQuery(NotificationListRequest request)
    {
        Guid? cursor = null;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!Guid.TryParse(request.Cursor, out var parsedCursor))
            {
                throw new NotificationValidationException(nameof(request.Cursor), "Cursor must be a valid UUID.");
            }

            cursor = parsedCursor;
        }

        Guid? sourceId = null;
        if (!string.IsNullOrWhiteSpace(request.SourceEntityId))
        {
            if (!Guid.TryParse(request.SourceEntityId, out var parsedSourceId))
            {
                throw new NotificationValidationException(nameof(request.SourceEntityId), "Source entity id must be a valid UUID.");
            }

            sourceId = parsedSourceId;
        }

        var limit = request.Limit ?? 20;
        if (limit < 1 || limit > 100)
        {
            throw new NotificationValidationException(nameof(request.Limit), "Limit must be between 1 and 100.");
        }

        string? severity = null;
        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            if (!AllowedSeverities.Contains(request.Severity))
            {
                throw new NotificationValidationException(nameof(request.Severity), "Severity must be one of info, warning, or critical.");
            }

            severity = request.Severity.ToLowerInvariant();
        }

        var sort = NotificationSortOrder.CreatedAtDesc;
        if (!string.IsNullOrWhiteSpace(request.Sort))
        {
            sort = request.Sort.ToLowerInvariant() switch
            {
                "created_at_desc" => NotificationSortOrder.CreatedAtDesc,
                "created_at_asc" => NotificationSortOrder.CreatedAtAsc,
                _ => throw new NotificationValidationException(nameof(request.Sort), "Sort must be created_at_desc or created_at_asc.")
            };
        }

        return new NotificationListQuery
        {
            Cursor = cursor,
            Limit = limit,
            UnreadOnly = request.UnreadOnly,
            Severity = severity,
            Category = request.Category,
            From = request.From,
            To = request.To,
            SourceEntityType = request.SourceEntityType,
            SourceEntityId = sourceId,
            Sort = sort
        };
    }
}
