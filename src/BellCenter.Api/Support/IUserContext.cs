namespace BellCenter.Api.Support;

public interface IUserContext
{
    Guid GetCurrentUserId();
    Guid? TryGetCurrentUserId();
}
