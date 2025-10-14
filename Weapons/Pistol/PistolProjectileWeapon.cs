// Assets/Obscurus/Scripts/Weapons/PistolProjectileWeapon.cs
using UnityEngine;
using Obscurus.Combat;

namespace Obscurus.Weapons
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Obscurus/Weapons/Pistol Weapon (Projectile)")]
    public class PistolProjectileWeapon : RangedWeaponBase, IHasImpactEffect
    {
        [Header("Projectile")]
        public PelletProjectile pelletPrefab;
        public float pelletSpeed = 200f;      // fallback když v DB není vyplněno
        public float pelletLifeTime = 3f;     // fallback když v DB není vyplněno

        [Header("Spread")]
        [Range(0f, 10f)] public float spreadAngleDeg = 0.5f; // fallback – malý rozptyl

        [Header("FX")]
        public GameObject muzzleFlashPrefab;
        public GameObject hitEffectPrefab; // bullet hole prefab

        // Vystřel jeden projektil – TYPED varianta (preferovaná)
        protected override void FireOneShot(Vector3 dir, float damage, in DamageContext ctx)
        {
            // --- Balistika z DB (fallback na lokální inspektorová pole) ---
            var rd = (weaponDef && weaponDef.ranged != null) ? weaponDef.ranged : null;

            // multiplikátory munice
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

            float effSpeed    = (rd != null ? rd.projectileSpeed    : pelletSpeed)    * speedMult;
            float effLifetime = (rd != null ? rd.projectileLifetime : pelletLifeTime);
            float effSpread   = (rd != null ? rd.spreadAngleDeg     : spreadAngleDeg) / accMult;

            Vector3 pdir = ApplySpread(dir, effSpread);

            var pellet = Instantiate(pelletPrefab, muzzle.position, Quaternion.identity);
            pellet.speed    = effSpeed;
            pellet.lifeTime = effLifetime;
            pellet.Fire(pdir, gameObject, in ctx);   // << typovaný kontext

            if (muzzleFlashPrefab)
                Instantiate(muzzleFlashPrefab, muzzle.position, muzzle.rotation, muzzle);
        }

        // Fallback pro starý abstract (může zůstat – volá typed, aby to fungovalo i bez kontextu)
        protected override void FireOneShot(Vector3 dir, float damage)
        {
            var ctx = DamageTyping.BuildRangedContext(weaponDef, db, damage, false, gameObject);
            FireOneShot(dir, damage, in ctx);
        }

        // přístup k bullet hole prefab pro pellet skrz rozhraní
        GameObject IHasImpactEffect.HitEffectPrefab => hitEffectPrefab;

        // Stejná metoda ApplySpread jako v shotgunu
        private static Vector3 ApplySpread(Vector3 forward, float angleDeg)
        {
            forward = forward.normalized;
            if (angleDeg <= 0f) return forward;

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
