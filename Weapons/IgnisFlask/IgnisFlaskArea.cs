using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obscurus.Combat;   // typed damage

namespace Obscurus.Weapons
{
    [DisallowMultipleComponent]
    public class IgnisFlaskArea : MonoBehaviour
    {
        [Header("Runtime payload (plní projektil/weapon)")]
        public GameObject owner;
        public float radius       = 3f;
        public float splashDamage = 30f;
        public float dotDps       = 8f;
        public float duration     = 5f;
        public bool  damageOwner  = true;
        public LayerMask hitMask  = ~0;

        [Header("DoT ticking")]
        [Min(0.05f)] public float dotTick = 0.20f;

        [Header("Vizuál (volitelné)")]
        public ParticleSystem flameFx;
        public AudioSource   loopSfx;

        [Header("Ground snap")]
        [Tooltip("Kde hledat zem při snappu (když láhev praskne na těle/ve vzduchu).")]
        public LayerMask groundMask = ~0;
        [Tooltip("O kolik nahoru z výchozí pozice začít raycast.")]
        public float probeUp = 0.75f;
        [Tooltip("Jak hluboko dolů raycastovat.")]
        public float probeDown = 6f;
        [Tooltip("Zarovnat rotaci podle normály země?")]
        public bool alignToGround = true;

        [Header("Časování účinku")]
        [Tooltip("Po kolika sekundách od dopadu se aplikuje splash dmg.")]
        [Min(0f)] public float splashDelay = 0f;

        [Header("Typed damage")]
        [Tooltip("Pokud true, area si sama postaví DamageContext z DB (weaponDef.ranged) a použije TypedDamage.")]
        public bool useTyped = true;

        // „Ignoruj“ tento transform při hledání země (abychom netrefili zasaženého enemáka)
        [System.NonSerialized] public Transform groundSnapIgnore;

        // --- interní stav ---
        bool _initialized;   // Init() již zavolán?
        bool _started;       // CoRun již běží?

        // cache potenciálních obětí
        readonly List<HealthSystem> _victimCache = new();

        // typed
        DamageContext _baseCtx;
        bool _typedReady;

        public void Init(GameObject ownerGO, float r, float splash, float dps, float dur, bool dmgOwner, LayerMask mask)
        {
            owner       = ownerGO;
            radius      = r;
            splashDamage= splash;
            dotDps      = dps;
            duration    = dur;
            damageOwner = dmgOwner;
            hitMask     = mask;

            _initialized = true;
            if (isActiveAndEnabled && !_started)
                Begin();    // spustíme až teď, kdy jsou payload hodnoty nastavené
        }

        void OnEnable()
        {
            if (duration <= 0f) duration = 3f;
            if (_initialized && !_started)
                Begin();
        }

        void Begin()
        {
            // 0) načti AOE parametry a typed kontext z DB/weapon, pokud je k dispozici
            TrySetupFromOwnerWeapon();

            // 1) přichytit na zem
            SnapToGround();

            // 2) připravit oběti a rozjet DoT
            BuildVictimCache();
            StartCoroutine(CoRun());
            _started = true;
        }

        void TrySetupFromOwnerWeapon()
        {
            if (!owner) return;

            // Najdi weapon, z ní vytáhni DB payload (ItemDefinition.ranged.*)
            var weapon = owner.GetComponentInParent<IgnisFlaskWeapon>();
            if (!weapon || !weapon.weaponDef || weapon.weaponDef.ranged == null) return;

            var rd = weapon.weaponDef.ranged;

            // Přepiš runtime payload hodnotami z DB (pokud dávají smysl)
            if (rd.aoeRadius        > 0f)  radius       = rd.aoeRadius;
            if (rd.aoeDuration      > 0f)  duration     = rd.aoeDuration;
            if (rd.aoeTickInterval  > 0f)  dotTick      = rd.aoeTickInterval;
            if (rd.aoeDamage        >= 0f) splashDamage = rd.aoeDamage;
            if (rd.aoeDotDps        >= 0f) dotDps       = rd.aoeDotDps;
            if (rd.aoeSplashDelay   >= 0f) splashDelay  = rd.aoeSplashDelay;

            // LayerMask z DB
            hitMask = rd.aoeHitMask;

            // Postav typed DamageContext (amount doplňujeme per-tick/splash)
            if (useTyped && weapon.db)
            {
                // amount = 0 → pokaždé doplníme aktuální
                _baseCtx = DamageTyping.BuildRangedContext(weapon.weaponDef, weapon.db, 0f, false, owner ? owner : weapon.gameObject);
                _typedReady = true;
            }
        }

