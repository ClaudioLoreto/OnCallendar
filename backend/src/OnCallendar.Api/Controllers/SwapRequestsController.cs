using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnCallendar.Api.Contracts;
using OnCallendar.Application.Common;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Enums;

namespace OnCallendar.Api.Controllers;

[ApiController]
[Route("api/swaps")]
[Authorize]
public sealed class SwapRequestsController : ControllerBase
{
    private readonly ISwapService _swaps;
    private readonly ICurrentUserService _user;

    public SwapRequestsController(ISwapService swaps, ICurrentUserService user)
    {
        _swaps = swaps;
        _user = user;
    }

    // ── DTO mapping (kept in controller – presentation concern) ──

    private static ShiftBriefDto Brief(Shift s) => new(
        s.Id, s.Date.ToString("yyyy-MM-dd"), s.Code.ToString(), s.StartUtc, s.EndUtc);

    private static SwapDto Map(SwapRequest r) => new(
        r.Id, r.Type, r.Status,
        r.InitiatorMedicoId, $"{r.InitiatorMedico.FirstName} {r.InitiatorMedico.LastName}",
        Brief(r.InitiatorShift),
        r.CounterpartMedicoId,
        r.CounterpartMedico is null ? null : $"{r.CounterpartMedico.FirstName} {r.CounterpartMedico.LastName}",
        r.CounterpartShift is null ? null : Brief(r.CounterpartShift),
        r.Message, r.ResolutionReason, r.CreatedAtUtc, r.ResolvedAtUtc,
        r.CounterOffers?.Count(o => o.Status == CounterOfferStatus.Pending) ?? 0);

    private static CounterOfferDto MapOffer(SwapCounterOffer o) => new(
        o.Id, o.SwapRequestId,
        o.ProposedByMedicoId, $"{o.ProposedByMedico.FirstName} {o.ProposedByMedico.LastName}",
        Brief(o.OfferedShift), o.Message, o.Status.ToString(), o.CreatedAtUtc, o.ResolvedAtUtc);

    // ── Result → ActionResult mapping ──

    private ActionResult<TDto> FromResult<T, TDto>(ServiceResult<T> result, Func<T, TDto> map) where T : class
    {
        if (result.IsSuccess) return Ok(map(result.Value!));
        return result.ErrorKind switch
        {
            ServiceErrorKind.NotFound => NotFound(new { error = result.ErrorMessage }),
            ServiceErrorKind.Forbidden => Forbid(),
            ServiceErrorKind.Conflict => Conflict(new { error = result.ErrorMessage }),
            ServiceErrorKind.ValidationError => BadRequest(new { error = result.ErrorMessage }),
            ServiceErrorKind.BlockedByRules => UnprocessableEntity(new { error = result.ErrorMessage, canForce = false, violations = result.Details }),
            ServiceErrorKind.NeedsConfirmation => UnprocessableEntity(new { error = result.ErrorMessage, canForce = true, violations = result.Details }),
            _ => StatusCode(500),
        };
    }

    private ActionResult<IEnumerable<TDto>> FromListResult<T, TDto>(ServiceResult<IReadOnlyList<T>> result, Func<T, TDto> map) where T : class
    {
        if (result.IsSuccess) return Ok(result.Value!.Select(map));
        return result.ErrorKind switch
        {
            ServiceErrorKind.NotFound => NotFound(new { error = result.ErrorMessage }),
            ServiceErrorKind.Forbidden => Forbid(),
            ServiceErrorKind.Conflict => Conflict(new { error = result.ErrorMessage }),
            ServiceErrorKind.ValidationError => BadRequest(new { error = result.ErrorMessage }),
            _ => StatusCode(500),
        };
    }

    private IActionResult FromVoidResult(ServiceResult result)
    {
        if (result.IsSuccess) return NoContent();
        return result.ErrorKind switch
        {
            ServiceErrorKind.NotFound => NotFound(new { error = result.ErrorMessage }),
            ServiceErrorKind.Forbidden => Forbid(),
            ServiceErrorKind.ValidationError => BadRequest(new { error = result.ErrorMessage }),
            _ => StatusCode(500),
        };
    }

