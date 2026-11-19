namespace ConfigurationEngine.Model;

using MonkeyStack.Core.Events;

/// <summary>
/// A triggerfeltétel összehasonlító operátora.
/// </summary>
public enum ComparisonOperator
{
    /// <summary>Nagyobb (&gt;).</summary>
    Gt,

    /// <summary>Nagyobb vagy egyenlő (≥).</summary>
    Gte,

    /// <summary>Kisebb (&lt;).</summary>
    Lt,

    /// <summary>Kisebb vagy egyenlő (≤).</summary>
    Lte,

    /// <summary>
    /// Zárt intervallum: <c>Threshold ≤ érték ≤ ThresholdUpper</c>.
    /// <see cref="Metric.Time"/> esetén az alsó határ nagyobb is lehet (éjfélen átnyúló intervallum).
    /// </summary>
    Between
}
