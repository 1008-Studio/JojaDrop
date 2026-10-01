namespace JojaDrop.Models;

/// <summary>Filtering mode for the target item picker, based on the upgrade probability.</summary>
public enum TargetFilterMode
{
    /// <summary>Show every allowed target without probability filtering.</summary>
    All,

    /// <summary>Targets whose upgrade chance is near 50% (roughly a x2 value increase).</summary>
    X2,

    /// <summary>Targets whose upgrade chance is near 33% (roughly a x3 value increase).</summary>
    X3,

    /// <summary>Targets whose upgrade chance is near 20% (roughly a x5 value increase).</summary>
    X5,

    /// <summary>Targets whose upgrade chance is near 10% (roughly a x10 value increase).</summary>
    X10
}
