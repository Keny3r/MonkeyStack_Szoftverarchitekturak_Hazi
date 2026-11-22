namespace ConfigurationEngine.View;

using System.Windows;

/// <summary>
/// Az <see cref="IUserDialogs"/> megvalósítása WPF <see cref="MessageBox"/>-szal, a konfigurációs ablakhoz kötve.
/// </summary>
internal sealed class MessageBoxDialogs : IUserDialogs
{
    private const string Caption = "Konfiguráció";

    private readonly Window _owner;

    public MessageBoxDialogs(Window owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public bool Confirm(string title, string message) =>
        MessageBox.Show(_owner, message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning)
            == MessageBoxResult.OK;

    public CloseDecision AskUnsavedChanges() =>
        MessageBox.Show(
            _owner,
            "Mentetlen módosítások vannak. Mented őket bezárás előtt?\n\n"
                + "Igen: mentés és bezárás\nNem: a módosítások elvetése és bezárás\nMégse: az ablak nyitva marad",
            Caption,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question) switch
        {
            MessageBoxResult.Yes => CloseDecision.Save,
            MessageBoxResult.No => CloseDecision.Discard,
            _ => CloseDecision.Cancel
        };
}
