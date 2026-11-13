using ShopFloor.Application;
using ShopFloor.Data.Entities;
using Xunit;

namespace Platform.Services.Tests;

/// <summary>T056: start and stop of a work record. Stop without start is rejected, and a stale If-Match is refused with a conflict.</summary>
public class WorkRecordServiceTests
{
    /// <summary>In-memory store that follows the row-version rule: a write needs the version the caller read.</summary>
    private sealed class FakeWorkRecordStore : IWorkRecordStore
    {
        private readonly Dictionary<int, WorkRecord> _records = new();
        private int _nextId = 1;

        public Task<WorkRecord?> FindAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_records.TryGetValue(id, out var record) ? Copy(record) : null);

        public Task AddAsync(WorkRecord record, CancellationToken cancellationToken = default)
        {
            record.TOW_ID = _nextId++;
            record.ROWVER = NextVersion(0);
            _records[record.TOW_ID] = Copy(record);
            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateAsync(WorkRecord record, byte[] expectedRowVersion, CancellationToken cancellationToken = default)
        {
            if (!_records.TryGetValue(record.TOW_ID, out var stored) || !stored.ROWVER.SequenceEqual(expectedRowVersion))
            {
                return Task.FromResult(false);
            }
            record.ROWVER = NextVersion(stored.ROWVER[^1]);
            _records[record.TOW_ID] = Copy(record);
            return Task.FromResult(true);
        }

        public WorkRecord? Peek(int id) => _records.TryGetValue(id, out var record) ? Copy(record) : null;

        private static byte[] NextVersion(byte last) => [0, 0, 0, 0, 0, 0, 0, (byte)(last + 1)];

        private static WorkRecord Copy(WorkRecord r) => new()
        {
            TOW_ID = r.TOW_ID,
            TOW_REQ_OPERATION_ID = r.TOW_REQ_OPERATION_ID,
            TOW_DIC_ID = r.TOW_DIC_ID,
            TOW_START_WORK = r.TOW_START_WORK,
            TOW_STOP_WORK = r.TOW_STOP_WORK,
            TOW_STATUS = r.TOW_STATUS,
            TOW_USERS_ID = r.TOW_USERS_ID,
            TOW_MACHINE_ID = r.TOW_MACHINE_ID,
            TOW_QUANTITY_GOOD = r.TOW_QUANTITY_GOOD,
            TOW_QUANTITY_BAD = r.TOW_QUANTITY_BAD,
            TOW_COST_GOOD = r.TOW_COST_GOOD,
            TOW_COST_BAD = r.TOW_COST_BAD,
            ROWVER = (byte[])r.ROWVER.Clone(),
        };
    }

    private static readonly DateTime Start = new(2026, 11, 13, 10, 0, 0);
    private static readonly DateTime Stop = new(2026, 11, 13, 12, 0, 0);

    private static StartWorkCommand StartCommand() => new(RequestOperationId: 14965, UserId: 1002, MachineId: 1001, Now: Start);

    private static StopWorkCommand StopCommand(int id, byte[] ifMatch) =>
        new(id, ifMatch, GoodQuantity: 3, BadQuantity: 1, UserCostPerHour: 25, MachineCostPerHour: 100, Mode: CostMode.UserAndMachine, Now: Stop);

    [Fact]
    public async Task Start_CreatesOpenRecord()
    {
        var store = new FakeWorkRecordStore();
        var service = new WorkRecordService(store);

        var record = await service.StartAsync(StartCommand());

        Assert.Equal(Start, record.TOW_START_WORK);
        Assert.Null(record.TOW_STOP_WORK);
        Assert.Equal("A", record.TOW_STATUS);
        Assert.Equal(1002, record.TOW_USERS_ID);
        Assert.Equal(1001, record.TOW_MACHINE_ID);
        Assert.Equal(14965, record.TOW_REQ_OPERATION_ID);
    }

    [Fact]
    public async Task Stop_AfterStart_SavesStopTimeQuantitiesAndCost()
    {
        var store = new FakeWorkRecordStore();
        var service = new WorkRecordService(store);
        var started = await service.StartAsync(StartCommand());

        var result = await service.StopAsync(StopCommand(started.TOW_ID, started.ROWVER));

        Assert.Equal(StopStatus.Stopped, result.Status);
        Assert.Equal(Stop, result.Record!.TOW_STOP_WORK);
        Assert.Equal(3, result.Record.TOW_QUANTITY_GOOD);
        Assert.Equal(1, result.Record.TOW_QUANTITY_BAD);
        // 2 hours at 125 per hour (user 25 + machine 100) = 250, split 3:1 between good and bad.
        Assert.Equal(187.5, result.Record.TOW_COST_GOOD!.Value, precision: 9);
        Assert.Equal(62.5, result.Record.TOW_COST_BAD!.Value, precision: 9);
    }

    [Fact]
    public async Task Stop_WithoutStart_IsRejected_AndRecordUnchanged()
    {
        var store = new FakeWorkRecordStore();
        var service = new WorkRecordService(store);
        // A record that exists but was never started: start time is empty.
        var created = new WorkRecord { TOW_REQ_OPERATION_ID = 14965, TOW_STATUS = "A", TOW_USERS_ID = 1002, TOW_MACHINE_ID = 1001 };
        await store.AddAsync(created);

        var result = await service.StopAsync(StopCommand(created.TOW_ID, created.ROWVER));

        Assert.Equal(StopStatus.NotStarted, result.Status);
        Assert.Null(store.Peek(created.TOW_ID)!.TOW_STOP_WORK);
    }

    [Fact]
    public async Task Stop_WithStaleIfMatch_Conflicts_AndRecordUnchanged()
    {
        var store = new FakeWorkRecordStore();
        var service = new WorkRecordService(store);
        var started = await service.StartAsync(StartCommand());
        var stale = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x7F };

        var result = await service.StopAsync(StopCommand(started.TOW_ID, stale));

        Assert.Equal(StopStatus.Conflict, result.Status);
        Assert.Null(store.Peek(started.TOW_ID)!.TOW_STOP_WORK);
    }

    [Fact]
    public async Task Stop_UnknownRecord_IsNotFound()
    {
        var service = new WorkRecordService(new FakeWorkRecordStore());

        var result = await service.StopAsync(StopCommand(999, new byte[8]));

        Assert.Equal(StopStatus.NotFound, result.Status);
    }
}
