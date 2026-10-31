namespace OsEventEngine;

using MonkeyStack.Core.Events;

/// <summary>
/// Az OS eseménymotor konkrét értesítő osztálya.
/// Minden rendszereseményhez eseményt biztosít, amelyre a karakter motor feliratkozhat.
/// A konkrét OS monitorozást a komponensfelelős implementálja.
/// </summary>
public class OsEventNotifier
{
    public event EventHandler<CpuEventData>? CpuChanged;
    public event EventHandler<MemoryEventData>? MemoryChanged;
    public event EventHandler<DiskEventData>? DiskChanged;
    public event EventHandler<BatteryEventData>? BatteryChanged;
    public event EventHandler<WindowCountEventData>? WindowCountChanged;
    public event EventHandler<TimeEventData>? TimeChanged;
    public event EventHandler<InactivityEventData>? InactivityChanged;

    protected virtual void OnCpuChanged(CpuEventData data) => CpuChanged?.Invoke(this, data);
    protected virtual void OnMemoryChanged(MemoryEventData data) => MemoryChanged?.Invoke(this, data);
    protected virtual void OnDiskChanged(DiskEventData data) => DiskChanged?.Invoke(this, data);
    protected virtual void OnBatteryChanged(BatteryEventData data) => BatteryChanged?.Invoke(this, data);
    protected virtual void OnWindowCountChanged(WindowCountEventData data) => WindowCountChanged?.Invoke(this, data);
    protected virtual void OnTimeChanged(TimeEventData data) => TimeChanged?.Invoke(this, data);
    protected virtual void OnInactivityChanged(InactivityEventData data) => InactivityChanged?.Invoke(this, data);
}
