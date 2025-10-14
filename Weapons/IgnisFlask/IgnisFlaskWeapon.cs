// Assets/Obscurus/Scripts/Weapons/IgnisFlask/IgnisFlaskWeapon.cs
using System.Collections;
using UnityEngine;

namespace Obscurus.Weapons
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Obscurus/Weapons/Ignis Flask (Throwable)")]
    public class IgnisFlaskWeapon : RangedWeaponBase
    {
        [Header("Throwable")]
        public IgnisFlaskProjectile flaskPrefab;
        public Transform overrideSpawn;
        [Min(0f)] public float throwForce = 12f;
        [Min(0f)] public float upwardForce = 2.5f;
        public int projectileLayer = -1;

        [Header("Impact/AoE (JEDINÝ zdroj pravdy)")]
        public GameObject aoePrefab;
        public float splashRadius    = 3f;
        public float splashDamage    = 30f;
        public float dotDamagePerSec = 8f;
        public float dotDuration     = 5f;
        public bool  damageOwner     = true;
        public LayerMask hitMask     = ~0;

        [Header("Hand Visual")]
        [Tooltip("Model láhve v ruce (child), který se při hodu schová a po chvíli znovu ukáže.")]
        public GameObject heldFlaskVisual;
        [Min(0f)] public float restockDelay = 0.25f;
        public string pullNewTrigger = "";

        [Header("Debug")]
        public bool debug = true;

        Coroutine _restockCo;

        protected override void FireOneShot(Vector3 dir, float _)
        {
            if (!flaskPrefab) { Debug.LogError("[IgnisFlaskWeapon] Missing flaskPrefab.", this); return; }
            var spawn = overrideSpawn ? overrideSpawn : muzzle;
            if (!spawn)       { Debug.LogError("[IgnisFlaskWeapon] Missing spawn/muzzle.", this); return; }

            // 🔸 Syncni AOE payload z DB (ItemDefinition.ranged.*)
            SyncAoEFromDatabase();

            // 1) skryj vizuál v ruce
            SetHeldVisible(false);

            // 2) instancuj projektil (NEparentovat do ruky)
            var flask = Instantiate(flaskPrefab, spawn.position, spawn.rotation);
            if (projectileLayer >= 0 && projectileLayer <= 31) flask.gameObject.layer = projectileLayer;

            // 3) nastav AoE prefab + hoď
            flask.aoePrefab  = aoePrefab;
            flask.impactMask = hitMask;
            flask.debug      = debug;
            flask.Throw(dir, gameObject, this, throwForce, upwardForce);

            // 4) po krátké době „nová“ láhev v ruce (nebo to udělej anim eventem)
            if (_restockCo != null) StopCoroutine(_restockCo);
            _restockCo = StartCoroutine(CoRestockHeld());

            if (debug)
                Debug.DrawRay(spawn.position, dir.normalized * throwForce + Vector3.up * upwardForce, Color.red, 1.0f, false);
        }

        void SyncAoEFromDatabase()
        {
            // Pokud je v definici zbraně AOE payload, bereme hodnoty z něj (DB = single source of truth)
            if (weaponDef && weaponDef.ranged != null)
            {
                var rd = weaponDef.ranged;

                // Aktivní jen pokud je AOE povoleno nebo má smysluplné hodnoty; necháme ale i bez flagu, když čísla dávají smysl
                if (rd.aoeRadius > 0f)            splashRadius    = rd.aoeRadius;
                if (rd.aoeDamage >= 0f)           splashDamage    = rd.aoeDamage;
                if (rd.aoeDotDps >= 0f)           dotDamagePerSec = rd.aoeDotDps;
                if (rd.aoeDotDuration >= 0f)      dotDuration     = rd.aoeDotDuration;
                // Layer mask & targeting
                hitMask = rd.aoeHitMask;
            }
        }

        IEnumerator CoRestockHeld()
        {
            if (!string.IsNullOrEmpty(pullNewTrigger) && animator && animatorUsesTriggers)
                animator.SetTrigger(pullNewTrigger);

            yield return new WaitForSeconds(restockDelay);
            SetHeldVisible(true);
        }

        void SetHeldVisible(bool v)
        {
            if (!heldFlaskVisual) return;

            // Nepoužívej root jako heldFlaskVisual – jinak neumí běžet korutiny/cooldown.
            if (heldFlaskVisual == gameObject)
            {
                foreach (var r in heldFlaskVisual.GetComponentsInChildren<Renderer>(true)) r.enabled = v;
                Debug.LogWarning("[IgnisFlaskWeapon] HeldFlaskVisual je root – vypínám jen rendery.", this);
            }
            else heldFlaskVisual.SetActive(v);

            if (animator && !string.IsNullOrEmpty(loadedParam))
                animator.SetBool(loadedParam, v);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetHeldVisible(true);
        }

        public override void OnHolster()
        {
            base.OnHolster();
            SetHeldVisible(false);
        }
    }
}
