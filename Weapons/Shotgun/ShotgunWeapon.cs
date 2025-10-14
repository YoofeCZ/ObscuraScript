// Assets/Obscurus/Scripts/Weapons/ShotgunProjectileWeapon.cs
using UnityEngine;
using Obscurus.Combat;

namespace Obscurus.Weapons
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Obscurus/Weapons/Shotgun Weapon (Projectile)")]
    public class ShotgunProjectileWeapon : RangedWeaponBase, IHasImpactEffect
    {
        [Header("Shotgun (fallback hodnoty, když nejsou v DB)")]
        [Min(1)] public int pelletCount = 8;
        [Range(0.1f, 15f)] public float spreadAngleDeg = 4f;
        [Tooltip("Kolikrát násobit damage při nesdíleném poškození (viz slug/shrapnel).")]
        public bool splitDamageAcrossPellets = true;
        public float pelletDamageMultiplier = 0.2f;

        [Header("Projectile (fallback)")]
        public PelletProjectile pelletPrefab;
        public float pelletSpeed = 120f;
        public float pelletLifeTime = 2f;

        [Header("FX")]
        public GameObject muzzleFlashPrefab;
        public GameObject hitEffectPrefab;
        GameObject IHasImpactEffect.HitEffectPrefab => hitEffectPrefab;

        public bool debugSpawn = false;

        // Povinný override z base
        protected override void FireOneShot(Vector3 dir, float damage)
        {
            // 1) Načti DB payload (RangedWeaponData)
            var rd = (weaponDef && weaponDef.ranged != null) ? weaponDef.ranged : null;

            int   count      = rd != null ? Mathf.Max(1, rd.pelletCount) : pelletCount;
            float spreadDeg  = rd != null ? rd.spreadAngleDeg : spreadAngleDeg;
            bool  split      = rd != null ? rd.splitDamageAcrossPellets >= 0.5f : splitDamageAcrossPellets;
            float pelletMult = rd != null ? rd.pelletDamageMultiplier : pelletDamageMultiplier;
            float speed      = rd != null ? rd.projectileSpeed    : pelletSpeed;
            float life       = rd != null ? rd.projectileLifetime : pelletLifeTime;

            // 2) Multiplikátory munice (rychlost/“přesnost” → spread)
            float speedMult = 1f, accMult = 1f;
            if (db && !string.IsNullOrEmpty(AmmoKey))
            {
                var ammoDef = db.FindAmmoByKey(AmmoKey);
                if (ammoDef && ammoDef.ammo != null)
                {
                    speedMult = Mathf.Max(0f, ammoDef.ammo.speedMultiplier);
                    accMult   = Mathf.Max(0.01f, ammoDef.ammo.accuracyMultiplier);
                }
            }
            speed     *= speedMult;
            spreadDeg /= accMult;

            // 3) Per-pellet damage rozdělení
            float dmgPerPellet = split
                ? (damage / Mathf.Max(1, count))
                : (damage * pelletMult);

            // 4) Postav TYPED kontext pro pellet (tagy z DB + munice), pak jen měníme amount
            var baseCtx = DamageTyping.BuildRangedContext(weaponDef, db, dmgPerPellet, false, gameObject);

            for (int i = 0; i < count; i++)
            {
                Vector3 pdir = ApplySpread(dir, spreadDeg);

                if (debugSpawn)
                    Debug.DrawRay(muzzle.position, pdir * 3f, Color.red, 0.15f);

                var pellet = Instantiate(pelletPrefab, muzzle.position, Quaternion.identity);
                pellet.speed    = speed;
                pellet.lifeTime = life;

                // pro každý pellet jen upravíme částku v kontextu
                var pelletCtx = baseCtx.WithAmount(dmgPerPellet);
                pellet.Fire(pdir, gameObject, in pelletCtx);
            }

            if (muzzleFlashPrefab)
                Instantiate(muzzleFlashPrefab, muzzle.position, muzzle.rotation, muzzle);
        }

        private static Vector3 ApplySpread(Vector3 forward, float angleDeg)
        {
            forward = forward.normalized;
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 off = Random.insideUnitCircle * Mathf.Tan(rad);

            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(up, forward)) > 0.99f) up = Vector3.right;

            Vector3 right = Vector3.Cross(forward, up).normalized;
            Vector3 trueUp = Vector3.Cross(right, forward).normalized;

            Vector3 dir = (forward + right * off.x + trueUp * off.y).normalized;
            return dir;
        }
    }
}
