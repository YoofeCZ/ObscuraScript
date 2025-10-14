// Assets/Obscurus/Scripts/Weapons/CrossbowWeapon.cs
using UnityEngine;
using Obscurus.Combat;

namespace Obscurus.Weapons
{
    public class CrossbowWeapon : RangedWeaponBase
    {
        [Header("Projectile")]
        public GameObject projectilePrefab;  // ArrowProjectile
        public float projectileSpeed = 55f;
        public float projectileLifetime = 10f;
        public bool useGravity = false;

        // helper s DamageContext (NENÍ override – jen overload)
        protected void FireOneShot(Vector3 dir, float damage, in DamageContext ctx)
        {
            if (!projectilePrefab || muzzle == null) return;

            var go   = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(dir, Vector3.up));
            var proj = go.GetComponent<ArrowProjectile>();
            if (proj)
            {
                proj.Launch(
                    dir.normalized * Mathf.Max(0.1f, projectileSpeed),
                    in ctx, gameObject,
                    Mathf.Max(0.5f, projectileLifetime),
                    useGravity
                );
            }
        }

        // povinný override z base – postaví ctx z databáze
        protected override void FireOneShot(Vector3 dir, float damage)
        {
            var ctx = DamageTyping.BuildRangedContext(weaponDef, db, damage, false, gameObject);
            FireOneShot(dir, damage, in ctx);
        }
    }
}