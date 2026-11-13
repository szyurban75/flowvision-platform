namespace ShopFloor.Application;

/// <summary>
/// Which hourly rates a work record is costed with.
/// MachineOnly: machine work cost only. UserAndMachine: user cost plus machine work cost.
/// The mode is an explicit input; the rule that picks a mode for a given operation is not decided yet.
/// </summary>
public enum CostMode
{
    MachineOnly,
    UserAndMachine,
}

/// <summary>Cost of a work record, split by good and bad quantity. Currency as stored in TIME_OF_OPERATIONS.</summary>
public readonly record struct WorkCost(double Good, double Bad);

/// <summary>
/// Pure cost calculation for a work record (T065), ported from the desktop stop logic (Srv_RequestOperation.cs).
/// Hours are split by the good and bad share. With no quantity, the full cost goes to good.
/// </summary>
public static class WorkCostCalculator
{
    public static WorkCost Calculate(
        DateTime start,
        DateTime stop,
        double goodQuantity,
        double badQuantity,
        double userCostPerHour,
        double machineWorkCostPerHour,
        CostMode mode)
    {
        var hours = (stop - start).TotalHours;
        var ratePerHour = mode switch
        {
            CostMode.MachineOnly => machineWorkCostPerHour,
            CostMode.UserAndMachine => userCostPerHour + machineWorkCostPerHour,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown cost mode."),
        };

        var total = goodQuantity + badQuantity;
        var cost = hours * ratePerHour;
        if (total <= 0)
        {
            return new WorkCost(Good: cost, Bad: 0.0);
        }

        return new WorkCost(
            Good: cost * goodQuantity / total,
            Bad: cost * badQuantity / total);
    }
}
