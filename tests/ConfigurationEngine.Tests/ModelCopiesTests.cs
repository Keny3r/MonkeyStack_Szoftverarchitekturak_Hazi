namespace ConfigurationEngine.Tests;

using System.Reflection;
using ConfigurationEngine.Model;
using ConfigurationEngine.Storage;

/// <summary>
/// A kézzel írt <see cref="ModelCopies"/> másolók minden mezőt visznek-e: egy új modellmező, amit a másoló kihagy,
/// mentéskor csendben az alapértékére állna vissza.
/// </summary>
public class ModelCopiesTests
{
    public static TheoryData<Type> ModelTypes =>
    [
        typeof(AnimationSource),
        typeof(MovementAnimation),
        typeof(BehaviorRule),
        typeof(CharacterSettings),
        typeof(MetricPolling)
    ];

    [Theory]
    [MemberData(nameof(ModelTypes))]
    public void Copy_CopiesEveryProperty(Type type)
    {
        var original = Activator.CreateInstance(type)!;
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite).ToList();
        foreach (var property in properties)
        {
            // Az új példány értékétől eltérő érték, így a kihagyott mező a másolatban biztosan különbözik.
            property.SetValue(original, DifferentValue(property, property.GetValue(original)));
        }

        var copyMethod = typeof(ModelCopies).GetMethod(nameof(ModelCopies.Copy), [type])
            ?? throw new InvalidOperationException($"Nincs ModelCopies.Copy({type.Name}).");
        var copy = copyMethod.Invoke(null, [original])!;

        Assert.NotSame(original, copy);
        foreach (var property in properties)
        {
            Assert.True(
                Equals(property.GetValue(original), property.GetValue(copy)),
                $"A ModelCopies.Copy nem másolja a(z) {type.Name}.{property.Name} mezőt.");
        }
    }

    [Fact]
    public void ConfigurationCopy_IsDeep()
    {
        var data = TestData.ValidConfiguration();

        var copy = data.Copy();

        Assert.NotSame(data.AnimationSources[0], copy.AnimationSources[0]);
        Assert.NotSame(data.MovementAnimations[0], copy.MovementAnimations[0]);
        Assert.NotSame(data.BehaviorRules[0], copy.BehaviorRules[0]);
        Assert.NotSame(data.Character, copy.Character);
        Assert.NotSame(data.MetricPollings[0], copy.MetricPollings[0]);
        Assert.Equal(data.AnimationSources.Count, copy.AnimationSources.Count);
        Assert.Equal(data.MovementAnimations.Count, copy.MovementAnimations.Count);
        Assert.Equal(data.BehaviorRules.Count, copy.BehaviorRules.Count);
        Assert.Equal(data.MetricPollings.Count, copy.MetricPollings.Count);
    }

    private static object DifferentValue(PropertyInfo property, object? current)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (type.IsEnum)
        {
            return Enum.GetValues(type).Cast<object>().First(v => !v.Equals(current));
        }

        return type switch
        {
            _ when type == typeof(int) => current is int i ? i + 7 : 7,
            _ when type == typeof(double) => current is double d ? d + 1.5 : 1.5,
            _ when type == typeof(bool) => current is not true,
            _ when type == typeof(string) => current is string s ? s + "x" : "x",
            _ => throw new NotSupportedException(
                $"{property.DeclaringType!.Name}.{property.Name}: új mezőtípus ({type.Name}), bővítsd a tesztet.")
        };
    }
}
