using System.Collections.Generic;

public static class GlobalEffectHub
{
    static int _nextChainID = 1;
    public static int NewChainID() => _nextChainID++;

    // „Další“ uzly (počítají se proti maxExtraLinks)
    private static readonly Dictionary<int, HashSet<EffectCollector>> _linked  = new();
    // Všichni členové (včetně prvního/root)
    private static readonly Dictionary<int, HashSet<EffectCollector>> _members = new();

    // ==== GLOBAL STACKY PRO CHAIN ====
    private static readonly Dictionary<int, int> _chainStacks    = new(); // chainID -> current stacks
    private static readonly Dictionary<int, int> _chainMaxStacks = new(); // chainID -> max

    public static int Count(int chainID)
        => _linked.TryGetValue(chainID, out var set) ? set.Count : 0;

    public static bool Contains(int chainID, EffectCollector c)
        => _linked.TryGetValue(chainID, out var set) && set.Contains(c);

    public static bool IsMember(int chainID, EffectCollector c)
        => _members.TryGetValue(chainID, out var set) && set.Contains(c);

    /// Vrátí chainID, ve kterém už JE tento cíl (true), jinak false.
    public static bool TryGetExistingChain(EffectCollector c, out int chainID)
    {
        foreach (var kv in _members)
        {
            if (kv.Value.Contains(c)) { chainID = kv.Key; return true; }
        }
        chainID = 0;
        return false;
    }

    static void EnsureStacks(int chainID, int maxStacks)
    {
        if (!_chainStacks.ContainsKey(chainID)) _chainStacks[chainID] = 0;
        _chainMaxStacks[chainID] = maxStacks;
    }

    public static int GetStacks(int chainID)
        => _chainStacks.TryGetValue(chainID, out var s) ? s : 0;

    public static int TryIncrementStacks(int chainID, int maxStacks)
    {
        EnsureStacks(chainID, maxStacks);
        int cur = _chainStacks[chainID];
        if (cur < maxStacks) cur++;
        _chainStacks[chainID] = cur;
        return cur;
    }

    public static void MarkRoot(int chainID, EffectCollector c)
    {
        if (!_members.TryGetValue(chainID, out var all))
            _members[chainID] = all = new HashSet<EffectCollector>();
        all.Add(c); // root je člen (ale ne „linked“)
    }

    /// Přidej „další“ uzel (počítá se do kapacity) a zapiš do members.
    public static bool TryAdd(int chainID, EffectCollector c, int maxExtraLinks)
    {
        if (!_linked.TryGetValue(chainID, out var set))
            _linked[chainID] = set = new HashSet<EffectCollector>();
        if (!_members.TryGetValue(chainID, out var all))
            _members[chainID] = all = new HashSet<EffectCollector>();

        if (set.Contains(c) || all.Contains(c)) { all.Add(c); return true; }
        if (set.Count >= maxExtraLinks) return false;

        set.Add(c);
        all.Add(c);
        return true;
    }

    public static void Remove(int chainID, EffectCollector c)
    {
        if (_linked.TryGetValue(chainID, out var set))
        {
            set.Remove(c);
            if (set.Count == 0) _linked.Remove(chainID);
        }
        if (_members.TryGetValue(chainID, out var all))
        {
            all.Remove(c);
            if (all.Count == 0)
            {
                _members.Remove(chainID);
                _chainStacks.Remove(chainID);
                _chainMaxStacks.Remove(chainID);
            }
        }
    }
}
