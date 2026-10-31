namespace MonkeyStack.Core.Events;

/// <summary>
/// Központi eseményadattároló regiszter, amely az összes OS esemény aktuális állapotmodelljét tárolja.
/// További esemény-adatmodellekkel könnyen bővíthető.
/// </summary>
public class OsEventDataRegistry
{
    public CpuEventData? Cpu { get; set; }
    public MemoryEventData? Memory { get; set; }
    public DiskEventData? Disk { get; set; }
    public BatteryEventData? Battery { get; set; }
    public WindowCountEventData? WindowCount { get; set; }
    public TimeEventData? Time { get; set; }
    public InactivityEventData? Inactivity { get; set; }
}
