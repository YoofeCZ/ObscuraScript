using System;
using System.Collections.Generic;
using UnityEngine;
using Obscurus.Items;

namespace Obscurus.Player
{
    /// <summary>
    /// Inventář založený na čítačích zdrojů + rezervách munice + vlastněných ZBRANÍCH.
    /// Zdroje: PG (pražské groše), Gold, Mercury, Essence, Scrap, Sub_* (3 substance).
    /// Munice je skrytá (nezobrazuje se v inventáři) – HUD si ji čte přes GetAmmoReserve().
    /// Zbraně se ukládají jako seznam ItemDefinition.Id (GUID) pro snadný persist.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        [Header("Databáze (kvůli maxCarry a HUD ikonám)")]
        [SerializeField] private ItemDatabase itemDb;

        // ====== ZDROJE (fixní seznam) ======
        [Serializable] public class ResourceState
        {
            public int pg;         // Pražské groše
            public int gold;       // Zlato
            public int mercury;    // Rtuť
            public int essence;    // Esence
            public int scrap;      // Šrot

            public int subVitriol;    // “zelený kámen” – vitriol
            public int subAurum;
            public int subMercurius;
        }

        [Header("Zdroje")]
        [SerializeField] private ResourceState resources = new();

        // ====== MUNICE (key->count), serializovatelně přes list ======
        [Serializable] private struct AmmoPair { public string key; public int count; }
        [SerializeField] private List<AmmoPair> ammoPairs = new();

        // runtime dictionary (rychlý přístup)
        private readonly Dictionary<string, int> _ammo = new(StringComparer.Ordinal);

        // ====== ZBRANĚ (seznam definic podle jejich GUID Id) ======
        [Serializable] private class WeaponEntry { public string itemId; }
        [Header("Zbraně (vlastněné)")]
        [SerializeField] private List<WeaponEntry> weaponEntries = new();

        // === Event pro UI / systémy ===
        public event Action Changed;
        void NotifyChanged() => Changed?.Invoke();

        // ---------- Unity ----------
        private void Awake()
        {
            RebuildAmmoMap();
            // Očisti zbraně od prázdných / duplicit
            CleanupWeapons();
        }

        private void OnValidate()
        {
            RebuildAmmoMap();
            CleanupWeapons();
        }

        private void RebuildAmmoMap()
        {
            _ammo.Clear();
            for (int i = 0; i < ammoPairs.Count; i++)
            {
                var k = ammoPairs[i].key;
                if (string.IsNullOrEmpty(k)) continue;
                _ammo[k] = Mathf.Max(0, ammoPairs[i].count);
            }
        }

        private void SyncAmmoPairsFromMap()
        {
            ammoPairs.Clear();
            foreach (var kv in _ammo)
                ammoPairs.Add(new AmmoPair { key = kv.Key, count = kv.Value });
        }

