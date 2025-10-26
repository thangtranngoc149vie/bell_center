using System.Collections.ObjectModel;

namespace BellCenter.Api.Services;

public sealed class NotificationValidationException : Exception
{
    public NotificationValidationException(string field, string message)
        : base(message)
    {
        Errors = new ReadOnlyDictionary<string, string[]>(
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [field] = new[] { message }
            });
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

public sealed class NotificationAccessDeniedException : Exception
{
    public NotificationAccessDeniedException(Guid userId)
        : base($"User '{userId}' is not permitted to access notifications.")
    {
        UserId = userId;
    }

    public Guid UserId { get; }
}
