public static class AuthorityGate
{
    public static bool IsAuthoritative(bool isSpawned, bool isServer)
        => !isSpawned || isServer;
}

public sealed class SnapshotRevision
{
    private int accepted = -1;

    public int Accepted => accepted;

    public bool TryAccept(int incoming)
    {
        if (incoming <= accepted) return false;
        accepted = incoming;
        return true;
    }

    public void Reset() => accepted = -1;
}

public static class RequestValidation
{
    public static bool CanBuy(bool playing, bool canAct, bool inZone, bool unowned,
        bool holdReady, int money, int price)
        => playing && canAct && inZone && unowned && holdReady
            && price >= 0 && money >= price;

    public static bool WithinRange(float distanceSquared, float maxDistance)
        => !float.IsNaN(distanceSquared) && !float.IsInfinity(distanceSquared)
            && !float.IsNaN(maxDistance) && !float.IsInfinity(maxDistance)
            && distanceSquared >= 0f && maxDistance >= 0f
            && distanceSquared <= maxDistance * maxDistance;
}

public static class BoardContactRules
{
    public static bool IsPointWithin(float pointX, float pointZ, float minX, float maxX,
        float minZ, float maxZ)
    {
        if (float.IsNaN(pointX) || float.IsInfinity(pointX)
            || float.IsNaN(pointZ) || float.IsInfinity(pointZ)) return false;
        return pointX >= System.Math.Min(minX, maxX) && pointX <= System.Math.Max(minX, maxX)
            && pointZ >= System.Math.Min(minZ, maxZ) && pointZ <= System.Math.Max(minZ, maxZ);
    }

    public static bool IsStandingOnSurface(float feetY, float surfaceBottomY,
        float surfaceTopY, float tolerance)
    {
        if (float.IsNaN(feetY) || float.IsInfinity(feetY)
            || float.IsNaN(surfaceBottomY) || float.IsInfinity(surfaceBottomY)
            || float.IsNaN(surfaceTopY) || float.IsInfinity(surfaceTopY)
            || float.IsNaN(tolerance) || float.IsInfinity(tolerance)) return false;

        float safeTolerance = System.Math.Max(0f, tolerance);
        float bottom = System.Math.Min(surfaceBottomY, surfaceTopY);
        float top = System.Math.Max(surfaceBottomY, surfaceTopY);
        return feetY >= bottom - safeTolerance && feetY <= top + safeTolerance;
    }
}

public static class NetworkProtocolRules
{
    public const int CurrentVersion = 4;
    public static bool IsCompatible(int expected, int received) => expected == received;
}

public static class NetworkObjectKeyRules
{
    public static string Segment(string name, int sameNameOrdinal)
        => (name ?? string.Empty).Replace("/", "_") + "#" + System.Math.Max(0, sameNameOrdinal);
}
