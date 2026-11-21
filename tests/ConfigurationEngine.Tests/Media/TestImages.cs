namespace ConfigurationEngine.Tests.Media;

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>
/// Tesztképek előállítása: egyszínű PNG, színes rácsú sprite sheet és kézzel összerakott GIF
/// (a WPF GIF kódolója nem ír képkocka-pozíciót és eltávolítási módot, ezért a GIF bájtjait mi írjuk).
/// </summary>
internal static class TestImages
{
    public static readonly Color Red = Color.FromRgb(255, 0, 0);
    public static readonly Color Green = Color.FromRgb(0, 255, 0);
    public static readonly Color Blue = Color.FromRgb(0, 0, 255);

    public static void WritePng(string path, int width, int height, Color color) =>
        WritePng(path, width, height, (_, _) => color);

    public static void WritePng(string path, int width, int height, Func<int, int, Color> colorAt)
    {
        var pixels = new int[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var c = colorAt(x, y);
                pixels[y * width + x] = (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;
            }
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>A képkocka egy pixelének színe (ARGB, nem premultiplikált).</summary>
    public static Color PixelAt(BitmapSource frame, int x, int y)
    {
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var pixel = new byte[4];
        converted.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }

    /// <summary>Egy GIF képkocka: a helye a logikai képernyőn, a palettaindexei és az eltávolítási módja.</summary>
    public sealed record GifFrame(int Left, int Top, int Width, int Height, byte[] Indices, int Disposal);

    /// <summary>
    /// GIF89a fájl a 4 színű palettával: 0 = átlátszó, 1 = piros, 2 = zöld, 3 = kék.
    /// </summary>
    public static void WriteGif(string path, int width, int height, params GifFrame[] frames)
    {
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream))
        {
            w.Write("GIF89a"u8);
            w.Write((ushort)width);
            w.Write((ushort)height);
            w.Write((byte)0b1000_0001); // globális paletta, 2^(1+1) = 4 szín
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write(new byte[] { 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255 });

            foreach (var frame in frames)
            {
                // Graphic Control Extension: eltávolítási mód, átlátszó index = 0.
                w.Write(new byte[] { 0x21, 0xF9, 4, (byte)((frame.Disposal << 2) | 1), 10, 0, 0, 0 });

                // Image Descriptor.
                w.Write((byte)0x2C);
                w.Write((ushort)frame.Left);
                w.Write((ushort)frame.Top);
                w.Write((ushort)frame.Width);
                w.Write((ushort)frame.Height);
                w.Write((byte)0);

                WriteLzw(w, frame.Indices);
            }

            w.Write((byte)0x3B);
        }

        File.WriteAllBytes(path, stream.ToArray());
    }

    /// <summary>
    /// Tömörítetlen LZW: minden pixel előtt törlőkód, így a kódméret végig 3 bit marad.
    /// </summary>
    private static void WriteLzw(BinaryWriter w, byte[] indices)
    {
        const int minCodeSize = 2, clear = 4, end = 5, bits = 3;
        var data = new List<byte>();
        int buffer = 0, count = 0;

        void Emit(int code)
        {
            buffer |= code << count;
            count += bits;
            while (count >= 8)
            {
                data.Add((byte)buffer);
                buffer >>= 8;
                count -= 8;
            }
        }

        foreach (var index in indices)
        {
            Emit(clear);
            Emit(index);
        }

        Emit(end);
        if (count > 0)
        {
            data.Add((byte)buffer);
        }

        w.Write((byte)minCodeSize);
        foreach (var block in data.Chunk(255))
        {
            w.Write((byte)block.Length);
            w.Write(block);
        }

        w.Write((byte)0);
    }
}
