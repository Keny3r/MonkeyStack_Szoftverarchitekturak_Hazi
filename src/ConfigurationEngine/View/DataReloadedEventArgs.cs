namespace ConfigurationEngine.View;

/// <summary>
/// Miért cserélődött le a szerkesztett másolat. A fülek ebből döntik el, mi legyen az előnézetekkel
/// (view.md, "Élettartam"): mentés után megmaradnak, Elvetéskor a View előbb mindet eldobja.
/// </summary>
public enum DataReloadReason
{
    /// <summary>Az ablak megnyitása.</summary>
    Opened,

    /// <summary>Sikeres mentés után a mentett állapot (az új azonosítókkal); az előnézetek megmaradnak.</summary>
    Saved,

    /// <summary>Elvetés után a legutóbb mentett állapot; az előnézetek már el vannak dobva.</summary>
    Discarded
}

/// <summary>
/// A <see cref="ConfigurationWindowViewModel.DataReloaded"/> esemény adatai.
/// </summary>
public sealed class DataReloadedEventArgs : EventArgs
{
    public DataReloadedEventArgs(DataReloadReason reason)
    {
        Reason = reason;
    }

    public DataReloadReason Reason { get; }
}
