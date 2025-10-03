using System;
using System.Collections.Generic;
using UnityEngine;
using Obscurus.Items;

namespace Obscurus.Player
{
    /// <summary>
    /// Obsolete wrapper pro zpětnou kompatibilitu.
    /// Vše přeposílá do PlayerInventory (sekce zbraní).
    /// Můžeš tenhle skript později smazat, až refaktoruješ call-site.
    /// </summary>
    [DisallowMultipleComponent]
    [Obsolete("Používej PlayerInventory (HasWeapon/AddWeapon/RemoveWeapon/GetOwnedWeaponIds). Tento wrapper můžeš smazat.")]
    public class PlayerWeaponInventory : MonoBehaviour
    {
        private PlayerInventory _inv;

        void Awake()
        {
            // Preferuj inventory na stejném rootu/hráči, jinak fallback ve scéně
            _inv = GetComponentInParent<PlayerInventory>() ?? FindObjectOfType<PlayerInventory>(true);
            if (!_inv)
                Debug.LogWarning("[PlayerWeaponInventory] PlayerInventory nebyl nalezen. Wrapper nebude funkční.", this);
        }

        // Propagace eventu – přihlášky/odhlášky přesměruj na PlayerInventory.Changed
        public event Action Changed
        {
            add { if (_inv != null) _inv.Changed += value; }
            remove { if (_inv != null) _inv.Changed -= value; }
        }

        public bool Has(ItemDefinition def) => _inv != null && _inv.HasWeapon(def);
        public bool Add(ItemDefinition def) => _inv != null && _inv.AddWeapon(def);
        public bool Remove(ItemDefinition def) => _inv != null && _inv.RemoveWeapon(def);

        public IReadOnlyList<string> AllIds()
            => _inv != null ? _inv.GetOwnedWeaponIds() : Array.Empty<string>();
    }
}