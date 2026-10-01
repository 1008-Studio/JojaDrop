using System.Globalization;
using System.Text;
using JojaDrop.Models;

namespace JojaDrop.Services;

/// <summary>Produces a deterministic CSV and summary for offline balance inspection.</summary>
public sealed class ValuationSimulationExporter
{
    private readonly PointsValuationEngine engine;

    public ValuationSimulationExporter(PointsValuationEngine? engine = null) => this.engine = engine ?? new PointsValuationEngine();

    public ValuationSimulationReport Simulate(IEnumerable<ValuationSimulationInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ValuationSimulationInput[] source = inputs.ToArray();
        ValuationInput[] values = source.Select(input => new ValuationInput(input.Profile, input.BasePrice)).ToArray();
        ValuationSimulationEntry[] entries = source.Select(input => CreateEntry(input, engine.Evaluate(input.Profile, input.BasePrice, values)))
            .OrderBy(entry => entry.QualifiedItemId, StringComparer.Ordinal).ToArray();
        int[] points = entries.Select(entry => entry.Points).OrderBy(value => value).ToArray();
        return new ValuationSimulationReport(entries, new ValuationSimulationStatistics(points),
            entries.OrderBy(entry => entry.Points).ThenBy(entry => entry.QualifiedItemId, StringComparer.Ordinal).Take(10).ToArray(),
            entries.OrderByDescending(entry => entry.Points).ThenBy(entry => entry.QualifiedItemId, StringComparer.Ordinal).Take(10).ToArray());
    }

    public string ToCsv(ValuationSimulationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var csv = new StringBuilder("QualifiedItemId,Name,BasePrice,Points,SelectedRoute,Multiplier,Confidence,Difficulty,Scarcity,Access,Effort,Conditions,Uniqueness,Farmability\n");
        foreach (ValuationSimulationEntry entry in report.Entries)
        {
            csv.AppendLine(string.Join(',',
                Csv(entry.QualifiedItemId), Csv(entry.Name), entry.BasePrice.ToString(CultureInfo.InvariantCulture),
                entry.Points.ToString(CultureInfo.InvariantCulture), Csv(entry.SelectedRoute.ToString()),
                entry.Multiplier.ToString("0.####", CultureInfo.InvariantCulture), Csv(entry.Confidence.ToString()),
                Number(entry.Difficulty), Number(entry.Scarcity), Number(entry.Access), Number(entry.Effort),
                Csv(entry.Conditions), Number(entry.Uniqueness), Number(entry.Farmability)));
        }
        return csv.ToString();
    }

    public string ToSummary(ValuationSimulationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ValuationSimulationStatistics stats = report.Statistics;
        return $"count={stats.Count}\nmin={stats.Min}\nmax={stats.Max}\nmean={stats.Mean:0.##}\nmedian={stats.Median:0.##}\n"
            + $"P50={stats.P50:0.##}\nP75={stats.P75:0.##}\nP90={stats.P90:0.##}\nP95={stats.P95:0.##}\nP99={stats.P99:0.##}\n\n"
            + Outliers("bottom", report.BottomOutliers) + "\n" + Outliers("top", report.TopOutliers);
    }

    private static ValuationSimulationEntry CreateEntry(ValuationSimulationInput input, ValuationBreakdown valuation)
    {
        RouteValuationBreakdown? selected = valuation.Routes.Where(route => route.IsReliable)
            .OrderBy(route => route.Points).FirstOrDefault();
        AcquisitionRoute? route = selected?.Route;
        return new ValuationSimulationEntry(input.Profile.QualifiedItemId, input.Name, input.BasePrice, valuation.Points,
            route?.Kind ?? AcquisitionKind.Unknown, valuation.SelectedMultiplier, route?.Confidence ?? AcquisitionConfidence.Low,
            route?.Metrics.Difficulty, route?.Metrics.Scarcity, route?.Metrics.Access, route?.Metrics.Effort,
            route is null ? string.Empty : string.Join(" | ", route.Evidence.Select(evidence => evidence.Condition)
                .Where(condition => !string.IsNullOrWhiteSpace(condition)).Distinct(StringComparer.Ordinal)),
            route?.Metrics.Uniqueness, route?.Metrics.Farmability);
    }

    private static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
    private static string Outliers(string label, IEnumerable<ValuationSimulationEntry> entries) => $"{label}:\n"
        + string.Join('\n', entries.Select(entry => $"{entry.Points}\t{entry.QualifiedItemId}\t{entry.Name}\t{entry.SelectedRoute}"));
}

public sealed record ValuationSimulationInput
{
    public ValuationSimulationInput(string name, AcquisitionProfile profile, int basePrice)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A display name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(profile);
        if (basePrice < 0)
            throw new ArgumentOutOfRangeException(nameof(basePrice));

        Name = name;
        Profile = profile;
        BasePrice = basePrice;
    }

    public string Name { get; }
    public AcquisitionProfile Profile { get; }
    public int BasePrice { get; }
}

public sealed record ValuationSimulationEntry(string QualifiedItemId, string Name, int BasePrice, int Points,
    AcquisitionKind SelectedRoute, double Multiplier, AcquisitionConfidence Confidence, int? Difficulty, int? Scarcity,
    int? Access, int? Effort, string Conditions, int? Uniqueness, int? Farmability);

public sealed record ValuationSimulationReport(IReadOnlyList<ValuationSimulationEntry> Entries,
    ValuationSimulationStatistics Statistics, IReadOnlyList<ValuationSimulationEntry> BottomOutliers,
    IReadOnlyList<ValuationSimulationEntry> TopOutliers);

public sealed record ValuationSimulationStatistics
{
    public ValuationSimulationStatistics(IReadOnlyList<int> sortedPoints)
    {
        ArgumentNullException.ThrowIfNull(sortedPoints);
        Count = sortedPoints.Count;
        Min = Count == 0 ? 0 : sortedPoints[0];
        Max = Count == 0 ? 0 : sortedPoints[^1];
        Mean = Count == 0 ? 0d : sortedPoints.Average();
        Median = Percentile(sortedPoints, 0.50d);
        P50 = Median;
        P75 = Percentile(sortedPoints, 0.75d);
        P90 = Percentile(sortedPoints, 0.90d);
        P95 = Percentile(sortedPoints, 0.95d);
        P99 = Percentile(sortedPoints, 0.99d);
    }

    public int Count { get; }
    public int Min { get; }
    public int Max { get; }
    public double Mean { get; }
    public double Median { get; }
    public double P50 { get; }
    public double P75 { get; }
    public double P90 { get; }
    public double P95 { get; }
    public double P99 { get; }

    private static double Percentile(IReadOnlyList<int> values, double percentile)
    {
        if (values.Count == 0)
            return 0d;
        double position = (values.Count - 1) * percentile;
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        return values[lower] + (values[upper] - values[lower]) * (position - lower);
    }
}