    // ── Endpoints ──

    [HttpGet("incoming")]
    public async Task<ActionResult<IEnumerable<SwapDto>>> Incoming()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var list = await _swaps.GetIncomingAsync(uid);
        return Ok(list.Select(Map));
    }

    [HttpGet("outgoing")]
    public async Task<ActionResult<IEnumerable<SwapDto>>> Outgoing()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var list = await _swaps.GetOutgoingAsync(uid);
        return Ok(list.Select(Map));
    }

    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<SwapDto>>> History()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var list = await _swaps.GetHistoryAsync(uid);
        return Ok(list.Select(Map));
    }

    [HttpPost("giveaway")]
    public async Task<ActionResult<SwapDto>> CreateGiveaway([FromBody] CreateGiveawayRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        return FromResult(await _swaps.CreateGiveawayAsync(uid, req.ShiftId, req.ToMedicoId, req.Message), Map);
    }

    [HttpPost("giveaway-multi")]
    public async Task<ActionResult<IEnumerable<SwapDto>>> CreateMultiGiveaway([FromBody] CreateMultiGiveawayRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        return FromListResult(await _swaps.CreateMultiGiveawayAsync(uid, req.ShiftId, req.RecipientIds, req.Message), Map);
    }

    [HttpPost("swap")]
    public async Task<ActionResult<SwapDto>> CreateSwap([FromBody] CreateSwapRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        return FromResult(await _swaps.CreateSwapAsync(uid, req.MyShiftId, req.OtherShiftId, req.Message), Map);
    }

    [HttpPost("swap-multi")]
    public async Task<ActionResult<IEnumerable<SwapDto>>> CreateMultiSwap([FromBody] CreateMultiSwapRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        return FromListResult(await _swaps.CreateMultiSwapAsync(uid, req.MyShiftId, req.CandidateShiftIds, req.Message), Map);
    }

    [HttpPost("pick/{shiftId:guid}")]
    public async Task<ActionResult<SwapDto>> PickFromBoard(Guid shiftId)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        return FromResult(await _swaps.PickFromBoardAsync(uid, shiftId), Map);
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<SwapDto>> Accept(Guid id, [FromQuery] bool force = false)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        return FromResult(await _swaps.AcceptAsync(uid, id, force), Map);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<SwapDto>> Reject(Guid id, [FromBody] RejectBody? body)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        return FromResult(await _swaps.RejectAsync(uid, id, body?.Reason), Map);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<SwapDto>> Cancel(Guid id)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        return FromResult(await _swaps.CancelAsync(uid, id), Map);
    }

    [HttpGet("{id:guid}/counter-offers")]
    public async Task<ActionResult<IEnumerable<CounterOfferDto>>> ListCounterOffers(Guid id)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        return FromListResult(await _swaps.GetCounterOffersAsync(uid, id), MapOffer);
    }

    [HttpPost("{id:guid}/counter")]
    public async Task<ActionResult<CounterOfferDto>> ProposeCounter(Guid id, [FromBody] CreateCounterOfferRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        return FromResult(await _swaps.ProposeCounterAsync(uid, id, req.OfferedShiftId, req.Message), MapOffer);
    }

    [HttpPost("{swapId:guid}/counter/{offerId:guid}/accept")]
    public async Task<ActionResult<SwapDto>> AcceptCounter(Guid swapId, Guid offerId, [FromQuery] bool force = false)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        return FromResult(await _swaps.AcceptCounterAsync(uid, swapId, offerId, force), Map);
    }

    [HttpPost("{swapId:guid}/counter/{offerId:guid}/reject")]
    public async Task<IActionResult> RejectCounter(Guid swapId, Guid offerId)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        return FromVoidResult(await _swaps.RejectCounterAsync(uid, swapId, offerId));
    }
}
