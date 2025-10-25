using System.Data;
using System.Linq;
using System.Text.Json.Nodes;
using BellCenter.Api.Models;
using Dapper;

namespace BellCenter.Api.Infrastructure;

public sealed class NotificationRepository(IDbConnection connection) : INotificationRepository
{
    private readonly IDbConnection _connection = connection;

    public async Task<NotificationListResponse> ListAsync(Guid userId, NotificationListQuery query, CancellationToken cancellationToken)
    {
        var normalizedLimit = Math.Clamp(query.Limit, 1, 100);
        var sql = GetListSql();
        var parameters = new
        {
            uid = userId,
            limit = normalizedLimit,
            cursor_id = query.Cursor,
            unread_only = query.UnreadOnly,
            severity = query.Severity,
            category = query.Category,
            from = query.From,
            to = query.To,
            source_entity_type = query.SourceEntityType,
            source_entity_id = query.SourceEntityId,
            sort = query.Sort == NotificationSortOrder.CreatedAtAsc ? "created_at_asc" : "created_at_desc"
        };

        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        var rows = await _connection.QueryAsync<NotificationDbRow>(command);
        var items = rows.Select(MapListItem).ToList();
        Guid? nextCursor = items.Count > 0 ? items[^1].UserNotificationId : null;
        var stats = await GetStatsAsync(userId, cancellationToken);

        return new NotificationListResponse
        {
            Items = items,
            NextCursor = nextCursor,
            Stats = stats
        };
    }

    public async Task<NotificationDetail?> GetAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT un.id AS user_notification_id,
                                       n.id,
                                       n.title,
                                       n.message,
                                       n.category,
                                       n.type,
                                       n.severity,
                                       n.created_at,
                                       un.is_read,
                                       n.open_url,
                                       n.source_entity_type,
                                       n.source_entity_id,
                                       n.payload::text AS payload
                                FROM user_notifications un
                                JOIN notifications n ON n.id = un.notification_id
                                WHERE un.user_id = @uid AND n.id = @id AND un.is_hidden = FALSE";

        var command = new CommandDefinition(sql, new { uid = userId, id = notificationId }, cancellationToken: cancellationToken);
        var row = await _connection.QuerySingleOrDefaultAsync<NotificationDbRow>(command);
        if (row is null)
        {
            return null;
        }

