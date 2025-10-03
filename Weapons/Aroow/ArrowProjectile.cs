using UnityEngine;

namespace Obscurus.Weapons
{
    /// Šíp: letí, ray/spherecastem chytá zásah a ZAPÍCHNE se hrotem do povrchu.
    [RequireComponent(typeof(Rigidbody))]
    public class ArrowProjectile : MonoBehaviour
    {
        [Header("Model orientation")]
        [Tooltip("Která LOKÁLNÍ osa modelu míří z ocasu k hrotu? (např. (0,1,0) když je šíp podél +Y).")]
        public Vector3 modelForwardLocal = Vector3.forward;        // <<< nastav podle svého modelu

        [Header("Sticking")]
        public Transform stickStop;                 // bod těsně za hrotem
        public float    fallbackPenetration = 0.06f;
        public bool     alignToSurfaceNormal = true;
        public bool     parentToHit = true;
        public LayerMask hitMask = ~0;

        [Header("Raycast pre-hit (proti odrazu)")]
        public float castRadius = 0.03f;           // můžeš doladit (tenčí -> 0.015)

        [Header("Lifetime")]
        public float stickSeconds = 5f;
        public bool  destroyOnNoHit = true;

        Rigidbody _rb;
        float _life, _spawnTime, _damage;
        GameObject _owner;
        bool _stuck;
        Vector3 _lastTipPos;

        void Awake() => _rb = GetComponent<Rigidbody>();

        public void Launch(Vector3 velocity, float damage, GameObject owner, float life, bool useGravity)
        {
            _owner     = owner;
            _damage    = damage;
            _life      = life;
            _spawnTime = Time.time;

            _rb.useGravity = useGravity;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.linearVelocity = velocity;

            // prvotní orientace podle rychlosti + korekce osy modelu  // <<<
            Quaternion rot = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            Quaternion corr = Quaternion.FromToRotation(modelForwardLocal.normalized, Vector3.forward);
            transform.rotation = rot * corr;

            _lastTipPos = GetTipWorld();
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
                        DoDamage(hitGO);
                        StickTo(hit.transform, hit.point, hit.normal);
                        return;
                    }
                }
            }

            _lastTipPos = tipNow;
        }

        void Update()
        {
            if (!_stuck && _rb && _rb.linearVelocity.sqrMagnitude > 0.01f)
            {
                // drž vizuál ve směru letu + korekce osy
                Quaternion corr = Quaternion.FromToRotation(modelForwardLocal.normalized, Vector3.forward);
                transform.rotation = Quaternion.LookRotation(_rb.linearVelocity.normalized, Vector3.up) * corr;
            }

            if (Time.time >= _spawnTime + _life)
                if (destroyOnNoHit || _stuck) Destroy(gameObject);
        }

        void OnCollisionEnter(Collision c)
        {
            if (_stuck) return;
            if ((hitMask.value & (1 << c.collider.gameObject.layer)) == 0) return;

            var hitGO = c.collider.attachedRigidbody ? c.collider.attachedRigidbody.gameObject : c.collider.gameObject;
            if (_owner && hitGO == _owner) return;

            DoDamage(hitGO);

            Vector3 point, normal;
            if (c.contactCount > 0) { var ct = c.GetContact(0); point = ct.point; normal = ct.normal; }
            else { point = c.collider.ClosestPoint(transform.position); normal = -(_rb.linearVelocity.sqrMagnitude > 0.001f ? _rb.linearVelocity.normalized : transform.forward); }

            StickTo(hitGO.transform, point, normal);
        }

        void OnTriggerEnter(Collider col)
        {
            if (_stuck) return;
            if ((hitMask.value & (1 << col.gameObject.layer)) == 0) return;
            if (_owner && col.attachedRigidbody && col.attachedRigidbody.gameObject == _owner) return;

            DoDamage(col.attachedRigidbody ? col.attachedRigidbody.gameObject : col.gameObject);

            Vector3 point  = col.ClosestPoint(transform.position);
            Vector3 normal = -(_rb.linearVelocity.sqrMagnitude > 0.001f ? _rb.linearVelocity.normalized : transform.forward);
            StickTo(col.attachedRigidbody ? col.attachedRigidbody.transform : col.transform, point, normal);
        }

        // === helpers ===
        Vector3 GetTipWorld()
        {
            if (stickStop) return stickStop.position;
            // bez stickStopu ber „hrot“ kousek před kořenem (podél lokálního „forwardu“ modelu)
            return transform.position + (transform.rotation * (Quaternion.FromToRotation(Vector3.forward, modelForwardLocal) * Vector3.forward)) * Mathf.Max(0.01f, fallbackPenetration);
        }

        void DoDamage(GameObject hitGO)
        {
            var hp = hitGO.GetComponentInParent<HealthSystem>();
            if (hp) hp.Damage(Mathf.Max(0f, _damage));
        }

        void StickTo(Transform hit, Vector3 point, Vector3 normal)
        {
            if (_stuck) return;
            _stuck = true;

            // tvrdé vypnutí fyziky i kolizí – žádný odraz
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _rb.detectCollisions = false;
            _rb.useGravity = false;
            foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;

            // výsledná rotace (hrot do povrchu) + korekce osy modelu
            Quaternion face = alignToSurfaceNormal
                ? Quaternion.LookRotation(-normal, Vector3.up)
                : Quaternion.LookRotation(((_rb.linearVelocity.sqrMagnitude > 0.001f) ? _rb.linearVelocity : transform.forward).normalized, Vector3.up);
            Quaternion corr = Quaternion.FromToRotation(modelForwardLocal.normalized, Vector3.forward);
            Quaternion rot  = face * corr;

            // tak aby stickStop seděl přesně v contact pointu
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
