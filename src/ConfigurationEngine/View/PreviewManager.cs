namespace ConfigurationEngine.View;

using ConfigurationEngine.Model;
using MonkeyStack.Core.Animation;

/// <summary>
/// A konfigurációs ablak előnézeteinek élettartama (view.md, "Élettartam"):
/// <list type="bullet">
/// <item>sebesség- vagy forrásváltáskor és sor törlésekor egyenként (<see cref="PreviewViewModel"/>);</item>
/// <item>Elvetéskor és bezáráskor az összes egyszerre (<see cref="ReleaseAll"/>);</item>
/// <item>mentés után megmaradnak: az újraépített sorok átveszik a régi sorok azonos előnézeteit (<see cref="KeepDuring"/>).</item>
/// </list>
/// Az előnézeti animációkat a motor készíti és lépteti; a View csak rajzolja őket.
/// </summary>
public sealed class PreviewManager
{
    private readonly IConfigurationEditor _editor;
    private readonly List<PreviewViewModel> _active = [];
    private List<PreviewViewModel>? _kept;

    public PreviewManager(IConfigurationEditor editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    /// <summary>Egy új sor előnézete (még üres; a sor a <see cref="PreviewViewModel.Show"/>-val tölti meg).</summary>
    public PreviewViewModel Create()
    {
        var preview = new PreviewViewModel(this);
        _active.Add(preview);
        return preview;
    }

    /// <summary>
    /// Mentés utáni újraépítés: a <paramref name="rebuild"/> alatt létrejövő sorok átveszik a régi sorok azonos
    /// (forrás és sebesség szerint egyező) előnézeteit; a végén megmaradt régi előnézetek eldobódnak.
    /// </summary>
    public void KeepDuring(Action rebuild)
    {
        _kept = [.. _active];
        _active.Clear();
        try
        {
            rebuild();
        }
        finally
        {
            var unused = _kept;
            _kept = null;
            foreach (var preview in unused)
            {
                preview.Release();
            }
        }
    }

    /// <summary>Az összes előnézet eldobása (Elvetés, az ablak bezárása).</summary>
    public void ReleaseAll()
    {
        foreach (var preview in _active)
        {
            preview.Abandon();
        }

        _active.Clear();
        _kept?.Clear();
        _editor.ReleasePreviewAnimations();
    }

    internal Task<IAnimation> CreateAnimationAsync(AnimationSource source, double framesPerSecond, CancellationToken cancellationToken) =>
        _editor.CreatePreviewAnimationAsync(source, framesPerSecond, cancellationToken);

    internal void ReleaseAnimation(IAnimation animation) => _editor.ReleasePreviewAnimation(animation);

    internal void Remove(PreviewViewModel preview)
    {
        _active.Remove(preview);
        _kept?.Remove(preview);
    }

    /// <summary>Mentés utáni újraépítés közben: egy régi sor azonos kulcsú, már betöltött előnézete.</summary>
    internal IAnimation? TakeKept(string key)
    {
        var match = _kept?.FirstOrDefault(p => p.Key == key && p.Animation is not null);
        if (match is null)
        {
            return null;
        }

        _kept!.Remove(match);
        return match.Surrender();
    }
}
