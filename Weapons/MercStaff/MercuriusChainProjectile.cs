using UnityEngine;
using Obscurus.Effects;
using Obscurus.VFX;
using Obscurus.Combat;

namespace Obscurus.Weapons
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class MercuriusChainProjectile : MonoBehaviour
    {
        [Header("Kinematics")]
        public float speed = 60f;
        public float lifetime = 3f;

        [Header("AOE Settings")]
        public ShockField shockFieldPrefab;
        public float aoeDuration = 3f;
        public float aoeRadius = 6f;
        public float aoeTickInterval = 0.25f;

        [Header("Electrized")]
        public ElectrizedEffectDef electrizedDef;
        public int electrizedStacksPerTick = 1;

        [Header("Targeting")]
        public LayerMask enemyMask = ~0;
        public string enemyTag = "Enemy";

        [Header("VFX")]
        public GameObject fieldVFXPrefab;
        public GameObject impactVFX;

        [Header("Debug")]
        public bool debug;

        // runtime
        Rigidbody _rb;
        SphereCollider _sc;
        GameObject _owner;

        // typed damage payload – amount = DAMAGE PER TICK (pro AOE)
        DamageContext _baseCtx;

        public void Init(GameObject owner, in DamageContext baseCtx)
        {
            _owner   = owner;
            _baseCtx = baseCtx;
            if (_baseCtx.source == null) _baseCtx.source = owner;
        }

        // backwards-compat init
        public void Init(GameObject owner) => Init(owner, in _baseCtx);

        public virtual void Launch(Vector3 dir)
        {
            if (!_rb) _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.isKinematic = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = dir.normalized * speed;
#else
            _rb.velocity = dir.normalized * speed;
#endif
            if (lifetime > 0f) Invoke(nameof(OnLifetimeExpired), lifetime);
        }

        protected virtual void OnLifetimeExpired() => OnHitAndDone();

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _sc = GetComponent<SphereCollider>();
            _sc.isTrigger = false; // kolidujeme se zemí
        }

        void OnCollisionEnter(Collision col)
        {
            // ignoruj vlastníka
            if (_owner && col.collider.transform.IsChildOf(_owner.transform)) return;

            Vector3 hitPoint  = col.contactCount > 0 ? col.GetContact(0).point  : transform.position;
            Vector3 hitNormal = col.contactCount > 0 ? col.GetContact(0).normal : Vector3.up;

            // on-hit: stack jen pokud def říká OnHit
            if (electrizedDef && electrizedDef.gainMode == StackGainMode.OnHit)
            {
                var root = col.rigidbody ? col.rigidbody.transform.root : col.collider.transform.root;
                var ec = root ? root.GetComponent<EffectCollector>() : null;
                if (ec) ec.ApplyElectrized(electrizedDef, 1, _owner);
            }

            // snap na zem
            Vector3 spawnPos = hitPoint;
            Vector3 groundNormal = hitNormal;
            Vector3 probeStart = hitPoint + Vector3.up * 0.5f;
            if (Physics.Raycast(probeStart, Vector3.down, out var groundHit, 5f, ~0, QueryTriggerInteraction.Ignore))
            {
                spawnPos = groundHit.point;
                groundNormal = groundHit.normal;
            }

            // spawn AOE (typed)
            if (shockFieldPrefab)
            {
                var dome = Instantiate(shockFieldPrefab, spawnPos, Quaternion.identity);
                dome.SetupTyped(
                    aoeDuration, aoeRadius, aoeTickInterval, in _baseCtx,
                    electrizedDef, electrizedStacksPerTick,
                    enemyMask, enemyTag,
                    fieldVFXPrefab,
                    groundNormal
                );
                // identita původce (threat/credit pro DoT)
                dome.sourceOwner = _owner;
                if (debug) Debug.Log($"[Projectile] ShockField spawned at {spawnPos} (r={aoeRadius})", this);
            }

            if (impactVFX)
                VFXPool.SpawnOneShot(impactVFX, spawnPos, Quaternion.LookRotation(groundNormal), null, 2f);

            OnHitAndDone();
        }

        protected virtual void OnHitAndDone()
        {
            Destroy(gameObject);
        }
    }
}
