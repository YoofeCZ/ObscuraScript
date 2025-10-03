// CrossbowWeapon.cs (beze změn logiky; jen použijeme dir z base)
using UnityEngine;

namespace Obscurus.Weapons
{
    public class CrossbowWeapon : RangedWeaponBase
    {
        [Header("Projectile")]
        public GameObject projectilePrefab;  // ArrowProjectile
        public float projectileSpeed = 55f;
        public float projectileLifetime = 10f;
        public bool useGravity = false;

        protected override void FireOneShot(Vector3 dir, float damage)
        {
            if (!projectilePrefab || muzzle == null) return;

            var go = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(dir, Vector3.up));
            var proj = go.GetComponent<ArrowProjectile>();
            if (proj)
            {
                proj.Launch(dir.normalized * Mathf.Max(0.1f, projectileSpeed),
                    damage, gameObject,
                    Mathf.Max(0.5f, projectileLifetime),
                    useGravity);
            }
        }
    }
}