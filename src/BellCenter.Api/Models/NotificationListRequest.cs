namespace BellCenter.Api.Models;

public sealed class NotificationListRequest
{
    public string? Cursor { get; init; }
    public int? Limit { get; init; }
    public bool? UnreadOnly { get; init; }
    public string? Severity { get; init; }
    public string? Category { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? SourceEntityType { get; init; }
    public string? SourceEntityId { get; init; }
    public string? Sort { get; init; }
}
