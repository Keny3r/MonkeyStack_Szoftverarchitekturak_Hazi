using System.Windows;
using MonkeyStack.Core.Configuration;

namespace ConfigurationEngine;

/// <summary>
/// A Konfigurációs motor View ablaka.
/// Indítható önállóan (standalone F5 teszt módban saját ConfigurationService példánnyal),
/// vagy integráltan a közös memóriában élő IConfigurationService átadásával.
/// </summary>
public partial class MainWindow : Window
{
    public IConfigurationService ConfigurationService { get; }

    public MainWindow()
        : this(null)
    {
    }

    public MainWindow(IConfigurationService? configurationService)
    {
        ConfigurationService = configurationService ?? new ConfigurationService();
        InitializeComponent();
    }
}
