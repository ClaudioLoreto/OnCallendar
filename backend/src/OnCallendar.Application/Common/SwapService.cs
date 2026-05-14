using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Application.Common.Services;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Enums;
using OnCallendar.Domain.Notifications;
using OnCallendar.Domain.Services;

namespace OnCallendar.Application.Common;

public sealed class SwapService : ISwapService
{
    private readonly IApplicationDbContext _db;
    private readonly IShiftValidationService _rules;
    private readonly IAuditLogger _audit;
    private readonly INotificationDispatcher _dispatcher;

    public SwapService(
        IApplicationDbContext db,
        IShiftValidationService rules,
        IAuditLogger audit,
        INotificationDispatcher dispatcher)
    {
        _db = db;
        _rules = rules;
        _audit = audit;
        _dispatcher = dispatcher;
    }

    // ── Shared helpers ──

    private IQueryable<SwapRequest> LoadSwaps() => _db.SwapRequests
        .Include(r => r.InitiatorMedico)
        .Include(r => r.CounterpartMedico)
        .Include(r => r.InitiatorShift)
        .Include(r => r.CounterpartShift)
        .Include(r => r.CounterOffers);

    private async Task MarkSwapNotificationsReadAsync(params Guid[] swapIds)
    {
        if (swapIds.Length == 0) return;
        var pending = await _db.Notifications
            .Where(n => n.RelatedEntityId.HasValue
                        && swapIds.Contains(n.RelatedEntityId.Value)
                        && !n.IsRead)
            .ToListAsync();
        if (pending.Count == 0) return;
        foreach (var n in pending) n.IsRead = true;
        await _db.SaveChangesAsync();
    }

    private static string FullName(ApplicationUser? u) =>
        u is null ? "qualcuno" : $"{u.FirstName} {u.LastName}".Trim();

