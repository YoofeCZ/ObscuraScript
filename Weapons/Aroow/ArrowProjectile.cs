// Assets/Obscurus/Scripts/Weapons/ArrowProjectile.cs
using UnityEngine;
using Obscurus.Combat;

namespace Obscurus.Weapons
{
    [RequireComponent(typeof(Rigidbody))]
    public class ArrowProjectile : MonoBehaviour
    {
        [Header("Model orientation")]
        public Vector3 modelForwardLocal = Vector3.forward;

        [Header("Sticking")]
        public Transform stickStop;
        public float    fallbackPenetration = 0.06f;
        public bool     alignToSurfaceNormal = true;
        public bool     parentToHit = true;
        public LayerMask hitMask = ~0;

        [Header("Raycast pre-hit (proti odrazu)")]
        public float castRadius = 0.03f;

        [Header("Lifetime")]
        public float stickSeconds = 5f;
        public bool  destroyOnNoHit = true;

        private DamageContext _ctx;
        private Rigidbody _rb;
        private float _life, _spawnTime;
        private GameObject _owner;
        private bool _stuck;
        private Vector3 _lastTipPos;

        void Awake() => _rb = GetComponent<Rigidbody>();

        // preferovaný typed overload
        public void Launch(Vector3 velocity, in DamageContext ctx, GameObject owner, float life, bool useGravity)
        {
            _ctx       = ctx;
            _owner     = owner;
            _life      = life;
            _spawnTime = Time.time;

            _rb.useGravity = useGravity;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = velocity;
#else
            _rb.velocity = velocity;
#endif
            Quaternion rot  = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            Quaternion corr = Quaternion.FromToRotation(modelForwardLocal.normalized, Vector3.forward);
            transform.rotation = rot * corr;

            _lastTipPos = GetTipWorld();
        }

        // legacy kompatibilita – NEpoužívám žádný DamageType
        public void Launch(Vector3 velocity, float damage, GameObject owner, float life, bool useGravity)
        {
            var simple = new DamageContext
            {
                amount = Mathf.Max(0f, damage),
                source = owner
                // ostatní pole necháme na default
            };
            Launch(velocity, in simple, owner, life, useGravity);
        }

        void FixedUpdate()
        {
            if (_stuck) return;

            Vector3 tipNow = GetTipWorld();
            Vector3 delta  = tipNow - _lastTipPos;
            float   dist   = delta.magnitude;

            if (dist > 1e-5f)
            {
                if (Physics.SphereCast(_lastTipPos, castRadius, delta.normalized, out RaycastHit hit, dist, hitMask, QueryTriggerInteraction.Collide))
                {
                    var hitGO = hit.rigidbody ? hit.rigidbody.gameObject : hit.collider.gameObject;
                    if (!_owner || hitGO != _owner)
                    {
                        ApplyDamage(hit.collider, hit.point, hit.normal);
                        StickTo(hit.transform, hit.point, hit.normal);
                        return;
                    }
                }
            }

            _lastTipPos = tipNow;
        }

        void Update()
        {
#if UNITY_6000_0_OR_NEWER
            var v = _rb.linearVelocity;
#else
            var v = _rb.velocity;
#endif
            if (!_stuck && _rb && v.sqrMagnitude > 0.01f)
            {
                Quaternion corr = Quaternion.FromToRotation(modelForwardLocal.normalized, Vector3.forward);
                transform.rotation = Quaternion.LookRotation(v.normalized, Vector3.up) * corr;
            }

            if (Time.time >= _spawnTime + _life)
                if (destroyOnNoHit || _stuck) Destroy(gameObject);
        }

        void OnCollisionEnter(Collision c)
        {
            if (_stuck) return;
            if ((hitMask.value & (1 << c.collider.gameObject.layer)) == 0) return;
            if (_owner && c.collider.transform.IsChildOf(_owner.transform)) return;

            Vector3 hitPoint, hitNormal;
            if (c.contactCount > 0)
            {
                var contact = c.GetContact(0);
                hitPoint = contact.point;
                hitNormal = contact.normal;
            }
            else
            {
                hitPoint = transform.position;
                hitNormal = Vector3.up;
            }

            ApplyDamage(c.collider, hitPoint, hitNormal);
            StickTo(c.collider.transform, hitPoint, hitNormal);
        }

        Vector3 GetTipWorld()
        {
            if (stickStop) return stickStop.position;
            return transform.position +
                   (transform.rotation *
                    (Quaternion.FromToRotation(Vector3.forward, modelForwardLocal) * Vector3.forward)) *
                   Mathf.Max(0.01f, fallbackPenetration);
        }

        void ApplyDamage(Collider col, Vector3 point, Vector3 normal)
        {
            if (_ctx.source == null) _ctx.source = _owner;
            if (_ctx.amount <= 0f)  _ctx.amount = 1f;
            TypedDamage.Apply(col, in _ctx, point, normal, false);
        }

        void StickTo(Transform hit, Vector3 point, Vector3 normal)
        {
            if (_stuck) return;
            _stuck = true;

#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = Vector3.zero;
#else
            _rb.velocity = Vector3.zero;
#endif
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _rb.detectCollisions = false;
            _rb.useGravity = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;

            Quaternion face = alignToSurfaceNormal
                ? Quaternion.LookRotation(-normal, Vector3.up)
                : Quaternion.LookRotation(transform.forward, Vector3.up);
            Quaternion corr = Quaternion.FromToRotation(modelForwardLocal.normalized, Vector3.forward);
            Quaternion rot  = face * corr;

            if (stickStop)
            {
                Vector3 rootPos = point - (rot * stickStop.localPosition);
                transform.SetPositionAndRotation(rootPos, rot);
            }
            else
            {
                Vector3 rootPos = point - rot * (Vector3.forward * fallbackPenetration);
                transform.SetPositionAndRotation(rootPos, rot);
            }

            if (parentToHit && hit) transform.SetParent(hit, true);
            Destroy(gameObject, Mathf.Max(0.1f, stickSeconds));
        }
    }
}
