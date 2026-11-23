namespace ConfigurationEngine.View;

using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>
/// A konfigurációs ablak nézetmodelljeinek közös alapja: a WPF adatkötéshez szükséges
/// <see cref="INotifyPropertyChanged"/> megvalósítása.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Beállítja a mezőt, és ha az érték változott, értesíti a kötéseket.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
