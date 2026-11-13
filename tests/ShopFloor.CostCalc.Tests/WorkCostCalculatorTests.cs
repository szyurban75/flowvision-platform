using ShopFloor.Application;

namespace ShopFloor.CostCalc.Tests;

public class WorkCostCalculatorTests
{
    private static readonly DateTime Start = new(2026, 6, 12, 10, 0, 0);
    private static readonly DateTime Stop = Start.AddHours(2);

    [Fact]
    public void MachineOnly_UsesMachineRateOnly()
    {
        var cost = WorkCostCalculator.Calculate(Start, Stop, 1, 0, userCostPerHour: 25, machineWorkCostPerHour: 150, CostMode.MachineOnly);

        Assert.Equal(300.0, cost.Good, precision: 9);
        Assert.Equal(0.0, cost.Bad, precision: 9);
    }

    [Fact]
    public void UserAndMachine_AddsUserRate()
    {
        var cost = WorkCostCalculator.Calculate(Start, Stop, 1, 0, userCostPerHour: 25, machineWorkCostPerHour: 150, CostMode.UserAndMachine);

        Assert.Equal(350.0, cost.Good, precision: 9);
    }

    [Fact]
    public void Quantities_SplitCostByShare()
    {
        var cost = WorkCostCalculator.Calculate(Start, Stop, 3, 1, userCostPerHour: 0, machineWorkCostPerHour: 100, CostMode.MachineOnly);

        Assert.Equal(150.0, cost.Good, precision: 9);
        Assert.Equal(50.0, cost.Bad, precision: 9);
    }

    [Fact]
    public void NoQuantity_PutsFullCostOnGood()
    {
        var cost = WorkCostCalculator.Calculate(Start, Stop, 0, 0, userCostPerHour: 0, machineWorkCostPerHour: 100, CostMode.MachineOnly);

        Assert.Equal(200.0, cost.Good, precision: 9);
        Assert.Equal(0.0, cost.Bad, precision: 9);
    }

    [Fact]
    public void UnknownMode_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorkCostCalculator.Calculate(Start, Stop, 1, 0, 0, 0, (CostMode)99));
    }
}
