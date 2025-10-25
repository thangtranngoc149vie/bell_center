namespace BellCenter.Api.Models;

public enum NotificationSortOrder
{
    CreatedAtDesc,
    CreatedAtAsc
}

public sealed class NotificationListQuery
{
    public Guid? Cursor { get; init; }
    public int Limit { get; init; } = 20;
    public bool? UnreadOnly { get; init; }
    public string? Severity { get; init; }
    public string? Category { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? SourceEntityType { get; init; }
    public Guid? SourceEntityId { get; init; }
    public NotificationSortOrder Sort { get; init; } = NotificationSortOrder.CreatedAtDesc;
}
