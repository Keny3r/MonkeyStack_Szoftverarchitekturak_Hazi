namespace OsEventEngine.Monitoring;

using MonkeyStack.Core.Events;

/// <summary>
/// Honnan származik egy minta.
/// </summary>
internal enum SampleOrigin
{
    /// <summary>Valódi Win32 mérés.</summary>
    Live,

    /// <summary>A tesztpanel / <see cref="Simulation.ISimulationControl"/> által beállított érték.</summary>
    Simulated
}

/// <summary>
/// Egy metrika egy mintája: ez utazik a monitoroktól a feldolgozási láncon át a kiküldésig.
/// A <see cref="Metric"/> és az <see cref="Origin"/> egyben a minta "tokenje" (ACT jelleg):
/// ez alapján rendeli a kiküldő a megfelelő <c>OsEventNotifier.Notify…Changed</c> híváshoz.
/// </summary>
/// <param name="Metric">Melyik metrika.</param>
/// <param name="Value">Az érték a <see cref="MetricCatalog"/> szerinti egységben (Time: perc éjfél óta).</param>
/// <param name="Origin">Élő vagy szimulált.</param>
/// <param name="Timestamp">A mintavétel ideje.</param>
internal readonly record struct MetricSample(Metric Metric, int Value, SampleOrigin Origin, DateTimeOffset Timestamp);
