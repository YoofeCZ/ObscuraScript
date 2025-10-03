using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Obscurus.Player;   // PlayerInventory
using Obscurus.Items;    // ResourceKey

[DisallowMultipleComponent]
public class AlchemyTreeShop : MonoBehaviour
{
    [Header("References")]
    public PlayerInventory inventory;
    public AlchemyPerks perks;

    [Tooltip("Volitelně – pokud máš víc instancí hráče, zkusí si je sám najít.")]
    public bool autoFindAtRuntime = true;

    [Serializable]
    public struct Cost
    {
        public ResourceKey resource;
        [Min(1)] public int amount;
    }

    [Serializable]
    public class PerkConfig
    {
        public PerkId id;
        [Tooltip("Lidský název (pro UI).")]
        public string displayName;
        [Tooltip("Popis do tooltipu.")]
        [TextArea(2,6)] public string description;

        [Tooltip("Cena(-y) — poskládej si libovolný košík.")]
        public Cost[] costs;

        [Tooltip("Volitelné předpoklady (jiné odemčené perky).")]
        public PerkId[] requires;

        [Tooltip("Uzel je jednorázový (běžné). Pokud false, dovolí opakované koupě — nepoužívej, pokud perk je bool).")]
        public bool oneTime = true;

        [Header("UI hooks (optional)")]
        public UnityEvent onPurchased;
        public UnityEvent onFailed;
        public UnityEvent onAlreadyOwned;
        public UnityEvent onLockedByPrereq;
    }

    [Header("Perks in this tree")]
    public List<PerkConfig> perksInTree = new();

    void Awake()
    {
        if (autoFindAtRuntime)
        {
            if (!inventory) inventory = FindObjectOfType<PlayerInventory>(true);
            if (!perks)
            {
                var p = GameObject.FindWithTag("Player");
                if (p) perks = p.GetComponentInChildren<AlchemyPerks>(true);
                if (!perks) perks = FindObjectOfType<AlchemyPerks>(true);
            }
        }
    }
    
    void Update()
    {
        if (autoFindAtRuntime && (!inventory || !perks))
            TryResolveRefs();
    }

    void TryResolveRefs()
    {
        if (!inventory) inventory = FindObjectOfType<PlayerInventory>(true);
        if (!perks)
        {
            var p = GameObject.FindWithTag("Player");
            if (p) perks = p.GetComponentInChildren<AlchemyPerks>(true);
            if (!perks) perks = FindObjectOfType<AlchemyPerks>(true);
        }
    }


    // === PUBLIC API: tlačítko v UI může volat tohle přes OnClick(string) ===
    public void BuyPerkById(string idString)
    {
        if (!Enum.TryParse<PerkId>(idString, out var id))
        {
            Debug.LogWarning($"[AlchemyTreeShop] Unknown perk id '{idString}'.");
            return;
        }
        TryBuy(id);
    }

    public bool TryBuy(PerkId id)
    {
        var cfg = FindCfg(id);
        if (cfg == null)
        {
            Debug.LogWarning($"[AlchemyTreeShop] Config for {id} not found.");
            return false;
        }

        if (!inventory || !perks)
        {
            Debug.LogWarning("[AlchemyTreeShop] Missing inventory/perks reference.");
            cfg.onFailed?.Invoke();
            return false;
        }

        // 1) One-time už vlastněno?
        if (cfg.oneTime && perks.IsUnlocked(id))
        {
            cfg.onAlreadyOwned?.Invoke();
            Debug.Log($"[AlchemyTreeShop] {id} already owned.");
            return false;
        }

        // 2) Prerekvizity
        if (cfg.requires != null && cfg.requires.Length > 0)
        {
            foreach (var req in cfg.requires)
            {
                if (!perks.IsUnlocked(req))
                {
                    cfg.onLockedByPrereq?.Invoke();
                    Debug.Log($"[AlchemyTreeShop] {id} locked by prerequisite: {req}");
                    return false;
                }
            }
        }

        // 3) Finance – nejdřív čistý check (atomická koupě)
        if (!CanAfford(cfg.costs))
        {
            cfg.onFailed?.Invoke();
            Debug.Log($"[AlchemyTreeShop] Not enough resources for {id}.");
            return false;
        }

        // 4) Strhnout košík (teď už víme, že máme na vše)
        SpendBundle(cfg.costs);

        // 5) Aktivovat perk
        perks.SetUnlocked(id, true);

        cfg.onPurchased?.Invoke();
        Debug.Log($"[AlchemyTreeShop] Purchased {id}.");
        return true;
    }

    // === Helpers ===

    PerkConfig FindCfg(PerkId id) => perksInTree.Find(p => p.id == id);

    bool CanAfford(Cost[] basket)
    {
        if (basket == null) return true;
        foreach (var c in basket)
        {
            int have = inventory.GetResource(c.resource);
            if (have < c.amount) return false;
        }
        return true;
    }

    void SpendBundle(Cost[] basket)
    {
        if (basket == null) return;
        foreach (var c in basket)
            inventory.SpendResource(c.resource, c.amount);
    }
}
