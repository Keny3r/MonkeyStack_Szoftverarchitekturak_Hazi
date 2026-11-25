namespace ConfigurationEngine.Media;

/// <summary>
/// Egy kép mérete pixelben (pl. egy sprite sheet tényleges mérete a rácsból számolt mellett).
/// </summary>
/// <param name="Width">Szélesség pixelben.</param>
/// <param name="Height">Magasság pixelben.</param>
public readonly record struct PixelSize(int Width, int Height);
