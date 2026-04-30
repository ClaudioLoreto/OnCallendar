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
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<SwapRequest> SwapRequests => Set<SwapRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Carica tutte le IEntityTypeConfiguration<> dell'assembly
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // ---- Global Query Filters ----
        // 1) Soft delete
        // 2) Multi-tenant: se il provider restituisce un tenant, filtra automaticamente.
        var currentTenantId = _tenantProvider.GetCurrentTenantId();

        builder.Entity<Tenant>().HasQueryFilter(t => !t.IsDeleted);

        builder.Entity<ApplicationUser>().HasQueryFilter(u =>
            !u.IsDeleted &&
            (currentTenantId == null || u.TenantId == currentTenantId));

        builder.Entity<Shift>().HasQueryFilter(s =>
            !s.IsDeleted &&
            (currentTenantId == null || s.TenantId == currentTenantId));

        builder.Entity<ShiftAssignment>().HasQueryFilter(a =>
            !a.IsDeleted &&
            (currentTenantId == null || a.TenantId == currentTenantId));

        builder.Entity<SwapRequest>().HasQueryFilter(r =>
            !r.IsDeleted &&
            (currentTenantId == null || r.TenantId == currentTenantId));

        builder.Entity<AuditLog>().HasQueryFilter(l =>
            currentTenantId == null || l.TenantId == currentTenantId);
    }
}
