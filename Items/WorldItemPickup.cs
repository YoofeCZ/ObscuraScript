using UnityEngine;
using Obscurus.Items;
using Obscurus.Player;

namespace Obscurus.Items
{
    /// Dejte na world pickup (Collider = Trigger).
    [RequireComponent(typeof(Collider))]
    public class WorldItemPickup : MonoBehaviour
    {
        public ItemIdentity identity;          // auto-najde se
        public bool destroyOnPickup = true;

        [Header("Rules")]
        [Tooltip("Instantní pickup (HP/Armor/Stamina…) se vezme jen pokud by alespoň jeden efekt reálně něco přidal.")]
        public bool requireEffectToApply = true;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col) col.isTrigger = true;
            identity = GetComponent<ItemIdentity>();
        }

        void Awake()
        {
            if (!identity) identity = GetComponent<ItemIdentity>();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!identity || !identity.definition) return;

            // Který objekt je „hráč“? Vezmeme kořen s komponentami (HP/Inventory…)
            var rootGO = other.attachedRigidbody ? other.attachedRigidbody.gameObject
                                                 : other.gameObject;
            rootGO = rootGO.transform.root.gameObject;

            var def = identity.definition;

            // 1) PickupConsumable + Apply instantly (ALE jen když má kam přidat)
            if (def.Type == ItemType.PickupConsumable && def.applyInstantlyOnPickup && def.consumable != null)
            {
                if (requireEffectToApply && !CanAnyEffectApplyNow(def, rootGO))
                {
                    // sem si klidně dej "deny" SFX/flash
                    Debug.Log($"Cannot pick {def.Name} – all effects would be wasted (HP/Armor/Stamina full).", this);
                    return;
                }

                ItemEffectApplier.ApplyAll(def, rootGO, HealSource.Pickup);
                if (destroyOnPickup) Destroy(gameObject);
                return;
            }

            // 2) Ammunition → přidej do PlayerInventory (respektuj maxCarry)
            if (def.Type == ItemType.Ammunition && def.ammo != null)
            {
                var inv = FindInventoryNear(rootGO);
                if (!inv)
                {
                    Debug.LogWarning("[WorldItemPickup] Ammunition pickup – PlayerInventory nenalezen.", this);
                    return;
                }

                string ammoKey = def.ammo.ammoKey;
                if (string.IsNullOrEmpty(ammoKey))
                {
                    Debug.LogWarning("[WorldItemPickup] Ammunition bez ammoKey – ignoruji.", this);
                    return;
                }

                int amount = Mathf.Max(1, identity.stack);
                int before = inv.GetAmmoReserve(ammoKey);
                int added  = inv.AddAmmo(ammoKey, amount);
                int after  = inv.GetAmmoReserve(ammoKey);

                if (added > 0)
                {
                    Debug.Log($"Picked up {def.Name} (+{added} {ammoKey}) → {before}→{after}");
                    if (destroyOnPickup) Destroy(gameObject);
                }
                else
                {
                    int max = inv.GetAmmoMaxCarry(ammoKey);
                    Debug.Log($"Cannot pick {def.Name} – ammo '{ammoKey}' is full ({before}/{max}).");
                }
                return;
            }

            // 3) Weapon → zapiš vlastnictví a ODEMKNI slot v holderu (žádné instancování)
            if (def.Type == ItemType.Weapon && def.weapon != null)
            {
                var inv = FindInventoryNear(rootGO);
                if (!inv)
                {
                    Debug.LogWarning("[WorldItemPickup] Weapon pickup – PlayerInventory nenalezen.", this);
                    return;
                }

                var holder = rootGO.GetComponentInChildren<WeaponHolder>() ?? FindObjectOfType<WeaponHolder>(true);
                if (!holder)
                {
                    Debug.LogWarning("[WorldItemPickup] Weapon picked, ale WeaponHolder nenalezen.", this);
                    return;
                }

                bool firstTime = inv.AddWeapon(def); // false = už vlastním
                holder.Unlock(def, autoEquip: true);
                Debug.Log(firstTime
                    ? $"Picked up WEAPON: {def.Name} (vlastnictví zapsáno, slot odemknut & equipped)"
                    : $"Picked up WEAPON: {def.Name} (už vlastněno → equipped)");

                if (destroyOnPickup) Destroy(gameObject);
                return;
            }

            // 4) Default
            Debug.Log($"Picked up {def.Name} (x{identity.stack}). Route to inventory here.");
            if (destroyOnPickup) Destroy(gameObject);
        }

        // Najde PlayerInventory na hráči; když není na rootu, zkusí najít kdekoliv ve scéně.
        PlayerInventory FindInventoryNear(GameObject root)
        {
            var inv = root.GetComponentInParent<PlayerInventory>();
            if (!inv) inv = FindObjectOfType<PlayerInventory>(true);
            return inv;
        }

        // === Preview: vrátí true, pokud aspoň jeden efekt nebude "waste" ===
        static bool CanAnyEffectApplyNow(ItemDefinition def, GameObject rootGO)
        {
            if (def == null || def.consumable == null || def.consumable.effects == null)
                return false;

            // Záměrně hledáme v parentech (hráč může mít HP/Armor na rootu či childu)
            var hp  = rootGO.GetComponentInParent<HealthSystem>();
            var ar  = rootGO.GetComponentInParent<ArmorSystem>();
            var st  = rootGO.GetComponentInParent<StaminaSystem>();
            // Pokud máš SanitySystem, můžeš přidat stejnou logiku (Current/max).

            foreach (var e in def.consumable.effects)
            {
                switch (e.type)
                {
                    case ItemEffectType.HealHP:
                    case ItemEffectType.RegenHPOverTime:
                        if (hp && (hp.Current + 1e-4f) < hp.max) return true;
                        break;

                    case ItemEffectType.AddArmorFlat:
                        if (ar && (ar.Current + 1e-4f) < ar.max) return true;
                        break;

                    case ItemEffectType.RestoreStamina:
                    case ItemEffectType.RegenStaminaOverTime:
                        if (st && (st.Current + 1e-4f) < st.max) return true;
                        break;

                    case ItemEffectType.RestoreSanity:
                    case ItemEffectType.RegenSanityOverTime:
                        // pokud používáš Sanity pool, doplň zde check jako u HP
                        // pro jistotu povolíme (nebude to blokovat pickup)
                        return true;
                }
            }
            return false;
        }
    }
}
