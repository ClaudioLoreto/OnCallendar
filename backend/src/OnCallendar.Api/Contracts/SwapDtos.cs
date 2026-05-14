using OnCallendar.Domain.Enums;

namespace OnCallendar.Api.Contracts;

// ── Swap Requests ──
public sealed record ShiftBriefDto(
    Guid Id, string Date, string Code, DateTime StartUtc, DateTime EndUtc);

public sealed record SwapDto(
    Guid Id, SwapRequestType Type, SwapRequestStatus Status,
    Guid InitiatorId, string InitiatorName, ShiftBriefDto InitiatorShift,
    Guid? CounterpartId, string? CounterpartName, ShiftBriefDto? CounterpartShift,
    string? Message, string? ResolutionReason,
    DateTime CreatedAtUtc, DateTime? ResolvedAtUtc,
    int PendingCounterOffersCount,
    bool IsReperibile = false);

public sealed record CreateGiveawayRequest(Guid ShiftId, Guid ToMedicoId, string? Message = null, bool IsReperibile = false);
public sealed record CreateSwapRequest(Guid MyShiftId, Guid OtherShiftId, string? Message = null, bool IsReperibile = false);
public sealed record CreateMultiGiveawayRequest(Guid ShiftId, List<Guid> RecipientIds, string? Message = null, bool IsReperibile = false);
public sealed record CreateMultiSwapRequest(Guid MyShiftId, List<Guid> CandidateShiftIds, string? Message = null, bool IsReperibile = false);
public sealed record RejectBody(string? Reason = null);
public sealed record CreateCounterOfferRequest(Guid OfferedShiftId, string? Message = null);

public sealed record CounterOfferDto(
    Guid Id, Guid SwapRequestId,
    Guid ProposedById, string ProposedByName,
    ShiftBriefDto OfferedShift,
    string? Message, string Status,
    DateTime CreatedAtUtc, DateTime? ResolvedAtUtc);
