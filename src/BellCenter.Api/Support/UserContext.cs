using System.Linq;
using System.Security.Claims;

namespace BellCenter.Api.Support;

public sealed class UserContext(IHttpContextAccessor accessor) : IUserContext
{
    private readonly IHttpContextAccessor _accessor = accessor;

    public Guid GetCurrentUserId()
    {
        return TryGetCurrentUserId() ?? throw new InvalidOperationException(
            "Unable to resolve the current user id. Ensure authentication is configured or provide X-User-Id header.");
    }

    public Guid? TryGetCurrentUserId()
    {
        var principal = _accessor.HttpContext?.User;
        if (principal is not null)
        {
            var nameIdentifier = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub");
            if (Guid.TryParse(nameIdentifier, out var claimId))
            {
                return claimId;
            }
        }

        var headers = _accessor.HttpContext?.Request?.Headers;
        if (headers is not null && headers.TryGetValue("X-User-Id", out var headerValue))
        {
            var headerId = headerValue.FirstOrDefault();
            if (Guid.TryParse(headerId, out var guid))
            {
                return guid;
            }
        }

        return null;
    }
}
