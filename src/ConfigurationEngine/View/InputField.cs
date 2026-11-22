namespace ConfigurationEngine.View;

using System.Collections;
using System.ComponentModel;
using System.Globalization;

/// <summary>
/// Egy szöveges beviteli mező (pl. számmező) a hozzá tartozó hibákkal (gui.md, "Mezőnkénti hibajelzés").
/// A beírt szöveg csak akkor kerül a szerkesztett másolatba, ha átalakítható; különben beviteli hiba keletkezik,
/// és a másolatban az utolsó érvényes érték marad. A hibákat <see cref="INotifyDataErrorInfo"/>-val adja a WPF
/// kötésnek (piros keret), és <see cref="ErrorText"/>-ként a mező alatti felirathoz.
/// </summary>
public sealed class InputField : ObservableObject, INotifyDataErrorInfo
{
    private readonly Func<string> _read;
    private readonly Func<string, string?> _write;
    private readonly Action<string?> _committed;
    private string _text;
    private string? _inputError;
    private IReadOnlyList<string> _errors = [];

    /// <param name="propertyName">A modell mezőjének neve (a <see cref="Validation.ValidationError.PropertyName"/>).</param>
    /// <param name="read">A modell értéke szövegként.</param>
    /// <param name="write">Átalakítja és a modellbe írja a szöveget; a beviteli hiba üzenete, vagy <c>null</c>, ha sikerült.</param>
    /// <param name="committed">Minden beírás után, a beviteli hibával (vagy <c>null</c>-lal).</param>
    internal InputField(string propertyName, Func<string> read, Func<string, string?> write, Action<string?> committed)
    {
        PropertyName = propertyName;
        _read = read;
        _write = write;
        _committed = committed;
        _text = read();
    }

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>A modell mezőjének neve.</summary>
    public string PropertyName { get; }

    /// <summary>A mező szövege; minden módosításkor megpróbálja a modellbe írni.</summary>
    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value ?? string.Empty))
            {
                _inputError = _write(_text);
                _committed(_inputError);
            }
        }
    }

    /// <summary>A szöveg nem alakítható át, ezért nem került a szerkesztett másolatba.</summary>
    public bool HasInputError => _inputError is not null;

    /// <summary>A mező hibái (beviteli, validátor- és médiahibák) soronként, a mező alatti felirathoz.</summary>
    public string ErrorText => string.Join(Environment.NewLine, _errors);

    public bool HasErrors => _errors.Count > 0;

    public IEnumerable GetErrors(string? propertyName) =>
        string.IsNullOrEmpty(propertyName) || propertyName == nameof(Text) ? _errors : Array.Empty<string>();

    /// <summary>A modell értékének újraolvasása (pl. ha egy másik mező módosította), ha nincs beviteli hiba.</summary>
    public void Refresh()
    {
        if (_inputError is not null)
        {
            return;
        }

        var text = _read();
        if (text != _text)
        {
            _text = text;
            OnPropertyChanged(nameof(Text));
        }
    }

    /// <summary>A beviteli hiba elvetése és a modell értékének visszaírása a mezőbe.</summary>
    public void Reset()
    {
        _inputError = null;
        Refresh();
    }

    internal void SetErrors(IReadOnlyList<string> errors)
    {
        if (errors.SequenceEqual(_errors))
        {
            return;
        }

        _errors = errors;
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Text)));
        OnPropertyChanged(nameof(ErrorText));
        OnPropertyChanged(nameof(HasErrors));
    }

    /// <summary>Egész szám: üres vagy nem szám esetén beviteli hiba.</summary>
    internal static Func<string, string?> IntWriter(Action<int> set) => text =>
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return "A mező nem lehet üres.";
        }

        if (!TryParseInt(trimmed, out var value))
        {
            return "Egész számot adj meg.";
        }

        set(value);
        return null;
    };

    /// <summary>Nem kötelező egész szám: üres szövegnél <c>null</c> (a kötelezőséget a validátor ellenőrzi).</summary>
    internal static Func<string, string?> OptionalIntWriter(Action<int?> set) => text =>
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            set(null);
            return null;
        }

        if (!TryParseInt(trimmed, out var value))
        {
            return "Egész számot adj meg.";
        }

        set(value);
        return null;
    };

    /// <summary>Valós szám (tizedesvesszővel vagy -ponttal): üres vagy nem szám esetén beviteli hiba.</summary>
    internal static Func<string, string?> DoubleWriter(Action<double> set) => text =>
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return "A mező nem lehet üres.";
        }

        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            && !double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return "Számot adj meg.";
        }

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "Számot adj meg.";
        }

        set(value);
        return null;
    };

    internal static string Format(int value) => value.ToString(CultureInfo.CurrentCulture);

    internal static string Format(int? value) => value?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;

    internal static string Format(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    private static bool TryParseInt(string text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
        || int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}
