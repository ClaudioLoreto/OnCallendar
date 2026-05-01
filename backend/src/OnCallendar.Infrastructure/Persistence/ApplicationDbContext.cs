using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Domain.Entities;

namespace OnCallendar.Infrastructure.Persistence;

public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IApplicationDbContext
{
    private readonly ITenantProvider _tenantProvider;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public new DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<SwapRequest> SwapRequests => Set<SwapRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        var currentTenantId = _tenantProvider.GetCurrentTenantId();

        builder.Entity<Tenant>().HasQueryFilter(t => !t.IsDeleted);

        builder.Entity<ApplicationUser>().HasQueryFilter(u =>
            !u.IsDeleted &&
            (currentTenantId == null || u.TenantId == currentTenantId));

        builder.Entity<Shift>().HasQueryFilter(s =>
            !s.IsDeleted &&
            (currentTenantId == null || s.TenantId == currentTenantId));

        builder.Entity<SwapRequest>().HasQueryFilter(r =>
            !r.IsDeleted &&
            (currentTenantId == null || r.TenantId == currentTenantId));

        builder.Entity<AuditLog>().HasQueryFilter(l =>
            currentTenantId == null || l.TenantId == currentTenantId);

        builder.Entity<Notification>().HasQueryFilter(n =>
            currentTenantId == null || n.TenantId == currentTenantId);
    }
}
