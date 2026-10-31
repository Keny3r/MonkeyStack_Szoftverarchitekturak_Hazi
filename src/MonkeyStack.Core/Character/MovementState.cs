namespace MonkeyStack.Core.Character;

/// <summary>
/// A karakter beépített fizikai mozgásállapota (alapállapot).
/// Független a külső környezeti rendszereseményektől.
/// </summary>
public enum MovementState
{
    Idle,
    Walking,
    Falling,
    Dragging
}
