using System.Collections.Generic;
using UnityEngine;
using Obscurus.VFX;
using Obscurus.Combat;

namespace Obscurus.Effects
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public class ShockField : MonoBehaviour
    {
        [Header("Gameplay")]
        public float duration = 3f;
        public float radius = 6f;
        public float tickInterval = 0.25f;
        public float aoeDamage = 20f; // legacy only

        [Header("Electrized")]
        public ElectrizedEffectDef electrizedDef;
        public int stacksPerTick = 1;

        [Header("Targeting")]
        public LayerMask enemyMask = ~0;
        public string enemyTag = "Enemy";

        [Header("VFX (volitelné)")]
        public GameObject fieldVFXPrefab;

        float _endAt;
        SphereCollider _trigger;
        PooledVFX _fieldVFX;
        readonly HashSet<EffectCollector> _inside = new();

        // typed damage
        bool _typed;
        DamageContext _baseCtx; // amount = dmg per tick

        // původce (kvůli kreditu/threatu pro DoT)
        public GameObject sourceOwner;

        public bool IsAlive => Time.time < _endAt;
        public float SecondsLeft => Mathf.Max(0f, _endAt - Time.time);

        // ===== Legacy API =====
        public void Setup(
            float duration, float radius, float tickInterval, float aoeDamage,
            ElectrizedEffectDef electrizedDef, int stacksPerTick,
            LayerMask enemyMask, string enemyTag,
            GameObject fieldVFXPrefab,
            Vector3 groundNormal)
        {
            this.duration = duration;
            this.radius = radius;
            this.tickInterval = tickInterval;
            this.aoeDamage = aoeDamage;
            this.electrizedDef = electrizedDef;
            this.stacksPerTick = Mathf.Max(0, stacksPerTick);
            this.enemyMask = enemyMask;
            this.enemyTag = enemyTag;
            if (fieldVFXPrefab) this.fieldVFXPrefab = fieldVFXPrefab;

            _typed = false;
            sourceOwner = null;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
        }

        // ===== Typed API =====
        public void SetupTyped(
            float duration, float radius, float tickInterval, in DamageContext baseCtx,
            ElectrizedEffectDef electrizedDef, int stacksPerTick,
            LayerMask enemyMask, string enemyTag,
            GameObject fieldVFXPrefab,
            Vector3 groundNormal)
        {
            this.duration = duration;
            this.radius = radius;
            this.tickInterval = tickInterval;
            this.electrizedDef = electrizedDef;
            this.stacksPerTick = Mathf.Max(0, stacksPerTick);
            this.enemyMask = enemyMask;
            this.enemyTag = enemyTag;
            if (fieldVFXPrefab) this.fieldVFXPrefab = fieldVFXPrefab;

            _typed   = true;
            _baseCtx = baseCtx;
            sourceOwner = baseCtx.source ? baseCtx.source : null;

            transform.rotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
        }

        void Awake()
        {
            _trigger = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;
        }

        void OnEnable()
        {
            _endAt = Time.time + duration;
            if (_trigger) _trigger.radius = radius;
            if (fieldVFXPrefab)
                _fieldVFX = VFXPool.SpawnLoop(fieldVFXPrefab, transform.position, transform.rotation, transform);

            // splash = první tick hned
            DoTick();
            InvokeRepeating(nameof(DoTick), tickInterval, tickInterval);
        }

        void OnDisable()
        {
            CancelInvoke(nameof(DoTick));

            foreach (var col in _inside)
                if (col) col.NotifyExitDome(this);
            _inside.Clear();

            if (_fieldVFX) VFXPool.Release(_fieldVFX);
            _fieldVFX = null;
        }

        void Update()
        {
            if (!IsAlive)
            {
                CancelInvoke(nameof(DoTick));
                Destroy(gameObject);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsEnemy(other)) return;

            var root = other.attachedRigidbody ? other.attachedRigidbody.transform.root : other.transform.root;
            var col = root.GetComponent<EffectCollector>();
            if (!col) return;

            if (_inside.Add(col))
                col.NotifyEnterDome(this);
        }

        void OnTriggerExit(Collider other)
        {
            if (!IsEnemy(other)) return;

            var root = other.attachedRigidbody ? other.attachedRigidbody.transform.root : other.transform.root;
            var col = root.GetComponent<EffectCollector>();
            if (!col) return;

            if (_inside.Remove(col))
                col.NotifyExitDome(this);
        }

        bool IsEnemy(Collider c)
        {
            var go = c.attachedRigidbody ? c.attachedRigidbody.gameObject : c.gameObject;
            if (!string.IsNullOrEmpty(enemyTag) && !go.CompareTag(enemyTag)) return false;
            return ((enemyMask.value & (1 << go.layer)) != 0);
        }

        void DoTick()
        {
            if (!IsAlive) return;

            if (_typed)
                ApplyTypedAmountToOverlap(_baseCtx, _baseCtx.amount); // amount = dmg per tick
            else
                ApplyFloatAmountToOverlap(aoeDamage);
        }

        // legacy (bez typů) – převedu na simple DamageContext
        void ApplyFloatAmountToOverlap(float amount)
        {
            if (amount <= 0f) return;
            var cols = Physics.OverlapSphere(transform.position, radius, enemyMask, QueryTriggerInteraction.Ignore);
            foreach (var c in cols)
            {
                if (!c) continue;
                Vector3 p = c.ClosestPoint(transform.position);
                Vector3 n = (p - transform.position).sqrMagnitude > 1e-4f ? (p - transform.position).normalized : Vector3.up;

                var simple = new DamageContext { amount = amount, source = sourceOwner ? sourceOwner : gameObject };
                TypedDamage.Apply(c, in simple, p, n, false);
            }
        }

        void ApplyTypedAmountToOverlap(in DamageContext baseCtx, float amount)
        {
            if (amount <= 0f) return;

            var cols = Physics.OverlapSphere(transform.position, radius, enemyMask, QueryTriggerInteraction.Ignore);
            foreach (var c in cols)
            {
                if (!c) continue;

                Vector3 p = c.ClosestPoint(transform.position);
                Vector3 n = (p - transform.position).sqrMagnitude > 1e-4f ? (p - transform.position).normalized : Vector3.up;

                var tick = baseCtx;
                tick.amount = amount; // jediné, co měníme per-tick
                if (!tick.source) tick.source = sourceOwner ? sourceOwner : gameObject;
                TypedDamage.Apply(c, in tick, p, n, false);
            }
        }
    }
}
