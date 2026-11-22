namespace ConfigurationEngine.View.Tabs;

using System.Globalization;
using ConfigurationEngine.Model;
using ConfigurationEngine.Validation;

/// <summary>
/// A karakter mérete a „Karakter” fül tetején (gui.md, "1. Karakter mérete"): <c>CharacterWidth</c> és
/// <c>CharacterHeight</c> számmező, px. Érvényes érték beírásakor a méret azonnal érvényesül a szolgáltatáson
/// (a futó karakteren és az előnézeteken is), de csak Mentéskor tárolódik. Érvénytelen érték (üres, nem szám,
/// tartományon kívüli) nem kerül a szolgáltatásba: a mező hibát jelez, és az utolsó érvényes méret marad érvényben.
/// A fizikai paraméterek (<c>WalkSpeedPxPerSec</c>, <c>GravityPxPerSec2</c>, <c>FollowCursor</c>) nem jelennek meg.
/// </summary>
public sealed class CharacterSizeViewModel : EditableItemViewModel
{
    private readonly CharacterSettings _settings;

    public CharacterSizeViewModel(ConfigurationWindowViewModel owner, CharacterSettings settings)
        : base(owner)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Width = IntField(nameof(CharacterSettings.CharacterWidth), () => _settings.CharacterWidth, v => _settings.CharacterWidth = v);
        Height = IntField(nameof(CharacterSettings.CharacterHeight), () => _settings.CharacterHeight, v => _settings.CharacterHeight = v);
    }

    public override ConfigurationItemKind Kind => ConfigurationItemKind.CharacterSettings;

    public override string ItemKey => ItemKeys.CharacterSettings;

    public InputField Width { get; }

    public InputField Height { get; }

    /// <summary>Az érvényes tartomány a <see cref="ConfigurationLimits.CharacterSizePx"/>-ből, súgószövegnek.</summary>
    public static string RangeText => string.Create(
        CultureInfo.CurrentCulture,
        $"{ConfigurationLimits.CharacterSizePx.Min}–{ConfigurationLimits.CharacterSizePx.Max} px");

    protected override void OnFieldCommitted(string propertyName)
    {
        var range = ConfigurationLimits.CharacterSizePx;
        if (range.Contains(_settings.CharacterWidth) && range.Contains(_settings.CharacterHeight))
        {
            Owner.ApplyCharacterSize(_settings.CharacterWidth, _settings.CharacterHeight);
        }
    }
}
