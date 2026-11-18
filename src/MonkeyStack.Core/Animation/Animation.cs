namespace MonkeyStack.Core.Animation;

using System.Windows.Media.Imaging;

/// <summary>
/// Konkrét, szálbiztos (Monitor Object / Scoped Locking minta) animációs modell osztály.
/// A Konfigurációs motor példányosítja a betöltött és fix méretűre skálázott képkockákkal,
/// és folyamatosan pörgeti az Update metódussal. Pozícióval nem rendelkezik.
/// </summary>
public class Animation : IAnimation
{
    private readonly object _syncRoot = new();
    private IReadOnlyList<BitmapSource> _frames;
    private int _width;
    private int _height;
    private int _currentFrameIndex;
    private double _framesPerSecond;
    private int _zIndex;
    private double _elapsedSeconds;

    public Animation(
        IReadOnlyList<BitmapSource>? frames = null,
        int width = 100,
        int height = 100,
        double framesPerSecond = 10.0,
        int zIndex = 0)
    {
        _frames = frames ?? Array.Empty<BitmapSource>();
        _width = width > 0 ? width : 100;
        _height = height > 0 ? height : 100;
        _framesPerSecond = framesPerSecond > 0 ? framesPerSecond : 10.0;
        _zIndex = zIndex;

        FreezeFrames(_frames);
    }

    public int GetWidth()
    {
        lock (_syncRoot)
        {
            return _width;
        }
    }

    public int GetHeight()
    {
        lock (_syncRoot)
        {
            return _height;
        }
    }

    public BitmapSource? CurrentFrame
    {
        get
        {
            lock (_syncRoot)
            {
                if (_frames.Count == 0)
                {
                    return null;
                }

                return _frames[_currentFrameIndex];
            }
        }
    }

    public int CurrentFrameIndex
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentFrameIndex;
            }
        }
    }

    public int FrameCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _frames.Count;
            }
        }
    }

    public double FramesPerSecond
    {
        get
        {
            lock (_syncRoot)
            {
                return _framesPerSecond;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _framesPerSecond = value > 0 ? value : 1.0;
            }
        }
    }

    public int ZIndex
    {
        get
        {
            lock (_syncRoot)
            {
                return _zIndex;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _zIndex = value;
            }
        }
    }

    /// <summary>
    /// Lecseréli az animáció képkockáit és/vagy a skálázott méretét (pl. amikor a Config menüben
    /// új animációt töltenek fel vagy átállítják a globális karakter-képméretet).
    /// </summary>
    public void SetFrames(IReadOnlyList<BitmapSource> frames, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(frames);
        FreezeFrames(frames);

        lock (_syncRoot)
        {
            _frames = frames;
            _width = width > 0 ? width : _width;
            _height = height > 0 ? height : _height;
            _currentFrameIndex = 0;
            _elapsedSeconds = 0;
        }
    }

    /// <summary>
    /// Lépteti a folyamatosan loopoló animációt az eltelt idő alapján.
    /// </summary>
    public void Update(TimeSpan deltaTime)
    {
        if (deltaTime <= TimeSpan.Zero)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_frames.Count <= 1 || _framesPerSecond <= 0)
            {
                return;
            }

            _elapsedSeconds += deltaTime.TotalSeconds;
            double frameDuration = 1.0 / _framesPerSecond;

            if (_elapsedSeconds >= frameDuration)
            {
                int steps = (int)(_elapsedSeconds / frameDuration);
                _elapsedSeconds -= steps * frameDuration;
                _currentFrameIndex = (_currentFrameIndex + steps) % _frames.Count;
            }
        }
    }

    private static void FreezeFrames(IReadOnlyList<BitmapSource> frames)
    {
        foreach (var frame in frames)
        {
            if (frame is { CanFreeze: true, IsFrozen: false })
            {
                frame.Freeze();
            }
        }
    }
}
