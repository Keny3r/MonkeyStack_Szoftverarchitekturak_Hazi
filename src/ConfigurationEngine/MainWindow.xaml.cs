using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using ConfigurationEngine.View;
using ConfigurationEngine.View.Fakes;
using MonkeyStack.Core.Configuration;
using MonkeyStack.Core.Timing;

namespace ConfigurationEngine;

/// <summary>
/// A Konfigurációs motor View ablaka, a konfigurációs ablak (docs/configuration_engine/view.md).
/// A motort csak az <see cref="IConfigurationEditor"/> homlokzaton keresztül éri el (Wrapper Facade, Extension Interface):
/// integrált futásnál a host a közös <c>ConfigurationService</c> példányt adja át <see cref="IConfigurationEditor"/>-ként,
/// önálló (F5) futásnál az ablak maga hoz létre egyet, a saját léptetőjével (<see cref="FrameLoop"/>), hogy az előnézetek
/// ott is lépjenek, és bezáráskor mindkettőt eldobja (view.md, "Az ablak létrehozása").
/// </summary>
public partial class MainWindow : Window
{
    private readonly IDisposable? _ownedEditor;
    private bool _closeConfirmed;

    /// <summary>Önálló (F5) indítás saját <c>ConfigurationService</c> példánnyal és saját léptetővel.</summary>
    public MainWindow()
        : this(CreateStandaloneEditor())
    {
    }

    /// <summary>Integrált indítás: a host a közös szolgáltatást adja át a View homlokzataként.</summary>
    public MainWindow(IConfigurationEditor editor)
        : this((editor ?? throw new ArgumentNullException(nameof(editor)), null))
    {
    }

    /// <summary>
    /// Átmeneti, a host mostani hívásához (<c>CharacterEngine</c> <c>App.OpenConfigurationWindow</c>): a kapott
    /// szolgáltatást <see cref="IConfigurationEditor"/>-ként használja; <c>null</c> esetén önálló futásként indul. A
    /// <c>ConfigurationService</c> megvalósítja a homlokzatot; ha egy más <see cref="IConfigurationService"/> nem, az ablak
    /// memóriában tároló minta szerkesztővel (<see cref="FakeConfigurationEditor"/>) fut, és ezt a címsorban jelzi.
    /// </summary>
    public MainWindow(IConfigurationService? configurationService)
        : this(configurationService is null ? CreateStandaloneEditor() : ResolveEditor(configurationService))
    {
    }

    private MainWindow((IConfigurationEditor Editor, IDisposable? Owned) editor)
    {
        Editor = editor.Editor;
        _ownedEditor = editor.Owned;
        ViewModel = new ConfigurationWindowViewModel(Editor, new MessageBoxDialogs(this), new WpfMediaBrowser(this));
        InitializeComponent();
        DataContext = ViewModel;

        if (Editor is FakeConfigurationEditor)
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
    /// (view.md, "Elvetés és bezárás"). A döntés aszinkron (mentés), ezért az első bezárási kérést elutasítja,
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

    /// <summary>Önálló futás: saját szolgáltatás és léptető, amelyeket az ablak bezáráskor eldob.</summary>
    private static (IConfigurationEditor Editor, IDisposable? Owned) CreateStandaloneEditor()
    {
        var frameLoop = new FrameLoop();
        var service = new ConfigurationService(frameLoop);
        frameLoop.Start();

        // A mentett karakterméret és a be nem töltött források az előnézetekhez kellenek; a View a saját adatait külön tölti be.
        _ = service.InitializeAsync().ContinueWith(
            t => Trace.TraceError($"A konfiguráció betöltése nem sikerült: {t.Exception}"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
        return (service, new OwnedServices(service, frameLoop));
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

    /// <summary>Az önálló futás saját szolgáltatása és léptetője: előbb a szolgáltatás iratkozik le, utána áll le a léptető.</summary>
    private sealed class OwnedServices(ConfigurationService service, FrameLoop frameLoop) : IDisposable
    {
        public void Dispose()
        {
            service.Dispose();
            frameLoop.Dispose();
        }
    }
}
