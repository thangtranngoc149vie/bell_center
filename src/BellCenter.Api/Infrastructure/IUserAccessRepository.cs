namespace BellCenter.Api.Infrastructure;

public interface IUserAccessRepository
{
    Task<bool> UserHasNotificationAccessAsync(Guid userId, CancellationToken cancellationToken);
}