        private void CleanupWeapons()
        {
            for (int i = weaponEntries.Count - 1; i >= 0; i--)
                if (string.IsNullOrWhiteSpace(weaponEntries[i].itemId))
                    weaponEntries.RemoveAt(i);

            // vyhoď duplicity (ponech první výskyt)
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < weaponEntries.Count; )
            {
                if (seen.Contains(weaponEntries[i].itemId))
                    weaponEntries.RemoveAt(i);
                else
                {
                    seen.Add(weaponEntries[i].itemId);
                    i++;
                }
            }
        }

        // ====== PUBLIC API: ZDROJE ======
        public int GetResource(ResourceKey key) => key switch
        {
            ResourceKey.PG            => resources.pg,
            ResourceKey.Gold          => resources.gold,
            ResourceKey.Mercury       => resources.mercury,
            ResourceKey.Essence       => resources.essence,
            ResourceKey.Scrap         => resources.scrap,
            ResourceKey.Sub_Vitriol   => resources.subVitriol,
            ResourceKey.Sub_Aurum     => resources.subAurum,
            ResourceKey.Sub_Mercurius => resources.subMercurius,
            _ => 0
        };

        public void AddResource(ResourceKey key, int amount)
        {
            if (amount == 0) return;
            switch (key)
            {
                case ResourceKey.PG:            resources.pg += amount; break;
                case ResourceKey.Gold:          resources.gold += amount; break;
                case ResourceKey.Mercury:       resources.mercury += amount; break;
                case ResourceKey.Essence:       resources.essence += amount; break;
                case ResourceKey.Scrap:         resources.scrap += amount; break;
                case ResourceKey.Sub_Vitriol:   resources.subVitriol += amount; break;
                case ResourceKey.Sub_Aurum:     resources.subAurum += amount; break;
                case ResourceKey.Sub_Mercurius: resources.subMercurius += amount; break;
            }
            ClampResourcesNonNegative();
            NotifyChanged();
        }

        public bool SpendResource(ResourceKey key, int amount)
        {
            if (amount <= 0) return true;
            if (GetResource(key) < amount) return false;
            AddResource(key, -amount);
            return true;
        }

        private void ClampResourcesNonNegative()
        {
            resources.pg = Mathf.Max(0, resources.pg);
            resources.gold = Mathf.Max(0, resources.gold);
            resources.mercury = Mathf.Max(0, resources.mercury);
            resources.essence = Mathf.Max(0, resources.essence);
            resources.scrap = Mathf.Max(0, resources.scrap);
            resources.subVitriol = Mathf.Max(0, resources.subVitriol);
            resources.subAurum = Mathf.Max(0, resources.subAurum);
            resources.subMercurius = Mathf.Max(0, resources.subMercurius);
        }

        // ====== PUBLIC API: MUNICE (rezervy) ======
        public int GetAmmoReserve(string ammoKey)
        {
            if (string.IsNullOrEmpty(ammoKey)) return 0;
            return _ammo.TryGetValue(ammoKey, out var v) ? v : 0;
        }

        public int GetAmmoMaxCarry(string ammoKey)
        {
            var def = itemDb ? itemDb.FindAmmoByKey(ammoKey) : null;
            return def && def.ammo != null ? Math.Max(1, def.ammo.maxCarry) : 9999;
        }

        /// <summary>Přidá munici do rezervy, respektuje maxCarry. Vrací kolik se reálně přidalo.</summary>
        public int AddAmmo(string ammoKey, int amount)
        {
            if (string.IsNullOrEmpty(ammoKey) || amount == 0) return 0;

            _ammo.TryGetValue(ammoKey, out var cur);
            int max = GetAmmoMaxCarry(ammoKey);
            int target = Mathf.Clamp(cur + amount, 0, max);
            int added = target - cur;
            if (added != 0)
            {
                _ammo[ammoKey] = target;
                SyncAmmoPairsFromMap();
                NotifyChanged();
            }
            return added;
        }

        /// <summary>Odebere až amount z rezervy, vrací kolik skutečně odebral.</summary>
        public int TakeAmmo(string ammoKey, int amount)
        {
            if (string.IsNullOrEmpty(ammoKey) || amount <= 0) return 0;
            _ammo.TryGetValue(ammoKey, out var cur);
            int take = Mathf.Clamp(amount, 0, cur);
            if (take > 0)
            {
                _ammo[ammoKey] = cur - take;
                SyncAmmoPairsFromMap();
                NotifyChanged();
            }
            return take;
        }

        // ====== PUBLIC API: ZBRANĚ (vlastněné) ======
        public bool HasWeapon(ItemDefinition def)
        {
            if (!def) return false;
            var id = SafeId(def);
            if (string.IsNullOrEmpty(id)) return false;
            return weaponEntries.Exists(e => e.itemId == id);
        }

        public bool AddWeapon(ItemDefinition def)
        {
            if (!def) return false;
            var id = SafeId(def);
            if (string.IsNullOrEmpty(id)) return false;
            if (weaponEntries.Exists(e => e.itemId == id)) return false;

            weaponEntries.Add(new WeaponEntry { itemId = id });
            NotifyChanged();
            return true;
        }

        public bool RemoveWeapon(ItemDefinition def)
        {
            if (!def) return false;
            var id = SafeId(def);
            if (string.IsNullOrEmpty(id)) return false;

            int idx = weaponEntries.FindIndex(e => e.itemId == id);
            if (idx < 0) return false;

            weaponEntries.RemoveAt(idx);
            NotifyChanged();
            return true;
        }

        public IReadOnlyList<string> GetOwnedWeaponIds() => weaponEntries.ConvertAll(e => e.itemId);

        private static string SafeId(ItemDefinition it)
        {
            try { return it.Id; } catch { return null; }
        }

        // ====== (Volitelně) Obsolete wrappery – zachytí staré call-site a upozorní v inspektoru ======
        [Obsolete("Slotový inventář byl nahrazen. Použij AddResource/AddAmmo apod.")]
        public bool AddItem(ItemDefinition def, int qty = 1) { Debug.LogWarning("AddItem obsolete"); return false; }
        [Obsolete("Slotový inventář byl nahrazen. Použij SpendResource/TakeAmmo apod.")]
        public bool RemoveItem(ItemDefinition def, int qty = 1) { Debug.LogWarning("RemoveItem obsolete"); return false; }
        [Obsolete("Slotový inventář byl nahrazen čítači. Použij GetResource/ GetAmmoReserve.")]
        public int CountItem(ItemDefinition def) { return 0; }
        [Obsolete("Slotový inventář byl nahrazen čítači. Použij SpendResource(ResourceKey,amount).")]
        public bool SpendCurrency(string currencyKey, int amount) { return false; }
        [Obsolete("Slotový inventář byl nahrazen čítači. Použij GetAmmoReserve/TakeAmmo.")]
        public int GetAmmoCount(string ammoKey) { return GetAmmoReserve(ammoKey); }
        [Obsolete("Slotový inventář byl nahrazen čítači. Použij TakeAmmo.")]
        public bool ConsumeAmmo(string ammoKey, int amount) { return TakeAmmo(ammoKey, amount) == amount; }
    }
}
