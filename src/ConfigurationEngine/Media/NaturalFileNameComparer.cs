namespace ConfigurationEngine.Media;

using System.IO;

/// <summary>
/// Fájlnevek természetes sorrendje: a számjegysorozatokat számként hasonlítja (<c>frame2.png</c> &lt; <c>frame10.png</c>),
/// a többit kis- és nagybetűtől függetlenül. Csak a fájlnevet nézi, a mappát nem.
/// </summary>
internal sealed class NaturalFileNameComparer : IComparer<string>
{
    public static readonly NaturalFileNameComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var a = Path.GetFileName(x.AsSpan());
        var b = Path.GetFileName(y.AsSpan());
        int i = 0, j = 0;

        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                var numberA = TakeDigits(a, ref i).TrimStart('0');
                var numberB = TakeDigits(b, ref j).TrimStart('0');

                // A vezető nullák nélkül a hosszabb szám a nagyobb, azonos hossznál a számjegyek döntenek.
                var byNumber = numberA.Length != numberB.Length
                    ? numberA.Length.CompareTo(numberB.Length)
                    : numberA.SequenceCompareTo(numberB);
                if (byNumber != 0)
                {
                    return byNumber;
                }
            }
            else
            {
                var byChar = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                if (byChar != 0)
                {
                    return byChar;
                }

                i++;
                j++;
            }
        }

        var byLength = (a.Length - i).CompareTo(b.Length - j);
        return byLength != 0 ? byLength : string.CompareOrdinal(x, y);
    }

    private static ReadOnlySpan<char> TakeDigits(ReadOnlySpan<char> s, ref int index)
    {
        var start = index;
        while (index < s.Length && char.IsAsciiDigit(s[index]))
        {
            index++;
        }

        return s[start..index];
    }
}
