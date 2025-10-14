// Assets/Obscurus/Scripts/Weapons/IgnisFlask/IgnisFlaskProjectile.cs
using UnityEngine;
using Obscurus.Combat;
using Obscurus.Items;

namespace Obscurus.Weapons
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class IgnisFlaskProjectile : MonoBehaviour
    {
        [Header("Spawned AoE")]
        public GameObject aoePrefab;          // nastaví weapon při spawnu
        public LayerMask impactMask = ~0;
        public bool debug = false;

        Rigidbody rb;
        Collider  myCol;
        GameObject owner;
        IgnisFlaskWeapon ownerWeapon; // konkrétní typ kvůli payloadu

        void Awake()
        {
            rb    = GetComponent<Rigidbody>();
            myCol = GetComponent<Collider>();

            // bezpečné fyzikální defaulty
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
#else
            rb.drag = 0f;
            rb.angularDrag = 0.05f;
#endif
            if (myCol) myCol.isTrigger = false;
        }

        public void Throw(Vector3 dir, GameObject ownerObj, RangedWeaponBase weaponRef,
                          float throwForce, float upwardForce)
        {
            owner = ownerObj;
            ownerWeapon = weaponRef as IgnisFlaskWeapon;

            if (!rb)    rb    = GetComponent<Rigidbody>();
            if (!myCol) myCol = GetComponent<Collider>();
            if (!rb) { Debug.LogError("[IgnisFlaskProjectile] Missing Rigidbody on prefab.", this); return; }

            // musí být mimo ruku
            transform.SetParent(null, true);

            // ignoruj kolize s vlastníkem
            if (owner && myCol)
            {
                var ownerCols = owner.GetComponentsInChildren<Collider>(true);
                foreach (var oc in ownerCols) if (oc && oc.enabled) Physics.IgnoreCollision(myCol, oc, true);
            }

            // reset a „kopanec“
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;

            Vector3 v0 = dir.normalized * Mathf.Max(0f, throwForce) + Vector3.up * Mathf.Max(0f, upwardForce);
            rb.AddForce(v0, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 2.0f, ForceMode.VelocityChange);

            if (debug) Debug.DrawRay(transform.position, v0, Color.red, 1.0f, false);
        }

        void OnCollisionEnter(Collision c)
        {
            if (owner && c.collider && c.collider.transform.IsChildOf(owner.transform)) return;

            Vector3 hitPoint, hitNormal;
            if (c.contactCount > 0) { var cp = c.GetContact(0); hitPoint = cp.point; hitNormal = cp.normal; }
            else { hitPoint = transform.position; hitNormal = Vector3.up; }

            if (aoePrefab)
            {
                // spawn zhruba v místě dopadu; area se sama „sesune“ na zem
                var aoe = Instantiate(aoePrefab, hitPoint + hitNormal * 0.02f, Quaternion.identity);
                var area = aoe.GetComponent<IgnisFlaskArea>();
                if (area && ownerWeapon)
                {
                    // ignoruj při hledání země kolidující objekt (typicky enemák)
                    area.groundSnapIgnore = c.collider ? c.collider.transform.root : null;

                    area.Init(owner,
                        ownerWeapon.splashRadius,
                        ownerWeapon.splashDamage,
                        ownerWeapon.dotDamagePerSec,
                        ownerWeapon.dotDuration,
                        ownerWeapon.damageOwner,
                        ownerWeapon.hitMask);
                }
            }
            else
            {
                // fallback instant splash... (typed)
                var hits = Physics.OverlapSphere(
                    hitPoint,
                    ownerWeapon ? ownerWeapon.splashRadius : 2.5f,
                    impactMask,
                    QueryTriggerInteraction.Ignore
                );

                foreach (var h in hits)
                {
                    if (!h) continue;
                    if (ownerWeapon && !ownerWeapon.damageOwner && owner && h.transform.IsChildOf(owner.transform)) continue;

                    var ctx = new DamageContext
                    {
                        amount  = ownerWeapon ? ownerWeapon.splashDamage : 15f,
                        primary = DamageType.Fire,
                        source  = owner ? owner : gameObject
                    };
                    TypedDamage.Apply(h, in ctx, hitPoint, hitNormal, false);
                }
            }

            Destroy(gameObject);
        }
    }
}
