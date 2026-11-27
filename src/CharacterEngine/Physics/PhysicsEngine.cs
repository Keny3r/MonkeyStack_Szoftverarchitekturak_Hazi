namespace CharacterEngine.Physics;

using MonkeyStack.Core.Character;

/// <summary>
/// A karakter belső fizikai motorja: gravitáció, landolás az ablakperemekre, alátámasztás- és találatvizsgálat.
/// Nincs saját állapota, a paraméterei pedig megváltoztathatatlanok (<see cref="PhysicsSettings"/>), ezért zár nélkül,
/// bármelyik szálról hívható.
/// </summary>
public class PhysicsEngine : IPhysicsEngine
{
    public PhysicsEngine(PhysicsSettings? settings = null)
    {
        Settings = settings ?? PhysicsSettings.Default;
    }

    public PhysicsSettings Settings { get; }

    public (double Y, double VelocityY) ApplyGravity(double y, double velocityY, TimeSpan deltaTime)
    {
        if (deltaTime <= TimeSpan.Zero)
        {
            return (y, velocityY);
        }

        double dt = deltaTime.TotalSeconds;

        // Félig implicit Euler: előbb a sebesség, utána a pozíció lép, az új sebességgel.
        double newVelocity = Math.Min(velocityY + (Settings.GravityPxPerSec2 * dt), Settings.MaxFallSpeedPxPerSec);
        return (y + (newVelocity * dt), newVelocity);
    }

    public PlatformSegment? FindLandingPlatform(
        double x,
        double previousY,
        double newY,
        IReadOnlyList<PlatformSegment> platforms)
    {
        ArgumentNullException.ThrowIfNull(platforms);

        if (newY < previousY)
        {
            return null;
        }

        PlatformSegment? best = null;
        foreach (var platform in platforms)
        {
            bool crossed = platform.Y >= previousY && platform.Y <= newY;
            if (crossed && platform.ContainsX(x) && (best is null || platform.Y < best.Value.Y))
            {
                best = platform;
            }
        }

        return best;
    }

    public PlatformSegment? FindSupportingPlatform(double x, double y, IReadOnlyList<PlatformSegment> platforms)
    {
        ArgumentNullException.ThrowIfNull(platforms);

        PlatformSegment? best = null;
        double bestDistance = double.MaxValue;
        foreach (var platform in platforms)
        {
            double distance = Math.Abs(platform.Y - y);

            // Ráhagyás nélküli X-vizsgálat: a szél közelében landolt karakter se essen le azonnal.
            // A szélső ráhagyás (PlatformEdgeMargin) csak a séta megfordulásánál számít.
            if (distance <= Settings.SupportTolerancePx && platform.ContainsX(x) && distance < bestDistance)
            {
                best = platform;
                bestDistance = distance;
            }
        }

        return best;
    }

    public PlatformSegment? FindFallbackGround(double x, double y, IReadOnlyList<PlatformSegment> platforms)
    {
        ArgumentNullException.ThrowIfNull(platforms);

        PlatformSegment? lowestAtX = null;
        PlatformSegment? nearest = null;
        double nearestDistance = double.MaxValue;

        foreach (var platform in platforms)
        {
            if (platform.ContainsX(x))
            {
                if (platform.Y >= y)
                {
                    return null; // van alatta platform: rendes esés, nincs mit menteni
                }

                if (lowestAtX is null || platform.Y > lowestAtX.Value.Y)
                {
                    lowestAtX = platform;
                }

                continue;
            }

            double distance = x < platform.LeftX ? platform.LeftX - x : x - platform.RightX;
            if (distance < nearestDistance || (distance == nearestDistance && platform.Y > nearest!.Value.Y))
            {
                nearest = platform;
                nearestDistance = distance;
            }
        }

        return lowestAtX ?? nearest;
    }

    public bool HitTest(double x, double y, int width, int height, double cursorX, double cursorY)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        double halfWidth = width / 2.0;
        return cursorX >= x - halfWidth
            && cursorX <= x + halfWidth
            && cursorY >= y - height
            && cursorY <= y;
    }
}
