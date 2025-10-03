// Obscurus/Weapons/IWeapon.cs
using UnityEngine;
using Obscurus.Items;
using Obscurus.Player;

namespace Obscurus.Weapons
{
    public enum WeaponKind { Ranged, Melee }

    public interface IWeapon
    {
        WeaponKind Kind { get; }
        ItemDefinition Definition { get; }

        /// Zavolá se při equipu do holderu (zapni si efekty, cacheni refy…)
        void OnEquip(WeaponHolder holder);

        /// Zavolá se při holsteru/odložení (zastav nabíjení, vypni aim apod.)
        void OnHolster();

        GameObject gameObject { get; }
    }
}