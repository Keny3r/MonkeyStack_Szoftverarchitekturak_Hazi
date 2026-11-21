namespace ConfigurationEngine.Validation;

using System.Globalization;
using ConfigurationEngine.Model;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

/// <summary>
/// A <see cref="ValidationError.ItemKey"/> értékek előállítása, hogy a validátor és a tároló ugyanazt a kulcsot használja.
/// </summary>
internal static class ItemKeys
{
    public const string CharacterSettings = "1";

    public static string ForId(int id, int index) =>
        id != 0 ? id.ToString(CultureInfo.InvariantCulture) : ForIndex(index);

    /// <summary>A listabeli index alapú kulcs (<c>"#index"</c>): azonosító nélküli vagy ismétlődő azonosítójú elemnél.</summary>
    public static string ForIndex(int index) => $"#{index.ToString(CultureInfo.InvariantCulture)}";

    public static string For(AnimationSource source, int index) => ForId(source.Id, index);

    public static string For(BehaviorRule rule, int index) => ForId(rule.Id, index);

    public static string For(MovementState state) => state.ToString();

    public static string For(Metric metric) => metric.ToString();
}
