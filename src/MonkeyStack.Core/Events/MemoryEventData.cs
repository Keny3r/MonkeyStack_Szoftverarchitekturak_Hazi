namespace MonkeyStack.Core.Events;

/// <summary>
/// Rendszermemória kihasználtság esemény adatmodellje.
/// </summary>
public class MemoryEventData
{
    public int UsagePercent { get; set; }
}
