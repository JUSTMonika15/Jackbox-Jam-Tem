using System;

public sealed class MusicPlaylistRules
{
    readonly int trackCount;
    int currentIndex;

    public MusicPlaylistRules(int trackCount)
    {
        if (trackCount < 0) throw new ArgumentOutOfRangeException(nameof(trackCount));
        this.trackCount = trackCount;
        currentIndex = trackCount == 0 ? -1 : 0;
    }

    public int CurrentIndex => currentIndex;

    public int Advance()
    {
        if (trackCount == 0) return -1;
        currentIndex = (currentIndex + 1) % trackCount;
        return currentIndex;
    }
}
