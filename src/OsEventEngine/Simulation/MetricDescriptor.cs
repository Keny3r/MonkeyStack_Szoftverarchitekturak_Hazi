namespace OsEventEngine.Simulation;

using OsEventEngine.Monitoring;

/// <summary>
/// Egy metrika leírója a UI és a szimuláció számára. Az OS Mock View ebből építi fel a sorait,
/// így a View-ban nincs metrika-specifikus kód.
/// </summary>
/// <param name="Metric">A metrika.</param>
/// <param name="DisplayName">Megjelenített név.</param>
/// <param name="Min">Csúszka / szimulált érték alsó határa.</param>
/// <param name="Max">Csúszka / szimulált érték felső határa (teszteléshez szűkebb lehet a teljes tartománynál).</param>
/// <param name="Unit">Mértékegység a megjelenítéshez.</param>
public sealed record MetricDescriptor(Metric Metric, string DisplayName, int Min, int Max, string Unit);
