namespace ConfigurationEngine.View.Tabs;

using System.Windows;
using System.Windows.Controls;

/// <summary>
/// A „Szabályok” fül nézete (gui.md, "3. Viselkedési szabályok (rétegek)"); az adatkörnyezete egy
/// <see cref="RulesTabViewModel"/>.
/// </summary>
public partial class RulesTabView : UserControl
{
    public RulesTabView()
    {
        InitializeComponent();
    }

    /// <summary>A lista a <c>ZIndex</c> mező elhagyásakor rendeződik újra, nem gépelés közben (gui.md, "Rétegek listája").</summary>
    private void OnZIndexLostFocus(object sender, RoutedEventArgs e) => (DataContext as RulesTabViewModel)?.Resort();
}
