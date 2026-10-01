namespace JojaDrop.Models;

/// <summary>Optional manual fallback for an item whose acquisition is unavailable in resolved data assets.</summary>
public sealed class ModConfig
{
    public Dictionary<string, ItemValuationOverride> ItemOverrides { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ItemValuationOverride
{
    public string? Source { get; set; }
    public double? Difficulty { get; set; }
    public double? Scarcity { get; set; }
    public double? Access { get; set; }
    public double? Effort { get; set; }
    public double? Restrictions { get; set; }
    public double? Uniqueness { get; set; }
    public double? Farmability { get; set; }

    internal bool TryCreateRoute(out AcquisitionRoute? route, out string error)
    {
        route = null;
        if (!Enum.TryParse(Source, ignoreCase: true, out AcquisitionKind kind)
            || kind == AcquisitionKind.Unknown || !Enum.IsDefined(typeof(AcquisitionKind), kind))
        {
            error = "Source must name a supported non-Unknown acquisition kind.";
            return false;
        }

        try
        {
            route = new AcquisitionRoute(kind, new AcquisitionMetrics(Metric(Difficulty), Metric(Scarcity), Metric(Access),
                Metric(Effort), Metric(Restrictions), Metric(Uniqueness), Metric(Farmability)), AcquisitionConfidence.High,
                new[] { new AcquisitionEvidence("JojaDrop config", $"manual fallback override; source={kind}") });
            error = string.Empty;
            return true;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            error = $"{exception.ParamName} must be between 0 and 1.";
            return false;
        }
    }

    private static int? Metric(double? value)
    {
        if (!value.HasValue)
            return null;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value < 0d || value.Value > 1d)
            throw new ArgumentOutOfRangeException(nameof(value));
        return (int)Math.Round(value.Value * AcquisitionMetrics.Maximum, MidpointRounding.AwayFromZero);
    }
}
