using BellCenter.Api.Models;

namespace BellCenter.Api.Infrastructure;

public interface INotificationRepository
{
    Task<NotificationListResponse> ListAsync(Guid userId, NotificationListQuery query, CancellationToken cancellationToken);
    Task<NotificationDetail?> GetAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(Guid userId, Guid notificationId, bool isRead, CancellationToken cancellationToken);
    Task<int> BulkReadAsync(Guid userId, BulkReadRequest request, CancellationToken cancellationToken);
    Task<bool> HideAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken);
    Task<NotificationStats> GetStatsAsync(Guid userId, CancellationToken cancellationToken);
}