    private static NotificationRequest BuildSwap(
        Guid tenantId, Guid recipientUserId, string type,
        SwapRequest swap, ApplicationUser? recipient, ApplicationUser? actor,
        IDictionary<string, string?>? extra = null)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["RecipientFirstName"] = recipient?.FirstName,
            ["ActorName"] = FullName(actor),
            ["InitiatorName"] = FullName(swap.InitiatorMedico ?? actor),
            ["CounterpartName"] = FullName(swap.CounterpartMedico),
            ["ShiftCode"] = swap.InitiatorShift?.Code.ToString(),
            ["ShiftDate"] = swap.InitiatorShift?.Date.ToString("dd/MM/yyyy"),
            ["CounterpartShiftCode"] = swap.CounterpartShift?.Code.ToString(),
            ["CounterpartShiftDate"] = swap.CounterpartShift?.Date.ToString("dd/MM/yyyy"),
            ["Message"] = swap.Message,
            ["RequestKind"] = swap.Type.ToString(),
        };
        if (extra != null)
            foreach (var kv in extra) data[kv.Key] = kv.Value;

        return new NotificationRequest(
            TenantId: tenantId,
            RecipientUserId: recipientUserId,
            Type: type,
            RelatedEntityId: swap.Id,
            Data: data,
            DeepLinkPath: $"/swaps/{swap.Id}");
    }

    private async Task<SwapRequest> ReloadAsync(Guid id)
        => await LoadSwaps().FirstAsync(r => r.Id == id);

    // ── Queries ──

    public async Task<IReadOnlyList<SwapRequest>> GetIncomingAsync(Guid userId)
        => await LoadSwaps()
            .Where(r => r.CounterpartMedicoId == userId && r.Status == SwapRequestStatus.Pending)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

    public async Task<IReadOnlyList<SwapRequest>> GetOutgoingAsync(Guid userId)
        => await LoadSwaps()
            .Where(r => r.InitiatorMedicoId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(100)
            .ToListAsync();

    public async Task<IReadOnlyList<SwapRequest>> GetHistoryAsync(Guid userId)
        => await LoadSwaps()
            .Where(r => r.InitiatorMedicoId == userId || r.CounterpartMedicoId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(200)
            .ToListAsync();

    public async Task<ServiceResult<IReadOnlyList<SwapCounterOffer>>> GetCounterOffersAsync(Guid userId, Guid swapId)
    {
        var swap = await _db.SwapRequests.FirstOrDefaultAsync(r => r.Id == swapId);
        if (swap is null) return ServiceResult<IReadOnlyList<SwapCounterOffer>>.NotFound();
        if (swap.InitiatorMedicoId != userId && swap.CounterpartMedicoId != userId)
            return ServiceResult<IReadOnlyList<SwapCounterOffer>>.Forbidden();

        var offers = await _db.SwapCounterOffers
            .Include(o => o.ProposedByMedico)
            .Include(o => o.OfferedShift)
            .Where(o => o.SwapRequestId == swapId)
            .OrderBy(o => o.CreatedAtUtc)
            .ToListAsync();
        return ServiceResult<IReadOnlyList<SwapCounterOffer>>.Ok(offers);
    }

    // ── Create Giveaway ──

    public async Task<ServiceResult<SwapRequest>> CreateGiveawayAsync(
        Guid userId, Guid shiftId, Guid toMedicoId, string? message, bool isReperibile = false)
    {
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId);
        if (shift is null) return ServiceResult<SwapRequest>.NotFound("Turno inesistente.");
        var ownerId = isReperibile ? shift.MedicoReperibileId : shift.MedicoTurnoId;
        if (ownerId != userId) return ServiceResult<SwapRequest>.Forbidden();
        if (shift.StartUtc <= DateTime.UtcNow)
            return ServiceResult<SwapRequest>.ValidationError("Turno già iniziato.");

        var to = await _db.Users.FirstOrDefaultAsync(u => u.Id == toMedicoId);
        if (to is null) return ServiceResult<SwapRequest>.ValidationError("Destinatario inesistente.");
        if (to.Id == userId) return ServiceResult<SwapRequest>.ValidationError("Non puoi cedere il turno a te stesso.");

        var alreadyPending = await _db.SwapRequests.AnyAsync(r =>
            r.InitiatorShiftId == shift.Id && r.Status == SwapRequestStatus.Pending);
        if (alreadyPending)
            return ServiceResult<SwapRequest>.Conflict("Esiste già una richiesta in sospeso per questo turno.");

        var swap = new SwapRequest
        {
            TenantId = shift.TenantId,
            Type = SwapRequestType.Giveaway,
            Status = SwapRequestStatus.Pending,
            InitiatorMedicoId = userId,
            InitiatorShiftId = shift.Id,
            CounterpartMedicoId = to.Id,
            Message = message,
            IsReperibile = isReperibile,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.SwapRequests.Add(swap);
        _audit.Log("SwapRequest", swap.Id, "GiveawayCreated", shift.TenantId,
            newValues: new { swap.InitiatorMedicoId, swap.CounterpartMedicoId, swap.InitiatorShiftId, swap.IsReperibile });

        var initiator = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        await _db.SaveChangesAsync();

        swap.InitiatorMedico = initiator!;
        swap.CounterpartMedico = to;
        swap.InitiatorShift = shift;
        await _dispatcher.DispatchAsync(BuildSwap(
            shift.TenantId, to.Id, NotificationTypeCodes.SwapRequested,
            swap, recipient: to, actor: initiator));

        return ServiceResult<SwapRequest>.Ok(await ReloadAsync(swap.Id));
    }

    // ── Create Multi Giveaway ──

    public async Task<ServiceResult<IReadOnlyList<SwapRequest>>> CreateMultiGiveawayAsync(
        Guid userId, Guid shiftId, List<Guid> recipientIds, string? message, bool isReperibile = false)
    {
        if (recipientIds is null || recipientIds.Count == 0)
            return ServiceResult<IReadOnlyList<SwapRequest>>.ValidationError("Seleziona almeno un destinatario.");
        if (recipientIds.Contains(userId))
            return ServiceResult<IReadOnlyList<SwapRequest>>.ValidationError("Non puoi cedere il turno a te stesso.");

        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId);
        if (shift is null) return ServiceResult<IReadOnlyList<SwapRequest>>.NotFound("Turno inesistente.");
        var ownerId = isReperibile ? shift.MedicoReperibileId : shift.MedicoTurnoId;
        if (ownerId != userId) return ServiceResult<IReadOnlyList<SwapRequest>>.Forbidden();
        if (shift.StartUtc <= DateTime.UtcNow)
            return ServiceResult<IReadOnlyList<SwapRequest>>.ValidationError("Turno già iniziato o passato.");

        var existing = await _db.SwapRequests
            .Where(r => r.InitiatorShiftId == shift.Id && r.Status == SwapRequestStatus.Pending)
            .ToListAsync();
        foreach (var ex in existing)
        {
            ex.Status = SwapRequestStatus.Cancelled;
            ex.ResolvedAtUtc = DateTime.UtcNow;
            ex.ResolutionReason = "Rimpiazzato da nuova richiesta";
        }

        var recipients = await _db.Users
            .Where(u => recipientIds.Contains(u.Id) && u.Id != userId)
            .ToListAsync();
        if (recipients.Count == 0)
            return ServiceResult<IReadOnlyList<SwapRequest>>.ValidationError("Nessun destinatario valido trovato.");

        var initiator = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        var created = new List<SwapRequest>();

        foreach (var to in recipients)
        {
            var swap = new SwapRequest
            {
                TenantId = shift.TenantId,
                Type = SwapRequestType.Giveaway,
                Status = SwapRequestStatus.Pending,
                InitiatorMedicoId = userId,
                InitiatorShiftId = shift.Id,
                CounterpartMedicoId = to.Id,
                Message = message,
                IsReperibile = isReperibile,
                CreatedAtUtc = DateTime.UtcNow,
            };
            _db.SwapRequests.Add(swap);
            created.Add(swap);
        }

        _audit.Log("SwapRequest", Guid.NewGuid(), "MultiGiveawayCreated", shift.TenantId,
            newValues: new { ShiftId = shift.Id, RecipientCount = created.Count, IsReperibile = isReperibile });
        await _db.SaveChangesAsync();

        var notifs = new List<NotificationRequest>();
        foreach (var (swap, to) in created.Zip(recipients))
        {
            swap.InitiatorMedico = initiator!;
            swap.CounterpartMedico = to;
            swap.InitiatorShift = shift;
            notifs.Add(BuildSwap(shift.TenantId, to.Id,
                NotificationTypeCodes.SwapRequested, swap, recipient: to, actor: initiator));
        }
        await _dispatcher.DispatchManyAsync(notifs);

        var ids = created.Select(s => s.Id).ToList();
        var fresh = await LoadSwaps().Where(r => ids.Contains(r.Id)).ToListAsync();
        return ServiceResult<IReadOnlyList<SwapRequest>>.Ok(fresh);
    }

    // ── Create Swap ──

    public async Task<ServiceResult<SwapRequest>> CreateSwapAsync(
        Guid userId, Guid myShiftId, Guid otherShiftId, string? message, bool isReperibile = false)
    {
        var mine = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == myShiftId);
        var other = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == otherShiftId);
        if (mine is null || other is null) return ServiceResult<SwapRequest>.NotFound();
        var myOwnerId = isReperibile ? mine.MedicoReperibileId : mine.MedicoTurnoId;
        if (myOwnerId != userId) return ServiceResult<SwapRequest>.Forbidden();
        var otherOwnerId = isReperibile ? other.MedicoReperibileId : other.MedicoTurnoId;
        if (otherOwnerId is null)
            return ServiceResult<SwapRequest>.ValidationError("Turno destinazione non assegnato.");
        if (otherOwnerId == userId)
            return ServiceResult<SwapRequest>.ValidationError("Non puoi scambiare con te stesso.");

        var alreadyPending = await _db.SwapRequests.AnyAsync(r =>
            (r.InitiatorShiftId == mine.Id || r.CounterpartShiftId == mine.Id ||
             r.InitiatorShiftId == other.Id || r.CounterpartShiftId == other.Id)
            && r.Status == SwapRequestStatus.Pending);
        if (alreadyPending)
            return ServiceResult<SwapRequest>.Conflict("Esiste già una richiesta in sospeso per uno dei due turni.");

        var swap = new SwapRequest
        {
            TenantId = mine.TenantId,
            Type = SwapRequestType.Swap,
            Status = SwapRequestStatus.Pending,
            InitiatorMedicoId = userId,
            InitiatorShiftId = mine.Id,
            CounterpartMedicoId = otherOwnerId,
            CounterpartShiftId = other.Id,
            Message = message,
            IsReperibile = isReperibile,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.SwapRequests.Add(swap);
        _audit.Log("SwapRequest", swap.Id, "SwapCreated", mine.TenantId,
            newValues: new { swap.InitiatorShiftId, swap.CounterpartShiftId, swap.IsReperibile });

        var swapInitiator = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        var counterpartUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == otherOwnerId!.Value);
        await _db.SaveChangesAsync();

        swap.InitiatorMedico = swapInitiator!;
        swap.CounterpartMedico = counterpartUser!;
        swap.InitiatorShift = mine;
        swap.CounterpartShift = other;
        await _dispatcher.DispatchAsync(BuildSwap(
            mine.TenantId, otherOwnerId!.Value,
            NotificationTypeCodes.SwapRequested,
            swap, recipient: counterpartUser, actor: swapInitiator));

        return ServiceResult<SwapRequest>.Ok(await ReloadAsync(swap.Id));
    }

    // ── Create Multi Swap ──

    public async Task<ServiceResult<IReadOnlyList<SwapRequest>>> CreateMultiSwapAsync(
        Guid userId, Guid myShiftId, List<Guid> candidateShiftIds, string? message, bool isReperibile = false)
    {
        if (candidateShiftIds is null || candidateShiftIds.Count == 0)
            return ServiceResult<IReadOnlyList<SwapRequest>>.ValidationError("Seleziona almeno un turno candidato.");

        var mine = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == myShiftId);
        if (mine is null) return ServiceResult<IReadOnlyList<SwapRequest>>.NotFound("Turno non trovato.");
        var myOwnerId = isReperibile ? mine.MedicoReperibileId : mine.MedicoTurnoId;
        if (myOwnerId != userId) return ServiceResult<IReadOnlyList<SwapRequest>>.Forbidden();
        if (mine.StartUtc <= DateTime.UtcNow)
            return ServiceResult<IReadOnlyList<SwapRequest>>.ValidationError("Turno già iniziato o passato.");

        var distinct = candidateShiftIds.Distinct().ToList();
        IQueryable<Shift> candidateQuery;
        if (isReperibile)
            candidateQuery = _db.Shifts.Where(s => distinct.Contains(s.Id) && s.MedicoReperibileId != null && s.MedicoReperibileId != userId);
        else
            candidateQuery = _db.Shifts.Where(s => distinct.Contains(s.Id) && s.MedicoTurnoId != null && s.MedicoTurnoId != userId);
        var candidates = await candidateQuery.ToListAsync();
        if (candidates.Count == 0)
            return ServiceResult<IReadOnlyList<SwapRequest>>.ValidationError("Nessun turno candidato valido.");

        var existing = await _db.SwapRequests
            .Where(r => (r.InitiatorShiftId == mine.Id || r.CounterpartShiftId == mine.Id)
                        && r.Status == SwapRequestStatus.Pending)
            .ToListAsync();
        foreach (var ex in existing)
        {
            ex.Status = SwapRequestStatus.Cancelled;
            ex.ResolvedAtUtc = DateTime.UtcNow;
            ex.ResolutionReason = "Rimpiazzato da nuova richiesta multi-scambio";
        }

        var initiator = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        var created = new List<SwapRequest>();
        var notifiedRecipients = new List<(SwapRequest Swap, ApplicationUser To, Shift Cand)>();

        foreach (var cand in candidates)
        {
            var candOwnerId = isReperibile ? cand.MedicoReperibileId!.Value : cand.MedicoTurnoId!.Value;
            var swap = new SwapRequest
            {
                TenantId = mine.TenantId,
                Type = SwapRequestType.Swap,
                Status = SwapRequestStatus.Pending,
                InitiatorMedicoId = userId,
                InitiatorShiftId = mine.Id,
                CounterpartMedicoId = candOwnerId,
                CounterpartShiftId = cand.Id,
                Message = message,
                IsReperibile = isReperibile,
                CreatedAtUtc = DateTime.UtcNow,
            };
            _db.SwapRequests.Add(swap);
            var to = await _db.Users.FirstOrDefaultAsync(u => u.Id == candOwnerId);
            if (to != null) notifiedRecipients.Add((swap, to, cand));
            created.Add(swap);
        }

        _audit.Log("SwapRequest", Guid.NewGuid(), "MultiSwapCreated", mine.TenantId,
            newValues: new { MyShiftId = mine.Id, CandidateCount = created.Count, IsReperibile = isReperibile });
        await _db.SaveChangesAsync();

        var notifs = new List<NotificationRequest>();
        foreach (var (swap, to, cand) in notifiedRecipients)
        {
            swap.InitiatorMedico = initiator!;
            swap.CounterpartMedico = to;
            swap.InitiatorShift = mine;
            swap.CounterpartShift = cand;
            notifs.Add(BuildSwap(mine.TenantId, to.Id,
                NotificationTypeCodes.SwapRequested, swap, recipient: to, actor: initiator));
        }
        await _dispatcher.DispatchManyAsync(notifs);

        var ids = created.Select(s => s.Id).ToList();
        var fresh = await LoadSwaps().Where(r => ids.Contains(r.Id)).ToListAsync();
        return ServiceResult<IReadOnlyList<SwapRequest>>.Ok(fresh);
    }

    // ── Pick From Board ──

    public async Task<ServiceResult<SwapRequest>> PickFromBoardAsync(Guid userId, Guid shiftId)
    {
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId);
        if (shift is null) return ServiceResult<SwapRequest>.NotFound();
        if (shift.Status != ShiftStatus.OnBoard)
            return ServiceResult<SwapRequest>.ValidationError("Turno non in bacheca.");
        if (shift.MedicoTurnoId is null)
            return ServiceResult<SwapRequest>.ValidationError("Turno scoperto.");
        if (shift.MedicoTurnoId == userId)
            return ServiceResult<SwapRequest>.ValidationError("Sei già tu l'assegnatario.");

        var swap = new SwapRequest
        {
            TenantId = shift.TenantId,
            Type = SwapRequestType.PickFromBoard,
            Status = SwapRequestStatus.Pending,
            InitiatorMedicoId = shift.MedicoTurnoId.Value,
            InitiatorShiftId = shift.Id,
            CounterpartMedicoId = userId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.SwapRequests.Add(swap);
        await _db.SaveChangesAsync();

        return await ResolveAcceptance(userId, swap.Id, force: true);
    }

    // ── Accept / Reject / Cancel ──

    public Task<ServiceResult<SwapRequest>> AcceptAsync(Guid userId, Guid swapId, bool force)
        => ResolveAcceptance(userId, swapId, force);

    public async Task<ServiceResult<SwapRequest>> RejectAsync(Guid userId, Guid swapId, string? reason)
    {
        var swap = await LoadSwaps().FirstOrDefaultAsync(r => r.Id == swapId);
        if (swap is null) return ServiceResult<SwapRequest>.NotFound();
        if (swap.CounterpartMedicoId != userId) return ServiceResult<SwapRequest>.Forbidden();
        if (swap.Status != SwapRequestStatus.Pending)
            return ServiceResult<SwapRequest>.ValidationError("Richiesta non più pendente.");

        swap.Status = SwapRequestStatus.Rejected;
        swap.ResolvedAtUtc = DateTime.UtcNow;
        swap.ResolutionReason = reason;
        _audit.Log("SwapRequest", swap.Id, "Rejected", swap.TenantId, notes: reason);
        await _db.SaveChangesAsync();
        await MarkSwapNotificationsReadAsync(swap.Id);

        var extras = new Dictionary<string, string?> { ["Reason"] = reason };
        await _dispatcher.DispatchAsync(BuildSwap(
            swap.TenantId, swap.InitiatorMedicoId,
            NotificationTypeCodes.SwapRejected,
            swap, recipient: swap.InitiatorMedico, actor: swap.CounterpartMedico, extra: extras));

        return ServiceResult<SwapRequest>.Ok(swap);
    }

    public async Task<ServiceResult<SwapRequest>> CancelAsync(Guid userId, Guid swapId)
    {
        var swap = await LoadSwaps().FirstOrDefaultAsync(r => r.Id == swapId);
        if (swap is null) return ServiceResult<SwapRequest>.NotFound();
        if (swap.InitiatorMedicoId != userId) return ServiceResult<SwapRequest>.Forbidden();
        if (swap.Status != SwapRequestStatus.Pending)
            return ServiceResult<SwapRequest>.ValidationError("Richiesta non più pendente.");

        swap.Status = SwapRequestStatus.Cancelled;
        swap.ResolvedAtUtc = DateTime.UtcNow;
        _audit.Log("SwapRequest", swap.Id, "Cancelled", swap.TenantId);
        await _db.SaveChangesAsync();
        await MarkSwapNotificationsReadAsync(swap.Id);

        if (swap.CounterpartMedicoId.HasValue && swap.CounterpartMedico is not null)
        {
            await _dispatcher.DispatchAsync(BuildSwap(
                swap.TenantId, swap.CounterpartMedicoId.Value,
                NotificationTypeCodes.SwapCancelled,
                swap, recipient: swap.CounterpartMedico, actor: swap.InitiatorMedico));
        }

        return ServiceResult<SwapRequest>.Ok(swap);
    }

    // ── Core acceptance logic ──

    private async Task<ServiceResult<SwapRequest>> ResolveAcceptance(
        Guid userId, Guid swapId, bool force = false, bool skipCallerAuthCheck = false)
    {
        var swap = await LoadSwaps().FirstOrDefaultAsync(r => r.Id == swapId);
        if (swap is null) return ServiceResult<SwapRequest>.NotFound();
        if (!skipCallerAuthCheck && swap.CounterpartMedicoId != userId)
            return ServiceResult<SwapRequest>.Forbidden();
        if (swap.Status != SwapRequestStatus.Pending)
            return ServiceResult<SwapRequest>.ValidationError("Richiesta non più pendente.");

        var window = TimeSpan.FromDays(7);
        var initiatorId = swap.InitiatorMedicoId;
        var counterpartId = swap.CounterpartMedicoId!.Value;

        var minStart = swap.InitiatorShift.StartUtc - window;
        var maxEnd = swap.InitiatorShift.EndUtc + window;
        if (swap.CounterpartShift is not null)
        {
            if (swap.CounterpartShift.StartUtc - window < minStart) minStart = swap.CounterpartShift.StartUtc - window;
            if (swap.CounterpartShift.EndUtc + window > maxEnd) maxEnd = swap.CounterpartShift.EndUtc + window;
        }

        var counterpartShifts = await _db.Shifts
            .Where(s => s.MedicoTurnoId == counterpartId
                        && s.StartUtc < maxEnd && s.EndUtc > minStart
                        && s.Status != ShiftStatus.Cancelled)
            .ToListAsync();

        ShiftValidationResult result;

        if (swap.Type == SwapRequestType.Swap && swap.CounterpartShift is not null)
        {
            var initiatorShifts = await _db.Shifts
                .Where(s => s.MedicoTurnoId == initiatorId
                            && s.StartUtc < maxEnd && s.EndUtc > minStart
                            && s.Status != ShiftStatus.Cancelled)
                .ToListAsync();

            result = _rules.ValidateSwap(
                initiatorId, swap.InitiatorShift, initiatorShifts,
                counterpartId, swap.CounterpartShift, counterpartShifts);
        }
        else
        {
            result = _rules.ValidateGiveaway(
                swap.InitiatorShift, initiatorId, counterpartId, counterpartShifts);
        }

        if (!result.IsValid)
        {
            if (result.HasBlockingViolations)
            {
                swap.Status = SwapRequestStatus.BlockedByRules;
                swap.ResolvedAtUtc = DateTime.UtcNow;
                swap.ResolutionReason = string.Join(" | ",
                    result.Violations.Select(v => $"[{v.Code}] {v.Message}"));
                _audit.Log("SwapRequest", swap.Id, "BlockedByRules", swap.TenantId,
                    notes: swap.ResolutionReason);
                await _db.SaveChangesAsync();

                return ServiceResult<SwapRequest>.Blocked(
                    "Scambio bloccato dal Rule Engine.", result.Violations);
            }

            if (!force)
            {
                return ServiceResult<SwapRequest>.NeedsConfirmation(
                    "Conferma necessaria: superi una soglia di tutela.", result.Violations);
            }

            _audit.Log("SwapRequest", swap.Id, "AcceptedWithWarnings", swap.TenantId,
                notes: string.Join(" | ", result.Violations.Select(v => $"[{v.Code}] {v.Message}")));
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        // Riassegna il turno iniziatore
        if (swap.IsReperibile)
        {
            var oldInitiatorMedico = swap.InitiatorShift.MedicoReperibileId;
            swap.InitiatorShift.MedicoReperibileId = counterpartId;
            swap.InitiatorShift.UpdatedAtUtc = DateTime.UtcNow;
            _audit.Log("Shift", swap.InitiatorShift.Id, "ReperibileReassigned", swap.TenantId,
                oldValues: new { MedicoReperibileId = oldInitiatorMedico },
                newValues: new { MedicoReperibileId = counterpartId, SwapId = swap.Id });
            _db.ShiftAssignmentHistories.Add(new ShiftAssignmentHistory
            {
                TenantId = swap.TenantId,
                ShiftId = swap.InitiatorShift.Id,
                PreviousMedicoId = oldInitiatorMedico,
                NewMedicoId = counterpartId,
                Reason = (swap.Type == SwapRequestType.Swap ? "Swap" : "Giveaway") + " (Reperibilità)",
                SwapRequestId = swap.Id,
                AtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            var oldInitiatorMedico = swap.InitiatorShift.MedicoTurnoId;
            swap.InitiatorShift.MedicoTurnoId = counterpartId;
            swap.InitiatorShift.UpdatedAtUtc = DateTime.UtcNow;
            if (swap.InitiatorShift.Status == ShiftStatus.OnBoard)
                swap.InitiatorShift.Status = ShiftStatus.Assigned;
            _audit.Log("Shift", swap.InitiatorShift.Id, "Reassigned", swap.TenantId,
                oldValues: new { MedicoTurnoId = oldInitiatorMedico },
                newValues: new { MedicoTurnoId = counterpartId, SwapId = swap.Id });
            _db.ShiftAssignmentHistories.Add(new ShiftAssignmentHistory
            {
                TenantId = swap.TenantId,
                ShiftId = swap.InitiatorShift.Id,
                PreviousMedicoId = oldInitiatorMedico,
                NewMedicoId = counterpartId,
                Reason = swap.Type == SwapRequestType.Swap ? "Swap" : "Giveaway",
                SwapRequestId = swap.Id,
                AtUtc = DateTime.UtcNow,
            });
        }

        // Riassegna il turno controparte (solo per Swap)
        if (swap.Type == SwapRequestType.Swap && swap.CounterpartShift is not null)
        {
            if (swap.IsReperibile)
            {
                var oldCounterMedico = swap.CounterpartShift.MedicoReperibileId;
                swap.CounterpartShift.MedicoReperibileId = initiatorId;
                swap.CounterpartShift.UpdatedAtUtc = DateTime.UtcNow;
                _audit.Log("Shift", swap.CounterpartShift.Id, "ReperibileReassigned", swap.TenantId,
                    oldValues: new { MedicoReperibileId = oldCounterMedico },
                    newValues: new { MedicoReperibileId = initiatorId, SwapId = swap.Id });
                _db.ShiftAssignmentHistories.Add(new ShiftAssignmentHistory
                {
                    TenantId = swap.TenantId,
                    ShiftId = swap.CounterpartShift.Id,
                    PreviousMedicoId = oldCounterMedico,
                    NewMedicoId = initiatorId,
                    Reason = "Swap (Reperibilità)",
                    SwapRequestId = swap.Id,
                    AtUtc = DateTime.UtcNow,
                });
            }
            else
            {
                var oldCounterMedico = swap.CounterpartShift.MedicoTurnoId;
                swap.CounterpartShift.MedicoTurnoId = initiatorId;
                swap.CounterpartShift.UpdatedAtUtc = DateTime.UtcNow;
                _audit.Log("Shift", swap.CounterpartShift.Id, "Reassigned", swap.TenantId,
                    oldValues: new { MedicoTurnoId = oldCounterMedico },
                    newValues: new { MedicoTurnoId = initiatorId, SwapId = swap.Id });
                _db.ShiftAssignmentHistories.Add(new ShiftAssignmentHistory
                {
                    TenantId = swap.TenantId,
                    ShiftId = swap.CounterpartShift.Id,
                    PreviousMedicoId = oldCounterMedico,
                    NewMedicoId = initiatorId,
                    Reason = "Swap",
                    SwapRequestId = swap.Id,
                    AtUtc = DateTime.UtcNow,
                });
            }
        }

        swap.Status = SwapRequestStatus.AutoApproved;
        swap.ResolvedAtUtc = DateTime.UtcNow;
        _audit.Log("SwapRequest", swap.Id, "AutoApproved", swap.TenantId,
            newValues: new { swap.Type, swap.InitiatorShiftId, swap.CounterpartShiftId });

        var siblings = await _db.SwapRequests
            .Where(r => r.InitiatorShiftId == swap.InitiatorShiftId
                        && r.Status == SwapRequestStatus.Pending
                        && r.Id != swap.Id)
            .ToListAsync();
        foreach (var sib in siblings)
        {
            sib.Status = SwapRequestStatus.Cancelled;
            sib.ResolvedAtUtc = DateTime.UtcNow;
            sib.ResolutionReason = "Turno già accettato da altro medico";
            _audit.Log("SwapRequest", sib.Id, "AutoCancelled", swap.TenantId);
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        var idsToClose = new List<Guid> { swap.Id };
        idsToClose.AddRange(siblings.Select(s => s.Id));
        await MarkSwapNotificationsReadAsync(idsToClose.ToArray());

        var siblingNotifs = new List<NotificationRequest>();
        foreach (var sib in siblings.Where(s => s.CounterpartMedicoId.HasValue))
        {
            sib.InitiatorMedico ??= swap.InitiatorMedico;
            sib.InitiatorShift ??= swap.InitiatorShift;
            siblingNotifs.Add(BuildSwap(
                swap.TenantId, sib.CounterpartMedicoId!.Value,
                NotificationTypeCodes.SwapAutoCancelled,
                sib, recipient: null, actor: swap.InitiatorMedico));
        }
        if (siblingNotifs.Count > 0) await _dispatcher.DispatchManyAsync(siblingNotifs);

        await _dispatcher.DispatchAsync(BuildSwap(
            swap.TenantId, swap.InitiatorMedicoId,
            NotificationTypeCodes.SwapAccepted,
            swap, recipient: swap.InitiatorMedico, actor: swap.CounterpartMedico));

        return ServiceResult<SwapRequest>.Ok(await ReloadAsync(swap.Id));
    }

    // ── Counter-offers ──

    public async Task<ServiceResult<SwapCounterOffer>> ProposeCounterAsync(
        Guid userId, Guid swapId, Guid offeredShiftId, string? message)
    {
        var swap = await LoadSwaps().FirstOrDefaultAsync(r => r.Id == swapId);
        if (swap is null) return ServiceResult<SwapCounterOffer>.NotFound();
        if (swap.InitiatorMedicoId != userId && swap.CounterpartMedicoId != userId)
            return ServiceResult<SwapCounterOffer>.Forbidden();
        if (swap.Status != SwapRequestStatus.Pending)
            return ServiceResult<SwapCounterOffer>.ValidationError("La richiesta non è più trattabile.");

        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == offeredShiftId);
        if (shift is null) return ServiceResult<SwapCounterOffer>.NotFound("Turno offerto inesistente.");
        if (shift.MedicoTurnoId != userId)
            return ServiceResult<SwapCounterOffer>.ValidationError("Puoi offrire solo turni di cui sei medico di turno.");
        if (shift.StartUtc <= DateTime.UtcNow)
            return ServiceResult<SwapCounterOffer>.ValidationError("Il turno offerto è già iniziato.");

        var previous = await _db.SwapCounterOffers
            .Where(o => o.SwapRequestId == swapId && o.Status == CounterOfferStatus.Pending)
            .ToListAsync();
        foreach (var p in previous)
        {
            p.Status = CounterOfferStatus.Superseded;
            p.ResolvedAtUtc = DateTime.UtcNow;
        }

        var offer = new SwapCounterOffer
        {
            TenantId = swap.TenantId,
            SwapRequestId = swap.Id,
            ProposedByMedicoId = userId,
            OfferedShiftId = shift.Id,
            Message = message,
            Status = CounterOfferStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.SwapCounterOffers.Add(offer);
        _audit.Log("SwapCounterOffer", offer.Id, "Created", swap.TenantId,
            newValues: new { swap.Id, offer.OfferedShiftId, offer.ProposedByMedicoId });

        await _db.SaveChangesAsync();

        var otherUserId = userId == swap.InitiatorMedicoId
            ? swap.CounterpartMedicoId
            : swap.InitiatorMedicoId;
        if (otherUserId.HasValue)
        {
            var actor = userId == swap.InitiatorMedicoId ? swap.InitiatorMedico : swap.CounterpartMedico;
            var recip = userId == swap.InitiatorMedicoId ? swap.CounterpartMedico : swap.InitiatorMedico;
            var extras = new Dictionary<string, string?>
            {
                ["OfferedShiftCode"] = shift.Code.ToString(),
                ["OfferedShiftDate"] = shift.Date.ToString("dd/MM/yyyy"),
            };
            await _dispatcher.DispatchAsync(BuildSwap(
                swap.TenantId, otherUserId.Value,
                NotificationTypeCodes.CounterOfferReceived,
                swap, recipient: recip, actor: actor, extra: extras));
        }

        var fresh = await _db.SwapCounterOffers
            .Include(o => o.ProposedByMedico).Include(o => o.OfferedShift)
            .FirstAsync(o => o.Id == offer.Id);
        return ServiceResult<SwapCounterOffer>.Ok(fresh);
    }

    public async Task<ServiceResult<SwapRequest>> AcceptCounterAsync(
        Guid userId, Guid swapId, Guid offerId, bool force)
    {
        var swap = await LoadSwaps().FirstOrDefaultAsync(r => r.Id == swapId);
        if (swap is null) return ServiceResult<SwapRequest>.NotFound();
        if (swap.Status != SwapRequestStatus.Pending)
            return ServiceResult<SwapRequest>.ValidationError("Richiesta non più pendente.");

        var offer = await _db.SwapCounterOffers
            .Include(o => o.OfferedShift).Include(o => o.ProposedByMedico)
            .FirstOrDefaultAsync(o => o.Id == offerId && o.SwapRequestId == swapId);
        if (offer is null) return ServiceResult<SwapRequest>.NotFound();
        if (offer.Status != CounterOfferStatus.Pending)
            return ServiceResult<SwapRequest>.ValidationError("Controproposta non più valida.");
        if (offer.ProposedByMedicoId == userId)
            return ServiceResult<SwapRequest>.ValidationError("Devi attendere la risposta dell'altro medico.");
        if (userId != swap.InitiatorMedicoId && userId != swap.CounterpartMedicoId)
            return ServiceResult<SwapRequest>.Forbidden();

        swap.Type = SwapRequestType.Swap;
        swap.CounterpartShiftId = offer.OfferedShiftId;
        swap.CounterpartShift = offer.OfferedShift;
        swap.CounterpartMedicoId = offer.ProposedByMedicoId;
        swap.CounterpartMedico = offer.ProposedByMedico;

        offer.Status = CounterOfferStatus.Accepted;
        offer.ResolvedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await ResolveAcceptance(userId, swap.Id, force, skipCallerAuthCheck: true);
    }

    public async Task<ServiceResult> RejectCounterAsync(Guid userId, Guid swapId, Guid offerId)
    {
        var swap = await _db.SwapRequests.FirstOrDefaultAsync(r => r.Id == swapId);
        if (swap is null) return ServiceResult.NotFound();
        var offer = await _db.SwapCounterOffers.FirstOrDefaultAsync(o => o.Id == offerId && o.SwapRequestId == swapId);
        if (offer is null) return ServiceResult.NotFound();
        if (offer.ProposedByMedicoId == userId)
            return ServiceResult.ValidationError("Non puoi rifiutare la tua stessa proposta.");
        if (userId != swap.InitiatorMedicoId && userId != swap.CounterpartMedicoId)
            return ServiceResult.Forbidden();
        if (offer.Status != CounterOfferStatus.Pending)
            return ServiceResult.ValidationError("Controproposta non più valida.");

        offer.Status = CounterOfferStatus.Rejected;
        offer.ResolvedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var fullSwap = await LoadSwaps().FirstAsync(r => r.Id == swap.Id);
        var actorUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        var proposer = await _db.Users.FirstOrDefaultAsync(u => u.Id == offer.ProposedByMedicoId);
        await _dispatcher.DispatchAsync(BuildSwap(
            swap.TenantId, offer.ProposedByMedicoId,
            NotificationTypeCodes.CounterOfferRejected,
            fullSwap, recipient: proposer, actor: actorUser));

        return ServiceResult.Ok();
    }
}
