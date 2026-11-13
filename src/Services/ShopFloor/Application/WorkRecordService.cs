using ShopFloor.Data.Entities;

namespace ShopFloor.Application;

/// <summary>Storage used by the work-record service. Writes are checked against the row version the caller read.</summary>
public interface IWorkRecordStore
{
    Task<WorkRecord?> FindAsync(int id, CancellationToken cancellationToken = default);

    Task AddAsync(WorkRecord record, CancellationToken cancellationToken = default);

    /// <summary>Saves the record only if its stored row version still equals <paramref name="expectedRowVersion"/>. Returns false on a conflict.</summary>
    Task<bool> TryUpdateAsync(WorkRecord record, byte[] expectedRowVersion, CancellationToken cancellationToken = default);
}

public sealed record StartWorkCommand(int RequestOperationId, int UserId, int MachineId, DateTime Now);

public sealed record StopWorkCommand(
    int WorkRecordId,
    byte[] IfMatchRowVersion,
    double GoodQuantity,
    double BadQuantity,
    double UserCostPerHour,
    double MachineCostPerHour,
    CostMode Mode,
    DateTime Now);

public enum StopStatus
{
    Stopped,
    NotFound,
    NotStarted,
    Conflict,
}

public sealed record StopResult(StopStatus Status, WorkRecord? Record);

/// <summary>
/// Start and stop of a work record (T066). A stop needs a start, and it is refused with a conflict when the caller's
/// If-Match row version is out of date. Each write goes through one store call, which checks the row version (T067 adds the transaction boundary).
/// </summary>
public sealed class WorkRecordService(IWorkRecordStore store)
{
    private readonly IWorkRecordStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<WorkRecord> StartAsync(StartWorkCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var record = new WorkRecord
        {
            TOW_REQ_OPERATION_ID = command.RequestOperationId,
            TOW_USERS_ID = command.UserId,
            TOW_MACHINE_ID = command.MachineId,
            TOW_START_WORK = command.Now,
            TOW_STATUS = "A",
            TOW_QUANTITY_GOOD = 0,
            TOW_QUANTITY_BAD = 0,
            TOW_COST_GOOD = 0,
            TOW_COST_BAD = 0,
        };
        await _store.AddAsync(record, cancellationToken);
        return record;
    }

    public async Task<StopResult> StopAsync(StopWorkCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var record = await _store.FindAsync(command.WorkRecordId, cancellationToken);
        if (record is null)
        {
            return new StopResult(StopStatus.NotFound, null);
        }

        if (record.TOW_START_WORK is null)
        {
            return new StopResult(StopStatus.NotStarted, null);
        }

        var cost = WorkCostCalculator.Calculate(
            record.TOW_START_WORK.Value,
            command.Now,
            command.GoodQuantity,
            command.BadQuantity,
            command.UserCostPerHour,
            command.MachineCostPerHour,
            command.Mode);

        record.TOW_STOP_WORK = command.Now;
        record.TOW_QUANTITY_GOOD = command.GoodQuantity;
        record.TOW_QUANTITY_BAD = command.BadQuantity;
        record.TOW_COST_GOOD = cost.Good;
        record.TOW_COST_BAD = cost.Bad;

        var saved = await _store.TryUpdateAsync(record, command.IfMatchRowVersion, cancellationToken);
        return saved
            ? new StopResult(StopStatus.Stopped, record)
            : new StopResult(StopStatus.Conflict, null);
    }
}
