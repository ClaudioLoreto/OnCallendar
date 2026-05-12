using OnCallendar.Domain.Entities;

namespace OnCallendar.Application.Common.Interfaces;

/// <summary>
/// Business logic for swap requests, giveaways, board picks, and counter-offers.
/// </summary>
public interface ISwapService
{
    // ── Queries ──
    Task<IReadOnlyList<SwapRequest>> GetIncomingAsync(Guid userId);
    Task<IReadOnlyList<SwapRequest>> GetOutgoingAsync(Guid userId);
    Task<IReadOnlyList<SwapRequest>> GetHistoryAsync(Guid userId);
    Task<ServiceResult<IReadOnlyList<SwapCounterOffer>>> GetCounterOffersAsync(Guid userId, Guid swapId);

    // ── Commands ──
    Task<ServiceResult<SwapRequest>> CreateGiveawayAsync(Guid userId, Guid shiftId, Guid toMedicoId, string? message);
    Task<ServiceResult<IReadOnlyList<SwapRequest>>> CreateMultiGiveawayAsync(Guid userId, Guid shiftId, List<Guid> recipientIds, string? message);
    Task<ServiceResult<SwapRequest>> CreateSwapAsync(Guid userId, Guid myShiftId, Guid otherShiftId, string? message);
    Task<ServiceResult<IReadOnlyList<SwapRequest>>> CreateMultiSwapAsync(Guid userId, Guid myShiftId, List<Guid> candidateShiftIds, string? message);
    Task<ServiceResult<SwapRequest>> PickFromBoardAsync(Guid userId, Guid shiftId);
    Task<ServiceResult<SwapRequest>> AcceptAsync(Guid userId, Guid swapId, bool force);
    Task<ServiceResult<SwapRequest>> RejectAsync(Guid userId, Guid swapId, string? reason);
    Task<ServiceResult<SwapRequest>> CancelAsync(Guid userId, Guid swapId);
    Task<ServiceResult<SwapCounterOffer>> ProposeCounterAsync(Guid userId, Guid swapId, Guid offeredShiftId, string? message);
    Task<ServiceResult<SwapRequest>> AcceptCounterAsync(Guid userId, Guid swapId, Guid offerId, bool force);
    Task<ServiceResult> RejectCounterAsync(Guid userId, Guid swapId, Guid offerId);
}
