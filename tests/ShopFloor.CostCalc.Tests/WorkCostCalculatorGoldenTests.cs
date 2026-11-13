using System.Globalization;
using ShopFloor.Application;

namespace ShopFloor.CostCalc.Tests;

/// <summary>T054 (SC-008): each golden operation from FLOWVISION2_TEST matches the stored desktop cost within 0.01%, in the mode labelled for it.</summary>
public class WorkCostCalculatorGoldenTests
{
    private const double RelativeTolerance = 0.0001;

    private sealed record GoldenRow(
        string TowId,
        DateTime Start,
        DateTime Stop,
        double GoodQuantity,
        double BadQuantity,
        double UserCost,
        double MachineCost,
        double StoredGood,
        double StoredBad,
        string Mode);

    private static IReadOnlyList<GoldenRow> LoadGolden()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Golden", "operations-golden.csv");
        var lines = File.ReadAllLines(path);
        var header = lines[0].Split(',');
        int Index(string name) => Array.IndexOf(header, name);

        var rows = new List<GoldenRow>();
        foreach (var line in lines.Skip(1).Where(l => l.Length > 0))
        {
            var f = line.Split(',');
            rows.Add(new GoldenRow(
                TowId: f[Index("tow_id")],
                Start: DateTime.Parse(f[Index("start_work")], CultureInfo.InvariantCulture, DateTimeStyles.None),
                Stop: DateTime.Parse(f[Index("stop_work")], CultureInfo.InvariantCulture, DateTimeStyles.None),
                GoodQuantity: double.Parse(f[Index("quantity_good")], CultureInfo.InvariantCulture),
                BadQuantity: double.Parse(f[Index("quantity_bad")], CultureInfo.InvariantCulture),
                UserCost: double.Parse(f[Index("users_cost")], CultureInfo.InvariantCulture),
                MachineCost: double.Parse(f[Index("machine_cost_work")], CultureInfo.InvariantCulture),
                StoredGood: double.Parse(f[Index("cost_good")], CultureInfo.InvariantCulture),
                StoredBad: double.Parse(f[Index("cost_bad")], CultureInfo.InvariantCulture),
                Mode: f[Index("mode")]));
        }
        return rows;
    }

    private static bool Close(double expected, double actual)
    {
        if (Math.Abs(actual) < 1e-9)
        {
            return Math.Abs(expected) < 0.01;
        }
        return Math.Abs(expected - actual) / Math.Abs(actual) <= RelativeTolerance;
    }

    private static bool MatchesMode(GoldenRow row, CostMode mode)
    {
        var result = WorkCostCalculator.Calculate(row.Start, row.Stop, row.GoodQuantity, row.BadQuantity, row.UserCost, row.MachineCost, mode);
        return Close(row.StoredGood, result.Good) && Close(row.StoredBad, result.Bad);
    }

    [Fact]
    public void GoldenFile_HasRows()
    {
        Assert.NotEmpty(LoadGolden());
    }

    [Fact]
    public void EachRow_MatchesItsLabelledMode()
    {
        var failures = new List<string>();
        foreach (var row in LoadGolden())
        {
            var ok = row.Mode switch
            {
                "machine_only" => MatchesMode(row, CostMode.MachineOnly),
                "user_and_machine" => MatchesMode(row, CostMode.UserAndMachine),
                "either" => MatchesMode(row, CostMode.MachineOnly) || MatchesMode(row, CostMode.UserAndMachine),
                _ => false,
            };
            if (!ok)
            {
                failures.Add($"{row.TowId} ({row.Mode})");
            }
        }

        Assert.True(failures.Count == 0, "Rows not matching their mode: " + string.Join(", ", failures));
    }
}
