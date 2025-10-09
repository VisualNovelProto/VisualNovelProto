using System;

public static class StoryFlags
{
    static Func<int, bool> _has;

    // DialogueRunner에서 바인딩: StoryFlags.Bind(id => flags.Has(id));
    public static void Bind(Func<int, bool> hasProvider) { _has = hasProvider; }

    public static bool Has(int id) => _has != null && _has(id);
}
