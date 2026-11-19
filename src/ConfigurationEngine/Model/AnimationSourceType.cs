namespace ConfigurationEngine.Model;

/// <summary>
/// Az animációs forrás típusa (lásd Szótár: Animációs forrás).
/// </summary>
public enum AnimationSourceType
{
    /// <summary>Animált GIF fájl.</summary>
    Gif,

    /// <summary>Egyetlen képfájl, amely rácsban tartalmazza a képkockákat.</summary>
    SpriteSheet,

    /// <summary>Mappa, amelynek képfájljai természetes sorrendben alkotják a képkockákat.</summary>
    ImageFolder
}
