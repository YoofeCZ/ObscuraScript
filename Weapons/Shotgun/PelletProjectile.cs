// Assets/Obscurus/Scripts/Weapons/PelletProjectile.cs
using UnityEngine;
using Obscurus.Combat;
using Obscurus.Items;

namespace Obscurus.Weapons
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class PelletProjectile : MonoBehaviour
    {
        [Header("Kinematics")]
        public float speed = 100f;
        public float lifeTime = 2f;

        [Header("Damage")]
        public float damage = 10f;
        public GameObject owner;

        public DamageContext ctx;

        Rigidbody rb;
        SphereCollider sc;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            sc = GetComponent<SphereCollider>();

            // Bezpečné defaulty pro rychlý projectile
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (sc) sc.isTrigger = false; // používáme OnCollisionEnter
        }

        public void Fire(Vector3 direction, GameObject ownerObj, in DamageContext context)
        {
            owner = ownerObj;
            ctx = context;
            damage = context.amount; // pořád držíme i raw float (debug/inspekce)
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = direction.normalized * speed;
#else
            rb.velocity = direction.normalized * speed;
#endif
            Destroy(gameObject, lifeTime);
        }

        public void Fire(Vector3 direction, GameObject ownerObj, float damageAmount)
        {
            var simple = new DamageContext
            {
                amount = damageAmount,
                primary = DamageType.Physical,
                tags = null,
                isCrit = false,
                source = ownerObj
            };
            Fire(direction, ownerObj, in simple);
        }

        void OnCollisionEnter(Collision col)
        {
            if (!owner) { Destroy(gameObject); return; }

            // ignoruj kolize se střelcem
            if (col.collider && col.collider.transform.IsChildOf(owner.transform)) return;

            // bezpečný kontaktní bod
            Vector3 hitPoint, hitNormal;
            if (col.contactCount > 0)
            {
                var contact = col.GetContact(0);
                hitPoint = contact.point;
                hitNormal = contact.normal;
            }
            else
            {
                hitPoint = transform.position;
                hitNormal = Vector3.up;
            }

            // poškození
            Obscurus.Combat.TypedDamage.Apply(col.collider, in ctx, hitPoint, hitNormal, false);

            // Perk hook (z jakékoliv RangedWeaponBase)
            var weapon = owner.GetComponent<RangedWeaponBase>();
            weapon?.Perk_OnHit(col.collider.gameObject, hitPoint, hitNormal);

            // ===== Bullet hole / impact effect =====
            GameObject hitPrefab = null;

            // 1) preferuj interface (čisté řešení)
            if (weapon is IHasImpactEffect fx) hitPrefab = fx.HitEffectPrefab;

            // 2) Fallback na konkrétní třídy, když interface není implementované
            if (!hitPrefab)
            {
                var sg = owner.GetComponent<ShotgunProjectileWeapon>();
                if (sg && sg.hitEffectPrefab) hitPrefab = sg.hitEffectPrefab;
            }
            if (!hitPrefab)
            {
                var pistol = owner.GetComponent<PistolProjectileWeapon>();
                if (pistol && pistol.hitEffectPrefab) hitPrefab = pistol.hitEffectPrefab;
            }

            if (hitPrefab)
            {
                Quaternion rot = Quaternion.LookRotation(hitNormal);
                rot = Quaternion.AngleAxis(Random.Range(0f, 360f), hitNormal) * rot;
                Vector3 spawnPos = hitPoint + hitNormal * 0.01f;

                // parentuj na zasažený objekt, ať drží s ním
                var hole = Instantiate(hitPrefab, spawnPos, rot, col.collider.transform);
                Destroy(hole, 8f);
            }

            Destroy(gameObject);
        }
    }

    // Interface pro zbraně, které mají hitEffectPrefab
    public interface IHasImpactEffect
    {
        GameObject HitEffectPrefab { get; }
    }
}
