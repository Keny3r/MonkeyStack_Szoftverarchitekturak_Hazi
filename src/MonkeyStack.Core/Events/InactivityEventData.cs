namespace MonkeyStack.Core.Events;

/// <summary>
/// Felhasználói inaktivitás ideje esemény adatmodellje perc bontásban.
/// </summary>
public class InactivityEventData
{
    public int InactivityMinutes { get; set; }
}
