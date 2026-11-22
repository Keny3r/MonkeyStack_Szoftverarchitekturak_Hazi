namespace ConfigurationEngine.View;

using System.Globalization;
using ConfigurationEngine.Model;
using MonkeyStack.Core.Animation;

/// <summary>
/// Egy sor élő előnézete (gui.md, "Előnézeti animációk"): a szerkesztett forráshoz és sebességhez tartozó előnézeti
/// animáció. Az animációt a motor készíti és lépteti; a View csak rajzolja, és nem módosítja. Ha a forrás vagy a
/// sebesség változik, új előnézetet kér, a régit pedig egyenként eldobja (<c>ReleasePreviewAnimation</c>).
/// </summary>
public sealed class PreviewViewModel : ObservableObject
{
    private readonly PreviewManager _manager;
    private string? _key;
    private IAnimation? _animation;
    private bool _isLoading;
    private string? _errorMessage;
    private CancellationTokenSource? _pending;
    private bool _isClosed;

    internal PreviewViewModel(PreviewManager manager)
    {
        _manager = manager;
    }

    /// <summary>A kirajzolandó előnézeti animáció; <c>null</c>, amíg töltődik, vagy ha nincs forrás vagy hibás.</summary>
    public IAnimation? Animation
    {
        get => _animation;
        private set => SetProperty(ref _animation, value);
    }

    /// <summary>A betöltés a háttérben fut; a sor „betöltés…” állapotot mutat.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>A forrás nem tölthető be; az előnézet helyén ez az üzenet jelenik meg.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => ErrorMessage is not null;

    /// <summary>A betöltést meghatározó kulcs: a forrás betöltési mezői és a sebesség (az azonosító és a név nem).</summary>
    internal string? Key => _key;

    /// <summary>
    /// Az előnézet a megadott forrást és sebességet mutassa. Ha ezek nem változtak, nem történik semmi; különben a régi
    /// előnézet eldobódik, és új készül. <c>null</c> forrásnál (pl. új szabály, még nincs kiválasztva) nincs előnézet.
    /// </summary>
    public void Show(AnimationSource? source, double framesPerSecond)
    {
        if (_isClosed)
        {
            return;
        }

        var key = source is null ? null : KeyOf(source, framesPerSecond);
        if (key == _key)
        {
            return;
        }

        _key = key;
        CancelPending();
        ReleaseAnimation();
        ErrorMessage = null;
        if (source is null || key is null)
        {
            return;
        }

        // Mentés után a régi sor ugyanilyen előnézete átvehető (gui.md, "Élettartam": mentés után megmaradnak).
        if (_manager.TakeKept(key) is { } kept)
        {
            Animation = kept;
            return;
        }

        _ = LoadAsync(Snapshot(source), framesPerSecond, key);
    }

    /// <summary>A sor törlésekor: eldobja az előnézetet, és nem kér újat.</summary>
    public void Release()
    {
        CancelPending();
        ReleaseAnimation();
        _isClosed = true;
        _manager.Remove(this);
    }

    /// <summary>
    /// Az összes előnézet eldobása után (Elvetés, bezárás): az animációt már a motor eldobta, ezért a View sem
    /// rajzolja tovább; a még folyamatban lévő kérés eredményét eldobja.
    /// </summary>
    internal void Abandon()
    {
        CancelPending();
        Animation = null;
        _key = null;
        _isClosed = true;
    }

    /// <summary>Az animáció átadása egy új sornak mentés után; ez a példány ezután már nem használja.</summary>
    internal IAnimation? Surrender()
    {
        var animation = _animation;
        _animation = null;
        _key = null;
        _isClosed = true;
        CancelPending();
        return animation;
    }

    internal static string KeyOf(AnimationSource source, double framesPerSecond) =>
        string.Join(
            '|',
            source.SourceType,
            source.Path,
            source.FrameWidth,
            source.FrameHeight,
            source.Columns,
            source.Rows,
            source.FrameCount,
            framesPerSecond.ToString("R", CultureInfo.InvariantCulture));

    /// <summary>A forrás pillanatképe: a betöltés alatt a felhasználó tovább szerkesztheti az eredetit.</summary>
    internal static AnimationSource Snapshot(AnimationSource source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        SourceType = source.SourceType,
        Path = source.Path,
        FrameWidth = source.FrameWidth,
        FrameHeight = source.FrameHeight,
        Columns = source.Columns,
        Rows = source.Rows,
        FrameCount = source.FrameCount
    };

    private async Task LoadAsync(AnimationSource source, double framesPerSecond, string key)
    {
        var pending = new CancellationTokenSource();
        _pending = pending;
        IsLoading = true;
        try
        {
            var animation = await _manager.CreateAnimationAsync(source, framesPerSecond, pending.Token);
            if (_isClosed || pending.IsCancellationRequested || _key != key)
            {
                // A kérés közben elavult: a késve érkezett előnézetet a motornak is el kell dobnia.
                _manager.ReleaseAnimation(animation);
                return;
            }

            Animation = animation;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            // MediaLoadException: a felhasználónak szóló, magyar üzenet; más hiba sem állíthatja meg az ablakot.
            if (!pending.IsCancellationRequested && _key == key)
            {
                ErrorMessage = ex.Message;
            }
        }
        finally
        {
            if (ReferenceEquals(_pending, pending))
            {
                _pending = null;
                IsLoading = false;
            }

            pending.Dispose();
        }
    }

    private void CancelPending()
    {
        if (_pending is { } pending)
        {
            _pending = null;
            pending.Cancel();
            IsLoading = false;
        }
    }

    private void ReleaseAnimation()
    {
        if (_animation is { } animation)
        {
            Animation = null;
            _manager.ReleaseAnimation(animation);
        }
    }
}
