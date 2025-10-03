using System;
using System.Collections.Generic;
using UnityEngine;
using Obscurus.Items;
using Obscurus.Weapons;

namespace Obscurus.Player
{
    /// PRE-STACKED WEAPONS:
    /// - V Inspectoru nadefinuj sloty: (ItemDefinition, Instance MonoBehaviour, UnlockedByDefault)
    /// - ŽÁDNÉ SOCKETY, ŽÁDNÝ WeaponGrip, žádné dynamické instancování.
    /// - Unlock(def) jen zpřístupní slot; Equip() zapíná/vypíná GameObjecty.
    [DisallowMultipleComponent]
    public class WeaponHolder : MonoBehaviour
    {
        // Lokální hráč (HUD se na tohle připne i po spawnu)
        public static WeaponHolder Local { get; private set; }
        public static event Action<WeaponHolder> LocalChanged;

        [Tooltip("Je to holder lokálního hráče? Nech zapnuté na Playerovi.")]
        public bool isPlayerHolder = true;

        [Header("Refs")]
        public PlayerInventory playerInventory;
        public ItemDatabase itemDb;

        [Serializable]
        public class Slot
        {
            [Tooltip("Definice zbraně (ScriptableObject).")]
            public ItemDefinition definition;

            [Tooltip("Předem umístěná instance (GameObject s komponentou, která implementuje IWeapon).")]
            public MonoBehaviour instance;

            [Tooltip("Má být odemčená už na startu (bez pickupu)?")]
            public bool unlockedByDefault = false;

            [NonSerialized] public IWeapon weaponIfc;   // vyplní se v runtime
            [NonSerialized] public bool   unlocked;     // runtime stav
        }

        [Header("Slots (pre-stacked)")]
        public List<Slot> slots = new();

        [Header("Runtime")]
        public IWeapon Current { get; private set; }
        public event Action<IWeapon> WeaponChanged;

        private readonly List<IWeapon> _available = new();  // odemčené zbraně (v pořadí slotů)
        private int _index = -1;

        // ---------- Unity ----------
        private void Awake()
        {
            if (!playerInventory) playerInventory = GetComponentInParent<PlayerInventory>() ?? FindObjectOfType<PlayerInventory>(true);
            if (!itemDb) itemDb = FindObjectOfType<ItemDatabase>(true);

            if (isPlayerHolder)
            {
                Local = this;
                LocalChanged?.Invoke(Local);
            }

            BootstrapSlots();

            // Auto-equip první odemčenou
            if (_available.Count > 0)
                EquipByIndex(0);
        }

        private void OnDestroy()
        {
            if (Local == this)
            {
                Local = null;
                LocalChanged?.Invoke(null);
            }
        }

        private void OnValidate()
        {
            // Soft validace v editoru (nepřistupovat k ItemDB apod.)
            if (slots == null) return;
            foreach (var s in slots)
            {
                if (!s?.instance) continue;
                if (s.instance is not IWeapon)
                {
                    Debug.LogWarning($"[WeaponHolder] Instance '{s.instance.name}' neimplementuje IWeapon.", s.instance);
                }
            }
        }

        // ---------- Init ----------
        private void BootstrapSlots()
        {
            _available.Clear();
            _index = -1;

            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null) continue;

                // Vyřeš IWeapon rozhraní
                s.weaponIfc = null;
                if (s.instance && s.instance is IWeapon iw)
                {
                    s.weaponIfc = iw;
                    // na startu vždy vypnout GO (zapíná se až při equipu)
                    var go = s.instance.gameObject;
                    if (go.activeSelf) go.SetActive(false);
                }
                else if (s.instance != null)
                {
                    Debug.LogWarning($"[WeaponHolder] Slot[{i}] má instance '{s.instance.name}', ale ta neimplementuje IWeapon.", s.instance);
                }

                // Rozhodni odemčení
                s.unlocked = s.unlockedByDefault || (playerInventory && playerInventory.HasWeapon(s.definition));

