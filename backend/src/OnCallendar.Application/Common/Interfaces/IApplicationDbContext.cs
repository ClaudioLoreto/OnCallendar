using Microsoft.EntityFrameworkCore;
using OnCallendar.Domain.Entities;

namespace OnCallendar.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<ApplicationUser> Users { get; }
    DbSet<Shift> Shifts { get; }
    DbSet<ShiftAssignment> ShiftAssignments { get; }
    DbSet<SwapRequest> SwapRequests { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
