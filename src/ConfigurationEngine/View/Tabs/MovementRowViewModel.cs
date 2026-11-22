namespace ConfigurationEngine.View.Tabs;

using System.Globalization;
using ConfigurationEngine.Model;
using ConfigurationEngine.Validation;
using MonkeyStack.Core.Character;

/// <summary>
/// Egy mozgásállapot fix sora a „Karakter” fülön (gui.md, "2. Mozgási animációk"): állapot (felirat), forrás
/// (legördülő lista a szerkesztett másolat forrásaival), sebesség (fps) és élő előnézet. Sort hozzáadni vagy törölni
/// nem lehet, és „Nincs animáció” lehetőség sincs: a sorban mindig van kiválasztott forrás.
/// </summary>
public sealed class MovementRowViewModel : EditableItemViewModel
{
    private double _previewFramesPerSecond;

    public MovementRowViewModel(ConfigurationWindowViewModel owner, MovementAnimation movement)
        : base(owner)
    {
        Movement = movement ?? throw new ArgumentNullException(nameof(movement));
        FramesPerSecond = DoubleField(
            nameof(MovementAnimation.FramesPerSecond),
            () => Movement.FramesPerSecond,
            v => Movement.FramesPerSecond = v);
        Preview = owner.Previews.Create();
        _previewFramesPerSecond = ConfigurationLimits.FramesPerSecond.Contains(movement.FramesPerSecond)
            ? movement.FramesPerSecond
            : new MovementAnimation().FramesPerSecond;
    }

    public MovementAnimation Movement { get; }

    public MovementState State => Movement.MovementState;

    /// <summary>Az állapot felirata (nem szerkeszthető).</summary>
    public string StateName => State.ToString();

    public override ConfigurationItemKind Kind => ConfigurationItemKind.MovementAnimation;

    public override string ItemKey => ItemKeys.For(State);

    /// <summary>
    /// A kiválasztott forrás azonosítója (új forrásnál a negatív ideiglenes azonosító). A legördülő lista üres
    /// kijelölése (<c>null</c>) nem írja felül: a sorban mindig van forrás.
    /// </summary>
    public int? AnimationSourceId
    {
        get => Movement.AnimationSourceId;
        set
        {
            if (value is not { } id || id == Movement.AnimationSourceId)
            {
                return;
            }

            Movement.AnimationSourceId = id;
            OnPropertyChanged();
            Edited();
        }
    }

    /// <summary>Lejátszási sebesség, fps.</summary>
    public InputField FramesPerSecond { get; }

    /// <summary>Az érvényes tartomány a <see cref="ConfigurationLimits.FramesPerSecond"/>-ből, súgószövegnek.</summary>
    public static string FramesPerSecondRangeText => string.Create(
        CultureInfo.CurrentCulture,
        $"{ConfigurationLimits.FramesPerSecond.Min:0.##}–{ConfigurationLimits.FramesPerSecond.Max:0.##} fps");

    public PreviewViewModel Preview { get; }

    /// <summary>
    /// Az előnézet a szerkesztett forrást és sebességet mutassa. Tartományon kívüli sebességnél az utolsó érvényes
    /// sebességgel marad (a mező hibát jelez).
    /// </summary>
    public void RefreshPreview()
    {
        if (ConfigurationLimits.FramesPerSecond.Contains(Movement.FramesPerSecond))
        {
            _previewFramesPerSecond = Movement.FramesPerSecond;
        }

        Preview.Show(Owner.FindSource(Movement.AnimationSourceId), _previewFramesPerSecond);
    }
}
