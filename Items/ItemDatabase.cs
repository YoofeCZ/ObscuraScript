#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System;
using System.Collections.Generic;
using UnityEngine;
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

namespace Obscurus.Items
{
#if HAS_ODIN
    [HideMonoScript]
    [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
    [InfoBox("Centrální databáze itemů. Editor okno ji používá jako zdroj.")]
#endif
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Obscurus/Items/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
#if HAS_ODIN
        [TableList(AlwaysExpanded = true)]
#endif
        [SerializeField] private List<ItemDefinition> items = new();

        // ---- Rychlé indexy (runtime) ----
        private Dictionary<string, ItemDefinition> _byId;                       // Id -> Item
        private Dictionary<ItemType, List<ItemDefinition>> _byType;             // Type -> Items
        private Dictionary<string, ItemDefinition> _ammoByKey;                  // ammoKey -> Ammo Item
        private Dictionary<string, ItemDefinition> _currencyByKey;              // currencyKey -> Currency Item

        public IReadOnlyList<ItemDefinition> Items => items;
        public int Count => items?.Count ?? 0;

        // ======== Public API ========

        /// <summary>Přidá item do DB (ignoruje null a duplicity). Rebuildne index přírůstkově.</summary>
        public void Add(ItemDefinition def)
        {
            if (!def) return;
            if (items.Contains(def)) return;
            items.Add(def);
            Touch(def); // inkrementální update indexů
        }

        /// <summary>Odebere item z DB (ignoruje null). Rebuildne index přírůstkově.</summary>
        public void Remove(ItemDefinition def)
        {
            if (!def) return;
            if (!items.Remove(def)) return;
            DetachFromIndices(def);
        }

        /// <summary>Zda DB obsahuje konkrétní asset (podle reference).</summary>
        public bool Contains(ItemDefinition def) => def && items.Contains(def);

        /// <summary>Vyhledá item podle stabilního <see cref="ItemDefinition.Id"/>. Vrací null, když neexistuje.</summary>
        public ItemDefinition FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null) BuildIndices();
            return _byId.TryGetValue(id, out var v) ? v : null;
        }

        /// <summary>Bez alokací – TryGet pattern.</summary>
        public bool TryGetById(string id, out ItemDefinition def)
        {
            def = null;
            if (string.IsNullOrEmpty(id)) return false;
            if (_byId == null) BuildIndices();
            return _byId.TryGetValue(id, out def);
        }

        /// <summary>Vrátí KOPII seznamu itemů daného typu. (DB interní list neodhalujeme.)</summary>
        public List<ItemDefinition> FindByType(ItemType t)
        {
            if (_byType == null) BuildIndices();
            if (_byType.TryGetValue(t, out var list))
                return new List<ItemDefinition>(list);
            return new List<ItemDefinition>();
        }

        /// <summary>Enumeruje všechny itemy daného typu bez alokací (read-only!).</summary>
        public IEnumerable<ItemDefinition> EnumerateByType(ItemType t)
        {
            if (_byType == null) BuildIndices();
            if (_byType.TryGetValue(t, out var list))
                for (int i = 0; i < list.Count; i++) yield return list[i];
        }

        /// <summary>Najde první item podle zobrazeného jména (case-insensitive). Vhodné pro tooling.</summary>
        public ItemDefinition FindByDisplayName(string name, bool ignoreCase = true)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var cmp = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (!it) continue;
                if (string.Equals(it.name, name, cmp)) return it;
                try
                {
                    if (string.Equals(it.Name, name, cmp)) return it;
                }
                catch { /* ignore */ }
            }
            return null;
        }

        // ======== NEW: Ammo & Currency lookupy ========

        /// <summary>Najde munici podle <c>ammoKey</c> (např. "pistol_ball").</summary>
        public ItemDefinition FindAmmoByKey(string ammoKey)
        {
            if (string.IsNullOrEmpty(ammoKey)) return null;
            if (_ammoByKey == null) BuildIndices();
            return _ammoByKey.TryGetValue(ammoKey, out var def) ? def : null;
        }

        /// <summary>TryGet varianta pro munici.</summary>
        public bool TryGetAmmoByKey(string ammoKey, out ItemDefinition def)
        {
            def = null;
            if (string.IsNullOrEmpty(ammoKey)) return false;
            if (_ammoByKey == null) BuildIndices();
            return _ammoByKey.TryGetValue(ammoKey, out def);
        }

        /// <summary>Najde měnu podle <c>currencyKey</c> (např. "PG").</summary>
        public ItemDefinition FindCurrencyByKey(string currencyKey)
        {
            if (string.IsNullOrEmpty(currencyKey)) return null;
            if (_currencyByKey == null) BuildIndices();
            return _currencyByKey.TryGetValue(currencyKey, out var def) ? def : null;
        }

        /// <summary>TryGet varianta pro měnu.</summary>
        public bool TryGetCurrencyByKey(string currencyKey, out ItemDefinition def)
        {
            def = null;
            if (string.IsNullOrEmpty(currencyKey)) return false;
            if (_currencyByKey == null) BuildIndices();
            return _currencyByKey.TryGetValue(currencyKey, out def);
        }

        /// <summary>Vrátí všechny definice munice.</summary>
        public List<ItemDefinition> GetAllAmmo()
        {
            var list = new List<ItemDefinition>();
            foreach (var it in EnumerateByType(ItemType.Ammunition)) list.Add(it);
            return list;
        }

        /// <summary>Vyjmenuje zbraně používající zadaný <c>ammoKey</c>.</summary>
        public IEnumerable<ItemDefinition> EnumerateWeaponsUsingAmmo(string ammoKey)
        {
            if (string.IsNullOrEmpty(ammoKey)) yield break;
            if (_byType == null) BuildIndices();
            if (_byType.TryGetValue(ItemType.Weapon, out var weapons))
            {
                for (int i = 0; i < weapons.Count; i++)
                {
                    var w = weapons[i];
                    if (w && w.weaponKind == WeaponKind.Ranged && w.ranged != null &&
                        string.Equals(w.ranged.ammoKey, ammoKey, StringComparison.Ordinal))
                    {
                        yield return w;
                    }
                }
            }
        }

        /// <summary>Full rebuild indexů (např. po hromadných úpravách). Běží v O(N).</summary>
        [ContextMenu("Rebuild Indices")]
        public void RebuildIndices() => BuildIndices();

        // ======== Interní údržba ========

        private void Touch(ItemDefinition def)
        {
            if (!def) return;

            // zajisti indexy
            _byId ??= new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            _byType ??= new Dictionary<ItemType, List<ItemDefinition>>();
            _ammoByKey ??= new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            _currencyByKey ??= new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);

            var id = SafeId(def);
            if (!string.IsNullOrEmpty(id))
                _byId[id] = def;

            var t = def.Type;
            if (!_byType.TryGetValue(t, out var list))
            {
                list = new List<ItemDefinition>();
                _byType[t] = list;
            }
            if (!list.Contains(def))
                list.Add(def);

            // ammo index
            if (t == ItemType.Ammunition && def.ammo != null && !string.IsNullOrEmpty(def.ammo.ammoKey))
            {
                // pokud je duplicitní ammoKey, poslední vyhrává + log warning
                if (_ammoByKey.TryGetValue(def.ammo.ammoKey, out var oldAmmo) && oldAmmo != def)
                {
                    Debug.LogWarning($"[ItemDatabase] Duplicate ammoKey '{def.ammo.ammoKey}' between '{oldAmmo?.name}' and '{def.name}'. Using the latest.");
                }
                _ammoByKey[def.ammo.ammoKey] = def;
            }

            // currency index
            if (t == ItemType.Currency && def.currency != null && !string.IsNullOrEmpty(def.currency.currencyKey))
            {
                if (_currencyByKey.TryGetValue(def.currency.currencyKey, out var oldCur) && oldCur != def)
                {
                    Debug.LogWarning($"[ItemDatabase] Duplicate currencyKey '{def.currency.currencyKey}' between '{oldCur?.name}' and '{def.name}'. Using the latest.");
                }
                _currencyByKey[def.currency.currencyKey] = def;
            }
        }

        private void DetachFromIndices(ItemDefinition def)
        {
            if (_byId == null && _byType == null && _ammoByKey == null && _currencyByKey == null) return;

            if (_byId != null)
            {
                var id = SafeId(def);
                if (!string.IsNullOrEmpty(id)) _byId.Remove(id);
            }

            if (_byType != null)
            {
                var t = def.Type;
                if (_byType.TryGetValue(t, out var list)) list.Remove(def);
            }

            if (_ammoByKey != null && def.ammo != null && !string.IsNullOrEmpty(def.ammo.ammoKey))
            {
                // odstraň jen pokud jde o stejnou referenci
                if (_ammoByKey.TryGetValue(def.ammo.ammoKey, out var cur) && cur == def)
                    _ammoByKey.Remove(def.ammo.ammoKey);
            }

            if (_currencyByKey != null && def.currency != null && !string.IsNullOrEmpty(def.currency.currencyKey))
            {
                if (_currencyByKey.TryGetValue(def.currency.currencyKey, out var cur) && cur == def)
                    _currencyByKey.Remove(def.currency.currencyKey);
            }
        }

        private void BuildIndices()
        {
            _byId         = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            _byType       = new Dictionary<ItemType, List<ItemDefinition>>();
            _ammoByKey    = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            _currencyByKey= new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);

            // vyhoď nully
            for (int i = items.Count - 1; i >= 0; i--)
                if (!items[i]) items.RemoveAt(i);

            // vyhoď duplicitní reference (ponech první výskyt)
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                for (int j = i - 1; j >= 0; j--)
                {
                    if (items[j] == it) { items.RemoveAt(i); break; }
                }
            }

            // naplň indexy
            foreach (var it in items)
            {
                if (!it) continue;

                var id = SafeId(it);
                if (!string.IsNullOrEmpty(id) && !_byId.ContainsKey(id))
                    _byId.Add(id, it);

                var t = it.Type;
                if (!_byType.TryGetValue(t, out var list))
                {
                    list = new List<ItemDefinition>();
                    _byType[t] = list;
                }
                list.Add(it);

                if (t == ItemType.Ammunition && it.ammo != null && !string.IsNullOrEmpty(it.ammo.ammoKey))
                {
                    if (_ammoByKey.TryGetValue(it.ammo.ammoKey, out var old) && old != it)
                        Debug.LogWarning($"[ItemDatabase] Duplicate ammoKey '{it.ammo.ammoKey}' between '{old?.name}' and '{it.name}'. Using the latest.");
                    _ammoByKey[it.ammo.ammoKey] = it;
                }

                if (t == ItemType.Currency && it.currency != null && !string.IsNullOrEmpty(it.currency.currencyKey))
                {
                    if (_currencyByKey.TryGetValue(it.currency.currencyKey, out var old) && old != it)
                        Debug.LogWarning($"[ItemDatabase] Duplicate currencyKey '{it.currency.currencyKey}' between '{old?.name}' and '{it.name}'. Using the latest.");
                    _currencyByKey[it.currency.currencyKey] = it;
                }
            }
        }

        private static string SafeId(ItemDefinition it)
        {
            try { return it.Id; } catch { return null; }
        }

        // ======== Unity lifecycle ========

        private void OnEnable()
        {
            BuildIndices();
        }

        private void OnValidate()
        {
            BuildIndices();
        }
    }
}
