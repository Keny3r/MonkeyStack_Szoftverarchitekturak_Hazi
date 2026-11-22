using System.ComponentModel;
using System.Windows;
using ConfigurationEngine.View;
using ConfigurationEngine.View.Fakes;
using MonkeyStack.Core.Configuration;

namespace ConfigurationEngine;

/// <summary>
/// A Konfigurációs motor View ablaka, a konfigurációs ablak (docs/configuration_engine/gui.md).
/// A motort csak az <see cref="IConfigurationEditor"/> homlokzaton keresztül éri el (Wrapper Facade, Extension Interface):
/// integrált futásnál a host a közös <c>ConfigurationService</c> példányt adja át <see cref="IConfigurationEditor"/>-ként,
/// önálló (F5) futásnál az ablak maga hoz létre egyet (gui.md, "Az ablak létrehozása").
/// </summary>
public partial class MainWindow : Window
{
    private readonly IDisposable? _ownedEditor;
    private bool _closeConfirmed;

    /// <summary>Önálló (F5) indítás saját <c>ConfigurationService</c> példánnyal.</summary>
    public MainWindow()
        : this(ResolveEditor(new ConfigurationService()))
    {
    }

    /// <summary>Integrált indítás: a host a közös szolgáltatást adja át a View homlokzataként.</summary>
    public MainWindow(IConfigurationEditor editor)
        : this((editor ?? throw new ArgumentNullException(nameof(editor)), null))
    {
    }

    /// <summary>
    /// Átmeneti, a host mostani hívásához (<c>CharacterEngine</c> <c>App.OpenConfigurationWindow</c>): a kapott
    /// szolgáltatást <see cref="IConfigurationEditor"/>-ként használja. Amíg a <c>ConfigurationService</c> nem valósítja
    /// meg a homlokzatot, az ablak memóriában tároló minta szerkesztővel (<see cref="FakeConfigurationEditor"/>) fut,
    /// és ezt a címsorban jelzi.
    /// </summary>
    public MainWindow(IConfigurationService? configurationService)
        : this(ResolveEditor(configurationService ?? new ConfigurationService()))
    {
    }

    private MainWindow((IConfigurationEditor Editor, IDisposable? Owned) editor)
    {
        Editor = editor.Editor;
        _ownedEditor = editor.Owned;
        ViewModel = new ConfigurationWindowViewModel(Editor, new MessageBoxDialogs(this));
        InitializeComponent();
        DataContext = ViewModel;

        if (_ownedEditor is not null)
        {
            Title += " – minta adatok (memóriában, nem tárolódik)";
        }

        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    /// <summary>A View homlokzata a motorhoz.</summary>
    public IConfigurationEditor Editor { get; }

    public ConfigurationWindowViewModel ViewModel { get; }

    /// <summary>
    /// Bezárás előtt mentetlen módosításnál megkérdezi, hogy menti, elveti, vagy nyitva hagyja az ablakot
    /// (gui.md, "Elvetés és bezárás"). A döntés aszinkron (mentés), ezért az első bezárási kérést elutasítja,
    /// és a döntés után maga zárja be az ablakot.
    /// </summary>
    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_closeConfirmed || (!ViewModel.IsDirty && !ViewModel.IsBusy))
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        base.OnClosing(e);

        if (await ViewModel.ConfirmCloseAsync())
        {
            _closeConfirmed = true;

            // A bezárás a Closing eseményen kívül, a Dispatcher sorában fut újra.
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        ViewModel.OnWindowClosed();
        _ownedEditor?.Dispose();
        base.OnClosed(e);
    }

    private static (IConfigurationEditor Editor, IDisposable? Owned) ResolveEditor(IConfigurationService service)
    {
        if (service is IConfigurationEditor editor)
        {
            return (editor, null);
        }

        var fake = new FakeConfigurationEditor();
        return (fake, fake);
    }
}
