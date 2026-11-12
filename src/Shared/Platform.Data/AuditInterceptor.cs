using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Platform.Data;

/// <summary>Entities that record who changed them and when (FR-011).</summary>
public interface IAuditable
{
    string? UpdatedBy { get; set; }
    DateTime UpdatedAt { get; set; }
}

/// <summary>Fills the audit fields of every added or modified IAuditable entity before it is saved.</summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _clock;
    private readonly Func<string?> _currentUser;

    public AuditInterceptor(Func<string?> currentUser, TimeProvider? clock = null)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? TimeProvider.System;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var user = _currentUser();

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = user;
            }
        }
    }
}
