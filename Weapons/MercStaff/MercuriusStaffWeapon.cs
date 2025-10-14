using Obscurus.Player;
using UnityEngine;
using Obscurus.VFX;
using Obscurus.Core.Pooling;
using Obscurus.Combat;   // BuildRangedContext
using Obscurus.Effects;  // ElectrizedEffectDef
using Obscurus.Items;    // RangedWeaponData

namespace Obscurus.Weapons
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Obscurus/Weapons/Staffs/Mercurius Staff (Single Shot)")]
    public class MercuriusChainStaffWeapon : StaffBase, IHasImpactEffect
    {
        [Header("Projectile")]
        public PooledProjectile projectilePrefab;
        [Min(1f)]    public float projectileSpeed    = 200f;
        [Min(0.05f)] public float projectileLifetime = 3f;

        [Header("Aim/Spawn")]
        public Transform beamOrigin;
        [Range(0f, 10f)] public float spreadAngleDeg = 0.25f;

        [Header("FX / Impact")]
        public GameObject muzzleFlashPrefab;
        public GameObject hitEffectPrefab;

        [Header("Debug")]
        public bool projectileDebug = false;

        public override void OnEquip(WeaponHolder holder)
        {
            base.OnEquip(holder);

            if (beamOrigin) muzzle = beamOrigin;
            UseCameraAim = true;
            if (!aimCamera) aimCamera = Camera.main;

            // vynutit fireCooldown (ne 1/APS)
            if (weaponDef && weaponDef.ranged != null)
                weaponDef.ranged.attackSpeed = 0f;

            _cooldown = 0f;
            autoReloadOnEquipIfEmpty = false;
        }

        protected override void FireImmediate(Vector3 dir)
        {
            if (UseCameraAim && !aimCamera) aimCamera = Camera.main;

            Vector3 shotDir = ApplySpread(dir, spreadAngleDeg);
            SpawnOne(shotDir);
        }

        void SpawnOne(Vector3 dir)
        {
            if (!projectilePrefab)
            {
                Debug.LogError("[MercuriusChainStaffWeapon] Missing projectilePrefab!", this);
                return;
            }

            var origin = muzzle ? muzzle : (beamOrigin ? beamOrigin : transform);

            var proj = ProjectilePool.Get(projectilePrefab);
            proj.transform.SetPositionAndRotation(origin.position, Quaternion.LookRotation(dir));
            proj.speed    = projectileSpeed;
            proj.lifetime = projectileLifetime;
            proj.Init(gameObject);

            // === DB → projectile payload / typed context ===
            if (proj.TryGetComponent<MercuriusChainProjectile>(out var cmp))
            {
                cmp.debug = projectileDebug;

                var rd = weaponDef && weaponDef.ranged != null ? weaponDef.ranged : null;
                if (rd != null)
                {
                    // projectile ballistic (volitelně z DB)
                    if (rd.projectileSpeed    > 0f) cmp.speed          = rd.projectileSpeed;
                    if (rd.projectileLifetime > 0f) cmp.lifetime       = rd.projectileLifetime;

                    // AOE z DB
                    if (rd.aoeRadius       > 0f) cmp.aoeRadius       = rd.aoeRadius;
                    if (rd.aoeDuration     > 0f) cmp.aoeDuration     = rd.aoeDuration;
                    if (rd.aoeTickInterval > 0f) cmp.aoeTickInterval = rd.aoeTickInterval;

                    // Targeting
                    cmp.enemyMask = rd.aoeHitMask;
                    cmp.enemyTag  = string.IsNullOrEmpty(rd.aoeEnemyTag) ? cmp.enemyTag : rd.aoeEnemyTag;

                    // Typed AOE kontext: amount = DMG PER TICK
                    float tickDmg = Mathf.Max(0f, rd.aoeDamage);
                    var ctx = DamageTyping.BuildRangedContext(weaponDef, db, tickDmg, false, gameObject);
                    cmp.Init(gameObject, in ctx);

                    // --- DoT (Electrized) z DB (DPS -> per tick), zachovat stackování z template ---
                    var runtimeElec = BuildElectrizedFromDb(cmp.electrizedDef, rd);
                    if (runtimeElec)
                    {
                        cmp.electrizedDef = runtimeElec;
                        // kolik stacků přidat na AOE tick (většinou 1)
                        cmp.electrizedStacksPerTick = Mathf.Max(1, cmp.electrizedStacksPerTick);
                    }
                }
                else
                {
                    // fallback: aspoň typed kontext (amount=0)
                    var ctx = DamageTyping.BuildRangedContext(weaponDef, db, 0f, false, gameObject);
                    cmp.Init(gameObject, in ctx);
                }
            }

            // odpálit
            proj.Launch(dir);

            if (muzzleFlashPrefab && origin)
                VFXPool.SpawnOneShot(muzzleFlashPrefab, origin.position, origin.rotation, origin, 0.5f);
        }

        // DPS z DB -> přepočet na damagePerStack podle tick interval
        static ElectrizedEffectDef BuildElectrizedFromDb(ElectrizedEffectDef template, RangedWeaponData rd)
        {
            if (rd == null) return template;

            var inst = ScriptableObject.CreateInstance<ElectrizedEffectDef>();

            // --- přenos chování z templatu (aby fungovalo stackování) ---
            if (template)
            {
                inst.damageType             = template.damageType;
                inst.gainMode               = template.gainMode;               // OnHit / WhileInsideAoe
                inst.damageMode             = template.damageMode;             // AddPerStack / MultiplyFromBase
                inst.refreshDurationOnStack = template.refreshDurationOnStack;
                inst.maxStacks              = Mathf.Max(1, template.maxStacks);
                inst.baseTickDamage         = template.baseTickDamage;
                inst.multPerStack           = template.multPerStack;
                inst.onTargetVFX            = template.onTargetVFX;
                inst.duration               = template.duration;
                inst.tickInterval           = template.tickInterval;
                inst.damagePerStack         = template.damagePerStack;
            }
            else
            {
                // rozumné defaulty když není template
                inst.damageType             = Obscurus.Items.DamageType.Lightning;
                inst.gainMode               = StackGainMode.WhileInsideAoe;
                inst.damageMode             = StackDamageMode.AddPerStack;
                inst.refreshDurationOnStack = true;
                inst.maxStacks              = 10;
                inst.duration               = 5f;
                inst.tickInterval           = 1f;
            }

            // --- přepiš časování z DB, když je vyplněno ---
            if (rd.aoeTickInterval > 0f) inst.tickInterval = rd.aoeTickInterval;
            if (rd.aoeDotDuration  > 0f) inst.duration     = rd.aoeDotDuration;

            // --- DPS z DB -> per tick (pro AddPerStack) ---
            float dps = Mathf.Max(0f, rd.aoeDotDps);
            if (dps > 0f)
            {
                // u AddPerStack: tick = stacks * damagePerStack  -> damagePerStack = dps * tick
                inst.damageMode     = StackDamageMode.AddPerStack;
                inst.damagePerStack = dps * inst.tickInterval;
            }

            return inst;
        }

        static Vector3 ApplySpread(Vector3 forward, float angleDeg)
        {
            forward = forward.normalized;
            if (angleDeg <= 0f) return forward;

            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 off = Random.insideUnitCircle * Mathf.Tan(rad);

            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(up, forward)) > 0.99f) up = Vector3.right;

            Vector3 right  = Vector3.Cross(forward, up).normalized;
            Vector3 trueUp = Vector3.Cross(right, forward).normalized;

            return (forward + right * off.x + trueUp * off.y).normalized;
        }

        GameObject IHasImpactEffect.HitEffectPrefab => hitEffectPrefab;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (projectileSpeed    < 1f)    projectileSpeed    = 1f;
            if (projectileLifetime < 0.05f) projectileLifetime = 0.05f;
        }
#endif
    }
}
