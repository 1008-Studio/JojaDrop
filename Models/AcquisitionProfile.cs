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

/// <summary>Known normalized route metrics, each on a 0 through 100 scale.</summary>
public sealed record AcquisitionMetrics
{
    public const int Minimum = 0;
    public const int Maximum = 100;

    public AcquisitionMetrics(int? difficulty, int? scarcity, int? access, int? effort, int? restrictions,
        int? uniqueness, int? farmability, double? availabilityChance = null)
    {
        Difficulty = Validate(difficulty, nameof(difficulty));
        Scarcity = Validate(scarcity, nameof(scarcity));
        Access = Validate(access, nameof(access));
        Effort = Validate(effort, nameof(effort));
        Restrictions = Validate(restrictions, nameof(restrictions));
        Uniqueness = Validate(uniqueness, nameof(uniqueness));
        Farmability = Validate(farmability, nameof(farmability));
        AvailabilityChance = ValidateChance(availabilityChance);
    }

    public int? Difficulty { get; }
    public int? Scarcity { get; }
    public int? Access { get; }
    public int? Effort { get; }
    public int? Restrictions { get; }
    public int? Uniqueness { get; }
    public int? Farmability { get; }
    public double? AvailabilityChance { get; }

    private static int? Validate(int? value, string parameterName)
    {
        if (value.HasValue && (value.Value < Minimum || value.Value > Maximum))
            throw new ArgumentOutOfRangeException(parameterName, value, $"Metrics must be between {Minimum} and {Maximum}.");

        return value;
    }

    private static double? ValidateChance(double? value)
    {
        if (value.HasValue && (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value < 0d || value.Value > 1d))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Availability chance must be between zero and one.");

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

/// <summary>Static production facts for one output route; dynamic outputs intentionally have no expected output.</summary>
public sealed record ProductionRelationship
{
    public ProductionRelationship(IEnumerable<ProductionInput> inputs, int minOutput, int maxOutput,
        bool isRandomOutput = false, bool hasCustomOutputMethod = false)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (minOutput < 1 || maxOutput < minOutput)
            throw new ArgumentOutOfRangeException(nameof(minOutput), "Production output must be positive and ordered.");

        ProductionInput[] inputItems = inputs.ToArray();
        if (inputItems.Any(input => input is null || input.Quantity < 1 || string.IsNullOrWhiteSpace(input.ItemId)))
            throw new ArgumentException("Production inputs must have an item ID and positive quantity.", nameof(inputs));

        Inputs = new ReadOnlyCollection<ProductionInput>(inputItems);
        MinOutput = minOutput;
        MaxOutput = maxOutput;
        IsRandomOutput = isRandomOutput;
        HasCustomOutputMethod = hasCustomOutputMethod;
    }

    public IReadOnlyList<ProductionInput> Inputs { get; }
    public int MinOutput { get; }
    public int MaxOutput { get; }
    public bool IsRandomOutput { get; }
    public bool HasCustomOutputMethod { get; }
    public double? ExpectedOutput => IsRandomOutput || HasCustomOutputMethod ? null : (MinOutput + MaxOutput) / 2d;
}

/// <summary>One evidenced way to acquire an item. Profiles may contain many routes.</summary>
public sealed record AcquisitionRoute
{
    public AcquisitionRoute(AcquisitionKind kind, AcquisitionMetrics metrics, AcquisitionConfidence confidence,
        IEnumerable<AcquisitionEvidence> evidence, ProductionRelationship? production = null)
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
        if (kind != AcquisitionKind.Unknown && confidence == AcquisitionConfidence.Unknown)
            throw new ArgumentException("Only an unknown route may have unknown confidence.", nameof(confidence));

        Kind = kind;
        Metrics = metrics;
        Confidence = confidence;
        Evidence = new ReadOnlyCollection<AcquisitionEvidence>(evidenceItems);
        Production = production;
    }

    public AcquisitionKind Kind { get; }
    public AcquisitionMetrics Metrics { get; }
    public AcquisitionConfidence Confidence { get; }
    public IReadOnlyList<AcquisitionEvidence> Evidence { get; }
    public ProductionRelationship? Production { get; }
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

/// <summary>Per-route details from a deterministic points valuation.</summary>
public sealed record RouteValuationBreakdown(AcquisitionRoute Route, double Difficulty, double Scarcity,
    double Access, double Effort, double Restrictions, double Uniqueness, double Farmability, double Score,
    double Multiplier, int Points, bool IsReliable, int? ProductionFloor = null,
    double? ProductionMultiplier = null, string? ProductionReason = null);

/// <summary>One canonical base value and profile available to production dependency resolution.</summary>
public sealed record ValuationInput
{
    public ValuationInput(AcquisitionProfile profile, int canonicalBaseValue)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (canonicalBaseValue < 0)
            throw new ArgumentOutOfRangeException(nameof(canonicalBaseValue));

        Profile = profile;
        CanonicalBaseValue = canonicalBaseValue;
    }

    public AcquisitionProfile Profile { get; }
    public int CanonicalBaseValue { get; }
}

/// <summary>A deterministic points result and its route-level calculation.</summary>
public sealed record ValuationBreakdown
{
    public ValuationBreakdown(AcquisitionProfile profile, int points)
        : this(profile, canonicalBaseValue: 0, points, selectedMultiplier: 1d,
            Array.Empty<RouteValuationBreakdown>(), "No valuation calculation was supplied.")
    {
    }

    public ValuationBreakdown(AcquisitionProfile profile, int canonicalBaseValue, int points, double selectedMultiplier,
        IEnumerable<RouteValuationBreakdown> routes, string selectionReason)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (canonicalBaseValue < 0)
            throw new ArgumentOutOfRangeException(nameof(canonicalBaseValue), "Canonical base value cannot be negative.");
        if (points < 0)
            throw new ArgumentOutOfRangeException(nameof(points), "Points cannot be negative.");
        if (double.IsNaN(selectedMultiplier) || double.IsInfinity(selectedMultiplier) || selectedMultiplier < 0d)
            throw new ArgumentOutOfRangeException(nameof(selectedMultiplier));
        ArgumentNullException.ThrowIfNull(routes);
        if (string.IsNullOrWhiteSpace(selectionReason))
            throw new ArgumentException("A selection reason is required.", nameof(selectionReason));

        Profile = profile;
        CanonicalBaseValue = canonicalBaseValue;
        Points = points;
        SelectedMultiplier = selectedMultiplier;
        Routes = new ReadOnlyCollection<RouteValuationBreakdown>(routes.ToArray());
        SelectionReason = selectionReason;
    }

    public AcquisitionProfile Profile { get; }
    public int CanonicalBaseValue { get; }
    public int Points { get; }
    public double SelectedMultiplier { get; }
    public IReadOnlyList<RouteValuationBreakdown> Routes { get; }
    public string SelectionReason { get; }
}
