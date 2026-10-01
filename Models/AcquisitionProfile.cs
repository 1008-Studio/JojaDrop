using System.Collections.ObjectModel;

namespace JojaDrop.Models;

/// <summary>How an item can be acquired.</summary>
public enum AcquisitionKind
{
    Fishing,
    Farming,
    Foraging,
    Mining,
    Geode,
    MonsterDrop,
    Machine,
    Crafting,
    Cooking,
    Shop,
    QuestEvent,
    Unknown
}

/// <summary>How directly the route is supported by discovered game data.</summary>
public enum AcquisitionConfidence
{
    Unknown,
    Low,
    Medium,
    High
}

/// <summary>Normalized route metrics, each on a 0 through 100 scale.</summary>
public sealed record AcquisitionMetrics
{
    public const int Minimum = 0;
    public const int Maximum = 100;

    public AcquisitionMetrics(int difficulty, int scarcity, int access, int effort, int restrictions,
        int uniqueness, int farmability)
    {
        Difficulty = Validate(difficulty, nameof(difficulty));
        Scarcity = Validate(scarcity, nameof(scarcity));
        Access = Validate(access, nameof(access));
        Effort = Validate(effort, nameof(effort));
        Restrictions = Validate(restrictions, nameof(restrictions));
        Uniqueness = Validate(uniqueness, nameof(uniqueness));
        Farmability = Validate(farmability, nameof(farmability));
    }

    public int Difficulty { get; }
    public int Scarcity { get; }
    public int Access { get; }
    public int Effort { get; }
    public int Restrictions { get; }
    public int Uniqueness { get; }
    public int Farmability { get; }

    private static int Validate(int value, string parameterName)
    {
        if (value < Minimum || value > Maximum)
            throw new ArgumentOutOfRangeException(parameterName, value, $"Metrics must be between {Minimum} and {Maximum}.");

        return value;
    }
}

/// <summary>Traceable evidence for a discovered acquisition route.</summary>
public sealed record AcquisitionEvidence
{
    public AcquisitionEvidence(string source, string detail, string? condition = null)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Evidence source is required.", nameof(source));
        if (string.IsNullOrWhiteSpace(detail))
            throw new ArgumentException("Evidence detail is required.", nameof(detail));

        Source = source;
        Detail = detail;
        Condition = string.IsNullOrWhiteSpace(condition) ? null : condition;
    }

    public string Source { get; }
    public string Detail { get; }
    public string? Condition { get; }
}

/// <summary>One evidenced way to acquire an item. Profiles may contain many routes.</summary>
public sealed record AcquisitionRoute
{
    public AcquisitionRoute(AcquisitionKind kind, AcquisitionMetrics metrics, AcquisitionConfidence confidence,
        IEnumerable<AcquisitionEvidence> evidence)
    {
        if (!Enum.IsDefined(typeof(AcquisitionKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (!Enum.IsDefined(typeof(AcquisitionConfidence), confidence))
            throw new ArgumentOutOfRangeException(nameof(confidence));
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(evidence);

        AcquisitionEvidence[] evidenceItems = evidence.ToArray();
        if (evidenceItems.Length == 0 || evidenceItems.Any(item => item is null))
            throw new ArgumentException("A route needs at least one evidence item.", nameof(evidence));
        if ((kind == AcquisitionKind.Unknown) != (confidence == AcquisitionConfidence.Unknown))
            throw new ArgumentException("Only an unknown route may have unknown confidence.", nameof(confidence));

        Kind = kind;
        Metrics = metrics;
        Confidence = confidence;
        Evidence = new ReadOnlyCollection<AcquisitionEvidence>(evidenceItems);
    }

    public AcquisitionKind Kind { get; }
    public AcquisitionMetrics Metrics { get; }
    public AcquisitionConfidence Confidence { get; }
    public IReadOnlyList<AcquisitionEvidence> Evidence { get; }
}

/// <summary>All known, independently evidenced acquisition routes for one qualified item ID.</summary>
public sealed record AcquisitionProfile
{
    public AcquisitionProfile(string qualifiedItemId, IEnumerable<AcquisitionRoute> routes)
    {
        if (string.IsNullOrWhiteSpace(qualifiedItemId))
            throw new ArgumentException("A qualified item ID is required.", nameof(qualifiedItemId));
        ArgumentNullException.ThrowIfNull(routes);

        AcquisitionRoute[] routeItems = routes.ToArray();
        if (routeItems.Length == 0 || routeItems.Any(route => route is null))
            throw new ArgumentException("A profile needs at least one route.", nameof(routes));

        QualifiedItemId = qualifiedItemId;
        Routes = new ReadOnlyCollection<AcquisitionRoute>(routeItems);
    }

    public string QualifiedItemId { get; }
    public IReadOnlyList<AcquisitionRoute> Routes { get; }
}

/// <summary>A future points result and the profile it was derived from; no valuation policy is implied here.</summary>
public sealed record ValuationBreakdown
{
    public ValuationBreakdown(AcquisitionProfile profile, int points)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (points < 0)
            throw new ArgumentOutOfRangeException(nameof(points), "Points cannot be negative.");

        Profile = profile;
        Points = points;
    }

    public AcquisitionProfile Profile { get; }
    public int Points { get; }
}
