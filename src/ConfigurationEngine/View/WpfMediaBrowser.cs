namespace ConfigurationEngine.View;

using System.Windows;
using Microsoft.Win32;

/// <summary>
/// Az <see cref="IMediaBrowser"/> megvalósítása a WPF <see cref="OpenFileDialog"/> és <see cref="OpenFolderDialog"/>
/// párbeszédablakaival, a konfigurációs ablakhoz kötve.
/// </summary>
internal sealed class WpfMediaBrowser : IMediaBrowser
{
    private readonly Window _owner;

    public WpfMediaBrowser(Window owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public string? PickFile(IReadOnlyCollection<string> extensions)
    {
        var patterns = string.Join(";", extensions.Select(e => "*" + e));
        var dialog = new OpenFileDialog
        {
            Title = "Animációs forrás tallózása",
            Filter = $"Animáció ({patterns})|{patterns}",
            CheckFileExists = true,
            Multiselect = false
        };
        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }

    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Képsorozat-mappa tallózása",
            Multiselect = false
        };
        return dialog.ShowDialog(_owner) == true ? dialog.FolderName : null;
    }
}
