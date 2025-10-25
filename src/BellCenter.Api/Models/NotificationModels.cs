using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BellCenter.Api.Models;

public sealed record NotificationSource(string? Type, Guid? Id);

public sealed record NotificationListItem
{
    [JsonIgnore]
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
    public NotificationSource? Source { get; init; }
    public JsonNode? Payload { get; init; }
}

public sealed record NotificationDetail
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Message { get; init; }
    public string? Category { get; init; }
    public string? Type { get; init; }
    public string Severity { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public bool IsRead { get; init; }
    public string? OpenUrl { get; init; }
    public NotificationSource? Source { get; init; }
    public JsonNode? Payload { get; init; }
}

public sealed record NotificationListResponse
{
    public IReadOnlyList<NotificationListItem> Items { get; init; } = Array.Empty<NotificationListItem>();
    public Guid? NextCursor { get; init; }
    public NotificationStats Stats { get; init; } = new();
}

public sealed record NotificationStats
{
    public int UnreadTotal { get; init; }
    public IDictionary<string, int> ByCategory { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public IDictionary<string, int> BySeverity { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}

public sealed record MarkReadRequest
{
    public bool IsRead { get; init; }
}

public sealed record BulkReadFilters
{
    public string? Category { get; init; }
    public string? Severity { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

public sealed record BulkReadRequest
{
    public IReadOnlyList<Guid>? Ids { get; init; }
    public bool AllUnread { get; init; }
    public BulkReadFilters? Filters { get; init; }
}

public sealed record BulkReadResult
{
    public int Updated { get; init; }
}

public sealed record NegotiateResponse
{
    public string Url { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
}
