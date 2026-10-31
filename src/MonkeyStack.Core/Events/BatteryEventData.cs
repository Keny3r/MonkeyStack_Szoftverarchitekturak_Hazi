namespace MonkeyStack.Core.Events;

/// <summary>
/// Akkumulátor töltöttségi szint esemény adatmodellje.
/// </summary>
public class BatteryEventData
{
    public int ChargePercent { get; set; }
}