                // Zaregistruj do dostupných
                if (s.unlocked && s.weaponIfc != null)
                    _available.Add(s.weaponIfc);
            }
        }

        // ---------- Public API ----------
        public IReadOnlyList<IWeapon> Owned => _available;

        public bool EquipByIndex(int i)
        {
            if (_available.Count == 0) return false;
            if (i < 0 || i >= _available.Count) return false;
            _index = i;
            SetWeapon(_available[_index]);
            return true;
        }

        public void Next()
        {
            if (_available.Count == 0) return;
            _index = (_index + 1) % _available.Count;
            SetWeapon(_available[_index]);
        }

        public void Prev()
        {
            if (_available.Count == 0) return;
            _index = (_index - 1 + _available.Count) % _available.Count;
            SetWeapon(_available[_index]);
        }

        public bool EquipByDefinition(ItemDefinition def)
        {
            if (!def) return false;
            for (int i = 0; i < _available.Count; i++)
            {
                var w = _available[i];
                if (SameDef(w?.Definition, def))
                    return EquipByIndex(i);
            }
            return false;
        }

        /// <summary>Odemkne slot podle ItemDefinition (bez instancování). Volitelně rovnou equipne.</summary>
        public bool Unlock(ItemDefinition def, bool autoEquip = true)
        {
            if (!def) return false;

            // Najdi slot podle definice
            int slotIdx = FindSlotIndex(def);
            if (slotIdx < 0)
            {
                Debug.LogWarning($"[WeaponHolder] Unlock({def.Name}) – slot s touto definicí nebyl nalezen. Přidej ho do 'slots' v Inspectoru.");
                return false;
            }

            var s = slots[slotIdx];
            if (s.weaponIfc == null)
            {
                Debug.LogWarning($"[WeaponHolder] Unlock({def.Name}) – slot existuje, ale 'instance' chybí nebo neimplementuje IWeapon.");
                return false;
            }

            if (!s.unlocked)
            {
                s.unlocked = true;
                _available.Add(s.weaponIfc);
            }

            if (autoEquip)
            {
                // Najdi index v _available a equipni
                for (int i = 0; i < _available.Count; i++)
                {
                    if (SameDef(_available[i]?.Definition, def))
                    {
                        return EquipByIndex(i);
                    }
                }
            }

            return true;
        }

        public bool Contains(ItemDefinition def)
        {
            if (!def) return false;
            foreach (var w in _available)
                if (SameDef(w?.Definition, def))
                    return true;
            return false;
        }

        // ---------- Internals ----------
        private void SetWeapon(IWeapon w)
        {
            if (Current == w)
            {
                WeaponChanged?.Invoke(Current);
                return;
            }

            // Deactivate previous
            if (Current is MonoBehaviour prevMb)
            {
                try { Current.OnHolster(); } catch { /* ignore */ }
                if (prevMb && prevMb.gameObject.activeSelf)
                    prevMb.gameObject.SetActive(false);
            }

            Current = w;

            // Activate new
            if (Current is MonoBehaviour mb)
            {
                if (!mb.gameObject.activeSelf)
                    mb.gameObject.SetActive(true);

                try { Current.OnEquip(this); } catch { /* ignore */ }
            }

            WeaponChanged?.Invoke(Current);
        }

        private int FindSlotIndex(ItemDefinition def)
        {
            if (slots == null) return -1;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s != null && SameDef(s.definition, def))
                    return i;
            }
            return -1;
        }

        private static bool SameDef(ItemDefinition a, ItemDefinition b)
        {
            if (!a || !b) return false;
            // Preferuj porovnání podle persistentního Id, fallback reference
            try
            {
                if (!string.IsNullOrEmpty(a.Id) && !string.IsNullOrEmpty(b.Id))
                    return string.Equals(a.Id, b.Id, StringComparison.Ordinal);
            }
            catch { /* some defs nemusí mít Id getter v editoru */ }
            return a == b;
        }
    }
}
