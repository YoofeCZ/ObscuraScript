using UnityEngine;
using UnityEngine.Pool;
using Obscurus.Core.Pooling;

namespace Obscurus.Weapons
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class PooledProjectile : MercuriusChainProjectile, IPoolable
    {
        public ObjectPool<PooledProjectile> ownerPool;

        private float despawnAt = -1f;
        private bool _isReleased = false;
        private Rigidbody _rb;
        private Collider _col;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>();

            // Vypni gravitaci pro rovný let
            if (_rb) _rb.useGravity = false;
        }

        // -------------------------------------------------------------
        // === LAUNCH ===
        // -------------------------------------------------------------
        public override void Launch(Vector3 dir)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();

            // Nastav směr letu
            transform.forward = dir.normalized;

            // Reset fyziky
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            // ✅ Nastav okamžitou rychlost (žádná gravitace, žádná síla)
            _rb.linearVelocity = dir.normalized * speed;

            despawnAt = Time.time + lifetime;
            _isReleased = false;
        }

        // -------------------------------------------------------------
        // === UPDATE / DESPAWN ===
        // -------------------------------------------------------------
        void Update()
        {
            if (!_isReleased && despawnAt > 0f && Time.time >= despawnAt)
                Despawn();
        }

        protected override void OnHitAndDone()
        {
            if (!_isReleased)
                Despawn();
        }

        private void Despawn()
        {
            if (_isReleased) return;
            _isReleased = true;

            if (_col) _col.enabled = false;

            if (_rb)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            despawnAt = -1f;
            ownerPool?.Release(this);
        }

        // -------------------------------------------------------------
        // === IPoolable rozhraní ===
        // -------------------------------------------------------------
        public void OnRent()
        {
            CancelInvoke();
            despawnAt = -1f;
            _isReleased = false;
            debug = false;

            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_col == null) _col = GetComponent<Collider>();

            // Reset fyziky
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            // Vypni gravitaci pro jistotu
            _rb.useGravity = false;

            gameObject.SetActive(true);
            if (_col) _col.enabled = true;
        }

        public void OnReturn()
        {
            gameObject.SetActive(false);

            if (_rb)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
