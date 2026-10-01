using JojaDrop.Models;

namespace JojaDrop.Services;

/// <summary>Caches profiles from one resolved data snapshot until a relevant asset changes.</summary>
public sealed class AcquisitionProfileCache
{
    private readonly Func<AcquisitionIndexData> readData;
    private readonly IReadOnlyDictionary<string, ItemValuationOverride> overrides;
    private readonly Action<string>? diagnostic;
    private readonly AcquisitionProfileIndexer indexer = new();

    private readonly Dictionary<string, AcquisitionProfile> profiles =
        new(StringComparer.Ordinal);

    private readonly HashSet<string> reportedDiagnostics =
        new(StringComparer.Ordinal);

    private AcquisitionIndexData? data;

    public AcquisitionProfileCache(
        Func<AcquisitionIndexData> readData,
        IReadOnlyDictionary<string, ItemValuationOverride>? overrides = null,
        Action<string>? diagnostic = null)
    {
        this.readData = readData
            ?? throw new ArgumentNullException(nameof(readData));

        this.overrides = overrides
            ?? new Dictionary<string, ItemValuationOverride>(
                StringComparer.Ordinal);

        this.diagnostic = diagnostic;
    }

    public AcquisitionProfile Build(string qualifiedItemId)
    {
        if (string.IsNullOrWhiteSpace(qualifiedItemId))
        {
            throw new ArgumentException(
                "A qualified item ID is required.",
                nameof(qualifiedItemId));
        }

        if (profiles.TryGetValue(
                qualifiedItemId,
                out AcquisitionProfile? cached))
        {
            return cached;
        }

        AcquisitionProfile profile =
            indexer.Build(
                qualifiedItemId,
                data ??= readData());

        if (IsFallback(profile)
            && overrides.TryGetValue(
                qualifiedItemId,
                out ItemValuationOverride? itemOverride))
        {
            if (itemOverride.TryCreateRoute(
                    out AcquisitionRoute? route,
                    out string error))
            {
                profile = new AcquisitionProfile(
                    qualifiedItemId,
                    new[]
                    {
                        profile.Routes.Single(),
                        route!
                    });
            }
            else
            {
                Report(
                    qualifiedItemId,
                    $"Ignored valuation override: {error}");
            }
        }

        // Unknown/low-confidence fallback is expected for items
        // whose acquisition source isn't indexed yet.
        // Don't spam the SMAPI console for these normal fallbacks.

        return profiles[qualifiedItemId] = profile;
    }

    public void Invalidate()
    {
        data = null;
        profiles.Clear();
        reportedDiagnostics.Clear();
    }

    private void Report(
        string qualifiedItemId,
        string message)
    {
        if (diagnostic is not null
            && reportedDiagnostics.Add(qualifiedItemId))
        {
            diagnostic($"{qualifiedItemId}: {message}");
        }
    }

    private static bool IsFallback(AcquisitionProfile profile) =>
        profile.Routes.Count == 1
        && profile.Routes[0].Kind == AcquisitionKind.Unknown
        && profile.Routes[0].Confidence == AcquisitionConfidence.Low;
}