        void SnapToGround()
        {
            Vector3 origin = transform.position + Vector3.up * Mathf.Max(0f, probeUp);
            float   dist   = Mathf.Max(0.01f, probeUp + probeDown);

            var hits = Physics.RaycastAll(origin, Vector3.down, dist, groundMask, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits)
                {
                    if (!h.collider) continue;

                    // přeskoč cokoliv, co patří objektu, do kterého to narazilo (např. enemák)
                    if (groundSnapIgnore && h.collider.transform.IsChildOf(groundSnapIgnore))
                        continue;

                    transform.position = h.point + h.normal * 0.02f;
                    if (alignToGround) transform.rotation = Quaternion.FromToRotation(Vector3.up, h.normal);
                    return;
                }
            }

            // fallback: nic jsme nenašli – nech původní pozici
        }

        void BuildVictimCache()
        {
            _victimCache.Clear();

            float r2 = radius * radius;

            // 1) Všichni HealthSystem ve scéně
            var allHS = FindObjectsOfType<HealthSystem>(includeInactive: false);
            foreach (var hs in allHS)
            {
                if (!hs) continue;
                var go = hs.gameObject;

                if (((1 << go.layer) & hitMask.value) == 0) continue;
                if ((go.transform.position - transform.position).sqrMagnitude > r2) continue;
                if (!damageOwner && owner && go.transform.IsChildOf(owner.transform)) continue;

                _victimCache.Add(hs);
            }

            // 2) Přidej kořeny z OverlapSphere
            var cols = Physics.OverlapSphere(transform.position, radius, hitMask, QueryTriggerInteraction.Ignore);
            foreach (var c in cols)
            {
                if (!c) continue;
                var root = c.attachedRigidbody ? c.attachedRigidbody.gameObject : c.gameObject;
                if (!root) continue;

                if (!damageOwner && owner && root.transform.IsChildOf(owner.transform)) continue;

                var hs = root.GetComponentInParent<HealthSystem>();
                if (hs != null && !_victimCache.Contains(hs))
                    _victimCache.Add(hs);
            }
        }

        IEnumerator CoRun()
        {
            // 🔸 1) Počkej než se spustí splash
            if (splashDelay > 0f)
                yield return new WaitForSeconds(splashDelay);

            // 🔸 2) Splash damage po zpoždění
            ApplyDamageToOverlap(splashDamage);

            // 🔸 3) Až po splashi začne DoT
            float t = 0f, acc = 0f;
            var wait = new WaitForFixedUpdate();
            while (t < duration)
            {
                t   += Time.fixedDeltaTime;
                acc += Time.fixedDeltaTime;

                if (acc >= dotTick && dotDps > 0f)
                {
                    float dmg = dotDps * acc;
                    ApplyDamageToOverlap(dmg);
                    acc = 0f;
                }

                yield return wait;
            }

            // 🔸 4) Stop efekty + zánik
            if (flameFx) flameFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (loopSfx) loopSfx.Stop();
            Destroy(gameObject);
        }

        void ApplyDamageToOverlap(float amount)
        {
            if (amount <= 0f) return;

            var cols = Physics.OverlapSphere(transform.position, radius, hitMask, QueryTriggerInteraction.Ignore);
            foreach (var c in cols)
            {
                if (!c) continue;

                if (!damageOwner && owner && c.transform.IsChildOf(owner.transform))
                    continue;

                Vector3 p = c.ClosestPoint(transform.position);
                Vector3 n = (p - transform.position).sqrMagnitude > 1e-4f ? (p - transform.position).normalized : Vector3.up;

                if (_typedReady && useTyped)
                {
                    var tick = _baseCtx;
                    tick.amount = amount;
                    TypedDamage.Apply(c, in tick, p, n, false);
                }
                else
                {
                    DamageUtil.DealDamage(c, amount, owner ? owner : gameObject, p, n, false);
                }
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.25f);
            Gizmos.DrawSphere(transform.position, radius);
        }
#endif
    }
}