        return new NotificationDetail
        {
            Id = row.Id,
            Title = row.Title,
            Message = row.Message,
            Category = row.Category,
            Type = row.Type,
            Severity = row.Severity,
            CreatedAt = row.CreatedAt,
            IsRead = row.IsRead,
            OpenUrl = row.OpenUrl,
            Source = CreateSource(row),
            Payload = ParsePayload(row.Payload)
        };
    }

    public async Task<bool> MarkReadAsync(Guid userId, Guid notificationId, bool isRead, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE user_notifications
                             SET is_read = @is_read,
                                 read_at = CASE WHEN @is_read THEN COALESCE(read_at, now()) ELSE NULL END
                             WHERE user_id = @uid AND notification_id = @nid";

        var command = new CommandDefinition(sql, new { uid = userId, nid = notificationId, is_read = isRead }, cancellationToken: cancellationToken);
        var affected = await _connection.ExecuteAsync(command);
        return affected > 0;
    }

    public async Task<int> BulkReadAsync(Guid userId, BulkReadRequest request, CancellationToken cancellationToken)
    {
        if (request.AllUnread)
        {
            var filters = request.Filters;
            const string sql = @"UPDATE user_notifications un
                                 SET is_read = TRUE,
                                     read_at = now()
                                 FROM notifications n
                                 WHERE un.user_id = @uid
                                   AND un.notification_id = n.id
                                   AND un.is_hidden = FALSE
                                   AND un.is_read = FALSE
                                   AND (@category IS NULL OR n.category = @category)
                                   AND (@severity IS NULL OR n.severity = @severity)
                                   AND (@from IS NULL OR n.created_at >= @from)
                                   AND (@to IS NULL OR n.created_at <= @to)";

            var command = new CommandDefinition(sql, new
            {
                uid = userId,
                category = filters?.Category,
                severity = filters?.Severity,
                from = filters?.From,
                to = filters?.To
            }, cancellationToken: cancellationToken);
            return await _connection.ExecuteAsync(command);
        }

        var ids = request.Ids?.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids is null || ids.Length == 0)
        {
            return 0;
        }

        const string idsSql = @"UPDATE user_notifications
                                SET is_read = TRUE,
                                    read_at = now()
                                WHERE user_id = @uid
                                  AND notification_id = ANY(@ids)
                                  AND is_read = FALSE";

        var commandWithIds = new CommandDefinition(idsSql, new
        {
            uid = userId,
            ids
        }, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(commandWithIds);
    }

    public async Task<bool> HideAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE user_notifications
                             SET is_hidden = TRUE
                             WHERE user_id = @uid AND notification_id = @nid AND is_hidden = FALSE";

        var command = new CommandDefinition(sql, new { uid = userId, nid = notificationId }, cancellationToken: cancellationToken);
        var affected = await _connection.ExecuteAsync(command);
        return affected > 0;
    }

    public async Task<NotificationStats> GetStatsAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT COUNT(*)
                               FROM user_notifications un
                               JOIN notifications n ON n.id = un.notification_id
                               WHERE un.user_id = @uid AND un.is_hidden = FALSE AND un.is_read = FALSE";

        const string byCategorySql = @"SELECT n.category AS key,
                                              COUNT(*) AS value
                                       FROM user_notifications un
                                       JOIN notifications n ON n.id = un.notification_id
                                       WHERE un.user_id = @uid AND un.is_hidden = FALSE AND un.is_read = FALSE
                                       GROUP BY n.category";

        const string bySeveritySql = @"SELECT n.severity AS key,
                                              COUNT(*) AS value
                                       FROM user_notifications un
                                       JOIN notifications n ON n.id = un.notification_id
                                       WHERE un.user_id = @uid AND un.is_hidden = FALSE AND un.is_read = FALSE
                                       GROUP BY n.severity";

        var unreadTotal = await _connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { uid = userId }, cancellationToken: cancellationToken));

        var categoryRows = await _connection.QueryAsync<AggregateRow>(
            new CommandDefinition(byCategorySql, new { uid = userId }, cancellationToken: cancellationToken));
        var severityRows = await _connection.QueryAsync<AggregateRow>(
            new CommandDefinition(bySeveritySql, new { uid = userId }, cancellationToken: cancellationToken));

        var byCategory = categoryRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Key))
            .ToDictionary(row => row.Key!, row => row.Value, StringComparer.OrdinalIgnoreCase);
        var bySeverity = severityRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Key))
            .ToDictionary(row => row.Key!, row => row.Value, StringComparer.OrdinalIgnoreCase);

        return new NotificationStats
        {
            UnreadTotal = unreadTotal,
            ByCategory = byCategory,
            BySeverity = bySeverity
        };
    }

    private static NotificationListItem MapListItem(NotificationDbRow row)
    {
        return new NotificationListItem
        {
            UserNotificationId = row.UserNotificationId,
            Id = row.Id,
            Title = row.Title,
            Message = row.Message,
            Category = row.Category,
            Type = row.Type,
            Severity = row.Severity,
            CreatedAt = row.CreatedAt,
            IsRead = row.IsRead,
            OpenUrl = row.OpenUrl,
            Source = CreateSource(row),
            Payload = ParsePayload(row.Payload)
        };
    }

    private static NotificationSource? CreateSource(NotificationDbRow row)
    {
        if (string.IsNullOrWhiteSpace(row.SourceEntityType) && row.SourceEntityId is null)
        {
            return null;
        }

        return new NotificationSource(row.SourceEntityType, row.SourceEntityId);
    }

    private static JsonNode? ParsePayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(payload);
        }
        catch
        {
            return null;
        }
    }

    private static string GetListSql() => @"WITH cur AS (
    SELECT created_at AS cut_at, id
    FROM user_notifications
    WHERE id = @cursor_id
)
SELECT
    un.id AS user_notification_id,
    n.id,
    n.title,
    n.message,
    n.category,
    n.type,
    n.severity,
    n.created_at,
    un.is_read,
    n.open_url,
    n.source_entity_type,
    n.source_entity_id,
    n.payload::text AS payload
FROM user_notifications un
JOIN notifications n ON n.id = un.notification_id
LEFT JOIN cur ON TRUE
WHERE un.user_id = @uid
  AND un.is_hidden = FALSE
  AND (@unread_only IS NULL OR (@unread_only = TRUE AND un.is_read = FALSE))
  AND (@severity IS NULL OR n.severity = @severity)
  AND (@category IS NULL OR n.category = @category)
  AND (@from IS NULL OR n.created_at >= @from)
  AND (@to IS NULL OR n.created_at <= @to)
  AND (@source_entity_type IS NULL OR n.source_entity_type = @source_entity_type)
  AND (@source_entity_id IS NULL OR n.source_entity_id = @source_entity_id)
  AND (
        @cursor_id IS NULL
        OR (@sort = 'created_at_desc' AND (cur.cut_at IS NULL OR n.created_at < cur.cut_at OR (n.created_at = cur.cut_at AND un.id <> cur.id)))
        OR (@sort = 'created_at_asc' AND (cur.cut_at IS NULL OR n.created_at > cur.cut_at OR (n.created_at = cur.cut_at AND un.id <> cur.id)))
  )
ORDER BY
    CASE WHEN @sort = 'created_at_asc' THEN n.created_at END ASC,
    CASE WHEN @sort = 'created_at_desc' THEN n.created_at END DESC,
    CASE WHEN @sort = 'created_at_asc' THEN un.id END ASC,
    CASE WHEN @sort = 'created_at_desc' THEN un.id END DESC
LIMIT @limit;";

    private sealed record NotificationDbRow
    {
        public Guid UserNotificationId { get; init; }
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Message { get; init; }
        public string? Category { get; init; }
        public string? Type { get; init; }
        public string Severity { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
        public bool IsRead { get; init; }
        public string? OpenUrl { get; init; }
        public string? SourceEntityType { get; init; }
        public Guid? SourceEntityId { get; init; }
        public string? Payload { get; init; }
    }

    private sealed record AggregateRow
    {
        public string? Key { get; init; }
        public int Value { get; init; }
    }
}
