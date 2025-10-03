using System.Collections.Generic;
using UnityEngine;
using Obscurus.Items;

namespace Obscurus.Player
{
    [System.Serializable]
    public class WeaponUpgradeState
    {
        public int  damageTiers;   // každý tier = +X dmg
        public bool vitriolRune;   // příklad “runové rytiny”
    }

    /// <summary>
    /// Per-zbraň upgrady držené u hráče. Spotřebovává Scrap a Substance přes ResourceKey.
    /// </summary>
    public class WeaponUpgradeService : MonoBehaviour
    {
        [Header("Refs")]
        public PlayerInventory inventory;

        [Header("Balancing")]
        public int   scrapPerTier   = 10;
        public int   scrapPerRune   = 10;
        public int   vitriolPerRune = 1;
        public float damagePerTier  = 3f;

        // weaponId (ItemDefinition.Id) -> state
        private readonly Dictionary<string, WeaponUpgradeState> _byWeapon = new();

        public WeaponUpgradeState GetState(ItemDefinition weapon)
        {
            if (!weapon || weapon.Type != ItemType.Weapon) return null;
            if (!_byWeapon.TryGetValue(weapon.Id, out var s))
            {
                s = new WeaponUpgradeState();
                _byWeapon[weapon.Id] = s;
            }
            return s;
        }

        public float GetFinalDamage(ItemDefinition weapon)
        {
            if (!weapon || weapon.Type != ItemType.Weapon) return 0f;
            var baseDmg = weapon.weapon != null ? weapon.weapon.baseDamage : 0f;
            var st = GetState(weapon);
            return baseDmg + (st.damageTiers * damagePerTier);
        }

        /// <summary>+1 damage tier: spotřebuje Scrap (ResourceKey.Scrap).</summary>
        public bool TryUpgradeDamage(ItemDefinition weapon)
        {
            if (!weapon || weapon.Type != ItemType.Weapon || inventory == null) return false;
            if (!inventory.SpendResource(ResourceKey.Scrap, scrapPerTier)) return false;

            GetState(weapon).damageTiers++;
            return true;
        }

        /// <summary>Jednorázová vitriolová „runa“: 1× Sub_Vitriol + Scrap.</summary>
        public bool TryEngraveVitriolRune(ItemDefinition weapon, ItemDefinition vitriolSubstance)
        {
            if (!weapon || weapon.Type != ItemType.Weapon || inventory == null) return false;

            // volitelná validace původního parametru (ponecháno kvůli starým call-site)
            if (!vitriolSubstance ||
                vitriolSubstance.Type != ItemType.Substance ||
                vitriolSubstance.substance == null ||
                vitriolSubstance.substance.branch != SubstanceBranch.Vitriol)
                return false;

            var st = GetState(weapon);
            if (st.vitriolRune) return false; // jednorázová

            if (!inventory.SpendResource(ResourceKey.Scrap, scrapPerRune)) return false;
            if (!inventory.SpendResource(ResourceKey.Sub_Vitriol, vitriolPerRune)) return false;

            st.vitriolRune = true;
            return true;
        }

        // (chceš-li obecnější runy:)
        public bool TryEngraveRune(ItemDefinition weapon, SubstanceBranch branch, int runeCost = 1)
        {
            if (!weapon || weapon.Type != ItemType.Weapon || inventory == null) return false;

            if (!BranchToResourceKey(branch, out var subKey)) return false;

            var st = GetState(weapon);
            // pokud budeš mít více typů run, přidej do stavu vlastní flagy/počty

            if (!inventory.SpendResource(ResourceKey.Scrap, scrapPerRune)) return false;
            if (!inventory.SpendResource(subKey, runeCost)) return false;

            // např. pro Vitriol nastavíme existující flag
            if (branch == SubstanceBranch.Vitriol) st.vitriolRune = true;
            return true;
        }

        private static bool BranchToResourceKey(SubstanceBranch b, out ResourceKey key)
        {
            switch (b)
            {
                case SubstanceBranch.Vitriol:   key = ResourceKey.Sub_Vitriol;   return true;
                case SubstanceBranch.Aurum:     key = ResourceKey.Sub_Aurum;     return true;
                case SubstanceBranch.Mercurius: key = ResourceKey.Sub_Mercurius; return true;
                default: key = default; return false;
            }
        }
    }
}
