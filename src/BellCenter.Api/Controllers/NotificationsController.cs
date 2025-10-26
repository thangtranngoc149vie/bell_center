using System.Collections.Generic;
using BellCenter.Api.Models;
using BellCenter.Api.Options;
using BellCenter.Api.Services;
using BellCenter.Api.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BellCenter.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;
    private readonly IUserContext _userContext;
    private readonly IOptionsMonitor<SignalRNegotiationOptions> _negotiationOptions;

    public NotificationsController(
        INotificationService service,
        IUserContext userContext,
        IOptionsMonitor<SignalRNegotiationOptions> negotiationOptions)
    {
        _service = service;
        _userContext = userContext;
        _negotiationOptions = negotiationOptions;
    }

    [HttpGet]
    [ProducesResponseType(typeof(NotificationListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationListResponse>> ListAsync(
        [FromQuery] NotificationListRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _userContext.GetCurrentUserId();
        try
        {
            var response = await _service.ListAsync(userId, request, cancellationToken);
            return Ok(response);
        }
        catch (NotificationValidationException ex)
        {
            return ValidationProblemResult(ex);
        }
        catch (NotificationAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NotificationDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDetail>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _userContext.GetCurrentUserId();
        try
        {
            var detail = await _service.GetAsync(userId, id, cancellationToken);
            if (detail is null)
            {
                return NotFound();
            }

            return Ok(detail);
        }
        catch (NotificationAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkReadAsync(
        Guid id,
        [FromBody] MarkReadRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _userContext.GetCurrentUserId();
        try
        {
            var updated = await _service.MarkReadAsync(userId, id, request.IsRead, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (NotificationAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpPost("bulk-read")]
    [ProducesResponseType(typeof(BulkReadResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<BulkReadResult>> BulkReadAsync(
        [FromBody] BulkReadRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _userContext.GetCurrentUserId();
        try
        {
            var updated = await _service.BulkReadAsync(userId, request, cancellationToken);
            return Ok(new BulkReadResult { Updated = updated });
        }
        catch (NotificationValidationException ex)
        {
            return ValidationProblemResult(ex);
        }
        catch (NotificationAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpPatch("{id:guid}/hide")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> HideAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _userContext.GetCurrentUserId();
        try
        {
            var updated = await _service.HideAsync(userId, id, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (NotificationAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(NotificationStats), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationStats>> GetStatsAsync(CancellationToken cancellationToken)
    {
        var userId = _userContext.GetCurrentUserId();
        try
        {
            var stats = await _service.GetStatsAsync(userId, cancellationToken);
            return Ok(stats);
        }
        catch (NotificationAccessDeniedException)
        {
            return Forbid();
        }
    }

    [AllowAnonymous]
    [HttpGet("negotiate")]
    [ProducesResponseType(typeof(NegotiateResponse), StatusCodes.Status200OK)]
    public ActionResult<NegotiateResponse> Negotiate()
    {
        var options = _negotiationOptions.CurrentValue;
        var response = new NegotiateResponse
        {
            Url = options.Url,
            AccessToken = options.AccessToken,
            ExpiresIn = options.ExpiresIn
        };
        return Ok(response);
    }

    private ActionResult ValidationProblemResult(NotificationValidationException exception)
    {
        var errors = new Dictionary<string, string[]>(exception.Errors, StringComparer.OrdinalIgnoreCase);
        var details = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest
        };

        return BadRequest(details);
    }
}
