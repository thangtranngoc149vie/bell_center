using System.Collections.Generic;
using BellCenter.Api.Infrastructure;
using BellCenter.Api.Models;
using BellCenter.Api.Options;
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
    private static readonly HashSet<string> AllowedSeverities = new(StringComparer.OrdinalIgnoreCase)
    {
        "info", "warning", "critical"
    };

    private readonly INotificationRepository _repository;
    private readonly IUserContext _userContext;
    private readonly IOptionsMonitor<SignalRNegotiationOptions> _negotiationOptions;

    public NotificationsController(
        INotificationRepository repository,
        IUserContext userContext,
        IOptionsMonitor<SignalRNegotiationOptions> negotiationOptions)
    {
        _repository = repository;
        _userContext = userContext;
        _negotiationOptions = negotiationOptions;
    }

    [HttpGet]
    [ProducesResponseType(typeof(NotificationListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationListResponse>> ListAsync([FromQuery] NotificationListRequest request, CancellationToken cancellationToken)
    {
        if (!TryBuildQuery(request, out var query, out var errorResult))
        {
            return errorResult!;
        }

        var userId = _userContext.GetCurrentUserId();
        var response = await _repository.ListAsync(userId, query, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NotificationDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDetail>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _userContext.GetCurrentUserId();
        var detail = await _repository.GetAsync(userId, id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        return Ok(detail);
    }

    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkReadAsync(Guid id, [FromBody] MarkReadRequest request, CancellationToken cancellationToken)
    {
        var userId = _userContext.GetCurrentUserId();
        var updated = await _repository.MarkReadAsync(userId, id, request.IsRead, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("bulk-read")]
    [ProducesResponseType(typeof(BulkReadResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<BulkReadResult>> BulkReadAsync([FromBody] BulkReadRequest request, CancellationToken cancellationToken)
    {
        var userId = _userContext.GetCurrentUserId();
        var updated = await _repository.BulkReadAsync(userId, request, cancellationToken);
        return Ok(new BulkReadResult { Updated = updated });
    }

    [HttpPatch("{id:guid}/hide")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> HideAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _userContext.GetCurrentUserId();
        var updated = await _repository.HideAsync(userId, id, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(NotificationStats), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationStats>> GetStatsAsync(CancellationToken cancellationToken)
    {
        var userId = _userContext.GetCurrentUserId();
        var stats = await _repository.GetStatsAsync(userId, cancellationToken);
        return Ok(stats);
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

    private bool TryBuildQuery(NotificationListRequest request, out NotificationListQuery query, out ActionResult? errorResult)
    {
        query = new NotificationListQuery();
        errorResult = null;

        Guid? cursor = null;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!Guid.TryParse(request.Cursor, out var parsedCursor))
            {
                errorResult = ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Cursor)] = new[] { "Cursor must be a valid UUID." }
                });
                return false;
            }

            cursor = parsedCursor;
        }

        Guid? sourceId = null;
        if (!string.IsNullOrWhiteSpace(request.SourceEntityId))
        {
            if (!Guid.TryParse(request.SourceEntityId, out var parsedSourceId))
            {
                errorResult = ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.SourceEntityId)] = new[] { "Source entity id must be a valid UUID." }
                });
                return false;
            }

            sourceId = parsedSourceId;
        }

        var limit = request.Limit ?? 20;
        if (limit < 1 || limit > 100)
        {
            errorResult = ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Limit)] = new[] { "Limit must be between 1 and 100." }
            });
            return false;
        }

        string? severity = null;
        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            if (!AllowedSeverities.Contains(request.Severity))
            {
                errorResult = ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Severity)] = new[] { "Severity must be one of info, warning, or critical." }
                });
                return false;
            }

            severity = request.Severity.ToLowerInvariant();
        }

        var sort = NotificationSortOrder.CreatedAtDesc;
        if (!string.IsNullOrWhiteSpace(request.Sort))
        {
            switch (request.Sort.ToLowerInvariant())
            {
                case "created_at_desc":
                    sort = NotificationSortOrder.CreatedAtDesc;
                    break;
                case "created_at_asc":
                    sort = NotificationSortOrder.CreatedAtAsc;
                    break;
                default:
                    errorResult = ValidationProblem(new Dictionary<string, string[]>
                    {
                        [nameof(request.Sort)] = new[] { "Sort must be created_at_desc or created_at_asc." }
                    });
                    return false;
            }
        }

        query = new NotificationListQuery
        {
            Cursor = cursor,
            Limit = limit,
            UnreadOnly = request.UnreadOnly,
            Severity = severity,
            Category = request.Category,
            From = request.From,
            To = request.To,
            SourceEntityType = request.SourceEntityType,
            SourceEntityId = sourceId,
            Sort = sort
        };

        return true;
    }
}
