using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OnCallendar.Domain.Entities;

namespace OnCallendar.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<ApplicationUser> Users { get; }
    DbSet<Shift> Shifts { get; }
    DbSet<SwapRequest> SwapRequests { get; }
    DbSet<SwapCounterOffer> SwapCounterOffers { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<UserDeviceToken> UserDeviceTokens { get; }
    DbSet<ExternalDoctor> ExternalDoctors { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<ShiftAssignmentHistory> ShiftAssignmentHistories { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
