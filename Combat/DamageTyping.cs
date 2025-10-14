// Assets/Obscurus/Scripts/Combat/DamageTyping.cs
using System.Collections.Generic;
using UnityEngine;
using Obscurus.Items;
using Obscurus.Weapons;
using WeaponKind = Obscurus.Items.WeaponKind;

namespace Obscurus.Combat
{
    /// Kontext zásahu – amount se může měnit (crit, DoT tick…),
    /// typy zůstávají (primární + tagy).
    public struct DamageContext
    {
        public float amount;
        public DamageType primary;
        public List<DamageType> tags;
        public bool isCrit;
        public GameObject source;

        public DamageContext WithAmount(float newAmount)
        {
            return new DamageContext {
                amount = newAmount,
                primary = primary,
                tags = tags,
                isCrit = isCrit,
                source = source
            };
        }
    }

    public static class DamageTyping
    {
        // Ranged shot: vezme typy ze zbraně + přidá tagy z munice
        public static DamageContext BuildRangedContext(ItemDefinition weaponDef, ItemDatabase db, float amount, bool isCrit, GameObject source)
        {
            var primary = DamageType.Physical;
            var tags = new List<DamageType>(4);

            if (weaponDef && weaponDef.weaponKind == WeaponKind.Ranged && weaponDef.ranged != null)
            {
                primary = weaponDef.ranged.primaryDamage;
                if (weaponDef.ranged.extraDamageTags != null) tags.AddRange(weaponDef.ranged.extraDamageTags);

                var aKey = weaponDef.ranged.ammoKey;
                if (!string.IsNullOrEmpty(aKey) && db)
                {
                    var ammo = db.FindAmmoByKey(aKey);
                    if (ammo && ammo.ammo != null && ammo.ammo.addDamageTags != null)
                        tags.AddRange(ammo.ammo.addDamageTags);
                }
            }

            // deduplikace (ať se nevrství)
            for (int i = tags.Count - 1; i >= 0; i--)
                if (tags[i] == primary) tags.RemoveAt(i);

            return new DamageContext { amount = amount, primary = primary, tags = tags, isCrit = isCrit, source = source };
        }

        // Melee context (jen ze zbraně)
        public static DamageContext BuildMeleeContext(ItemDefinition weaponDef, float amount, bool isCrit, GameObject source)
        {
            var primary = DamageType.Physical;
            var tags = new List<DamageType>(4);

            if (weaponDef && weaponDef.weaponKind == WeaponKind.Melee && weaponDef.melee != null)
            {
                primary = weaponDef.melee.primaryDamage;
                if (weaponDef.melee.extraDamageTags != null) tags.AddRange(weaponDef.melee.extraDamageTags);
                for (int i = tags.Count - 1; i >= 0; i--)
                    if (tags[i] == primary) tags.RemoveAt(i);
            }

            return new DamageContext { amount = amount, primary = primary, tags = tags, isCrit = isCrit, source = source };
        }
    }

    /// Pokud máš příjemce, který umí typované poškození, implementuj tohle.
    public interface ITypedDamageReceiver
    {
        void ApplyDamage(in DamageContext ctx, Vector3 hitPoint, Vector3 hitNormal);
    }

    /// Bezpečný aplikátor: zkusí typovaně, jinak spadne na legacy float.
    public static class TypedDamage
    {
        public static void Apply(Collider target, in DamageContext ctx, Vector3 point, Vector3 normal, bool headshot = false)
        {
            if (!target) return;

            if (target.TryGetComponent<ITypedDamageReceiver>(out var typed))
            {
                typed.ApplyDamage(ctx, point, normal);
                return;
            }

            // fallback – stávající systém
            DamageUtil.DealDamage(target, ctx.amount, ctx.source, point, normal, headshot);
        }
    }
}
