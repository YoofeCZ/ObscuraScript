using System.Collections;
using System.Linq;
using System;
using UnityEngine;
using Obscurus.Items;
using Obscurus.Player;

namespace Obscurus.Weapons
{
    /// Základ střelných zbraní + Animator řízení (Fire/Reload).
    /// Publikuje IAmmoEvents.AmmoChanged → HUD reaguje hned (bez pollingu).
    public abstract class RangedWeaponBase : MonoBehaviour, IWeapon, IAmmoEvents
    {
        [Header("Definition & Links")]
        public ItemDefinition weaponDef;
        public PlayerInventory inventory;
        public ItemDatabase db;

        [Header("Setup")]
        [SerializeField] protected Transform muzzle;
        [SerializeField] private   bool   useCameraAim = true;
        [SerializeField] protected Camera aimCamera;

        [Header("Aim (screen center)")]
        [SerializeField] protected LayerMask aimRayMask = ~0;
        [SerializeField] protected float     aimRayMaxDistance = 1000f;
        [SerializeField] protected bool      aimDebug;

        [Header("Timing")]
        [Tooltip("Fallback prodleva mezi výstřely, pokud APS==0.")]
        public float fireCooldown = 0.15f;

        [Header("Combat Tunables")]
        [Tooltip("Násobič pro kritický zásah (damage * critMultiplier).")]
        public float critMultiplier = 2f;

        [Header("Debug / QoL")]
        public bool requireAmmo = true;
        public bool logReasons = true;

        [Header("SFX (optional)")]
        public AudioSource audioSource;
        public AudioClip fireSfx, emptySfx, reloadSfx;

        [Header("Animator")]
        public Animator animator;
        [Tooltip("Použít Triggery místo Boolů? (jinak Bool)")]
        public bool animatorUsesTriggers = false;
        [Tooltip("Názvy parametrů v Animatoru.")]
        public string fireParam = "Fire";
        public string reloadParam = "Reload";
        [Tooltip("Názvy stavů (pro detekci konce klipu).")]
        public string idleStateName   = "IdleReady";
        public string fireStateName   = "Firing";
        public string reloadStateName = "Reloading";
        public int animatorLayer = 0;

        [Header("Animator (Loaded state)")]
        [Tooltip("Bool parametr v Animatoru, který říká zda je náboj/bolt vizuálně naložen.")]
        public string loadedParam = "Loaded";
        [Tooltip("Máme Animator param 'loadedParam' nastavovat automaticky?")]
        public bool driveLoadedParam = true;

        [Tooltip("Volitelně: při equipu, když je prázdno a je rezerva, automaticky začít reload.")]
        public bool autoReloadOnEquipIfEmpty = false;

        // === Perks ===
        protected AlchemyPerks perks;

        // === Vitriol hook tunables (můžeš ladit v inspektoru) ===
        [Header("Vitriol Hooks (optional)")]
        public float vitriolArmorShred = 10f;   // kolik ubrat z armoru
        public float vitriolWeakSpotSec = 3f;   // délka odhalení weak-spotu
        public float vitriolDotDps = 5f;        // DPS pro DOT
        public float vitriolDotSec = 4f;        // délka DOTu
        public float vitriolAcidSec = 3f;       // délka acid poolu

        // === interní stav ===
        protected float _cooldown;
        protected int   _inMagazine;
        private bool _appliedStartLoaded = false;

        // === IAmmoEvents ===
        public event System.Action AmmoChanged;

        // Reload stav
        public bool IsReloading { get; private set; }
        Coroutine _reloadCo;

        // Animator coroutines
        Coroutine _fireAnimCo;
        Coroutine _reloadAnimCo;

        // ====== Staty – čtené primárně z ranged, fallback weapon, + modifiers ======
        private (float baseDamage, float attackSpeed, float critChance, int magazineSize, float reloadSeconds, int startLoaded, int shotsPerUse, string ammoKey) GetStats()
        {
            float baseDamage = 0f, attackSpeed = 0f, critChance = 0f, reloadSeconds = 0.25f;
            int magazineSize = 0, startLoaded = 0, shotsPerUse = 1;
            string ammoKey = null;

            if (weaponDef != null)
            {
                // Primárně nový payload
                if (weaponDef.weaponKind == Obscurus.Items.WeaponKind.Ranged && weaponDef.ranged != null)
                {
                    var r = weaponDef.ranged;
                    baseDamage   = r.baseDamage;
                    attackSpeed  = r.attackSpeed;
                    critChance   = r.critChance;
                    magazineSize = r.magazineSize;
                    reloadSeconds= r.reloadSeconds;
                    startLoaded  = r.startLoaded;
                    shotsPerUse  = Mathf.Max(1, r.shotsPerUse);
                    ammoKey      = string.IsNullOrEmpty(r.ammoKey) ? null : r.ammoKey;
                }
                // Fallback na legacy
                else if (weaponDef.weapon != null)
                {
                    var w = weaponDef.weapon;
                    baseDamage   = w.baseDamage;
                    attackSpeed  = w.attackSpeed;
                    critChance   = w.critChance;
                    magazineSize = w.magazineSize;
                    reloadSeconds= w.reloadSeconds;
                    startLoaded  = w.startLoaded;
                    shotsPerUse  = Mathf.Max(1, w.shotsPerUse);
                    ammoKey      = string.IsNullOrEmpty(w.ammoKey) ? null : w.ammoKey;
                }

                // Modifikátory (add/mult)
                if (weaponDef.modifiers != null)
                {
                    foreach (var m in weaponDef.modifiers)
                    {
                        var key = (m.key ?? "").Trim().ToLowerInvariant();
                        var v   = m.value;
                        switch (key)
                        {
                            // damage
                            case "damage":
                            case "basedamage": baseDamage += v; break;
                            case "damage_mult":
                            case "damagemult": baseDamage *= Mathf.Max(0f, v); break;

                            // attack speed (APS)
                            case "attackspeed": attackSpeed += v; break;
                            case "attackspeed_mult":
                            case "attackspeedmult": attackSpeed *= Mathf.Max(0f, v); break;

                            // crit
                            case "crit":
                            case "critchance": critChance += v; break;

                            // magazine
                            case "magazine":
                            case "magazinesize": magazineSize = Mathf.Max(0, magazineSize + Mathf.RoundToInt(v)); break;
                            case "magazine_mult":
                            case "magazinesize_mult": magazineSize = Mathf.Max(0, Mathf.RoundToInt(magazineSize * Mathf.Max(0f, v))); break;

                            // reload (s)
                            case "reload":
                            case "reloadsec": reloadSeconds = Mathf.Max(0.05f, reloadSeconds + v); break;
                            case "reload_mult":
                            case "reloadsec_mult": reloadSeconds = Mathf.Max(0.05f, reloadSeconds * Mathf.Max(0f, v)); break;

                            // shots/use
                            case "shotsperuse": shotsPerUse = Mathf.Max(1, shotsPerUse + Mathf.RoundToInt(v)); break;

                            // start loaded
                            case "startloaded": startLoaded = Mathf.Max(0, startLoaded + Mathf.RoundToInt(v)); break;
                        }
                    }
                }
            }

            // Limity + defaulty
            baseDamage    = Mathf.Max(0f, baseDamage);
            attackSpeed   = Mathf.Max(0f, attackSpeed);
            critChance    = Mathf.Clamp(critChance, 0f, 100f);
            shotsPerUse   = Mathf.Max(1, shotsPerUse);
            if (reloadSeconds <= 0f) reloadSeconds = 0.25f;
            startLoaded = Mathf.Clamp(startLoaded, 0, magazineSize > 0 ? magazineSize : startLoaded);

            return (baseDamage, attackSpeed, critChance, magazineSize, reloadSeconds, startLoaded, shotsPerUse, ammoKey);
        }

        // ==== Helpers z definice (používají GetStats) ====
        protected string AmmoKey       { get { var s = GetStats(); return s.ammoKey; } }
        protected int    MagazineSize  { get { var s = GetStats(); return s.magazineSize; } }
        protected int    ShotsPerUse   { get { var s = GetStats(); return s.shotsPerUse; } }
        protected float  ReloadSeconds { get { var s = GetStats(); return s.reloadSeconds; } }
        protected float  BaseDamage    { get { var s = GetStats(); return s.baseDamage; } }
        protected float  AttackSpeed   { get { var s = GetStats(); return s.attackSpeed; } }
        protected float  CritChance    { get { var s = GetStats(); return s.critChance; } }
        protected int    StartLoaded   { get { var s = GetStats(); return s.startLoaded; } }

        public bool HasMagazine => MagazineSize > 0;
        public int  InMagazine  => HasMagazine ? _inMagazine : 0;
        public bool IsReady     => _cooldown <= 0.0001f && !IsReloading;

        public bool UseCameraAim { get => useCameraAim; set => useCameraAim = value; }
        public void SetAim(bool enabled) => useCameraAim = enabled;

        // ===== IWeapon =====
        public WeaponKind Kind => WeaponKind.Ranged;
        public ItemDefinition Definition => weaponDef;

        // Je zbraň vizuálně "naložená"? (pro magazín: máme dost nábojů na další výstřel)
        bool IsVisuallyLoaded()
        {
            if (!HasMagazine) return false;
            return _inMagazine >= Mathf.Max(1, ShotsPerUse);
        }

        void UpdateLoadedVisual(bool? force = null)
        {
            if (!driveLoadedParam || !animator || string.IsNullOrEmpty(loadedParam)) return;
            bool v = force ?? IsVisuallyLoaded();
            animator.SetBool(loadedParam, v);
        }

        public virtual void OnEquip(WeaponHolder holder)
        {
            if (!perks)
            {
                // přes holder/inventory/parent – co najde dřív
                perks = GetComponentInParent<AlchemyPerks>()
                        ?? (inventory ? inventory.GetComponent<AlchemyPerks>() : null)
                        ?? FindObjectOfType<AlchemyPerks>(true);
            }
            if (!aimCamera && UseCameraAim) aimCamera = Camera.main;
            if (!inventory) inventory = holder ? holder.GetComponentInParent<PlayerInventory>() : FindObjectOfType<PlayerInventory>(true);
            if (!db)        db        = FindObjectOfType<ItemDatabase>(true);
            if (!animator)  animator  = GetComponentInChildren<Animator>(true);

            // autodetekuj "Muzzle"
            if (!muzzle)
            {
                var t = GetComponentsInChildren<Transform>(true).FirstOrDefault(x => string.Equals(x.name, "Muzzle", StringComparison.OrdinalIgnoreCase));
                if (t) muzzle = t;
            }

            gameObject.SetActive(true);
            ResetAnimatorFlags();       // čisté flagy
            RaiseAmmoChanged();         // pošli stav do HUDu
        }

        public virtual void OnHolster()
        {
            if (IsReloading && _reloadCo != null)
            {
                StopCoroutine(_reloadCo);
                IsReloading = false;
                _reloadCo = null;
            }
            if (_fireAnimCo   != null) { StopCoroutine(_fireAnimCo);   _fireAnimCo = null; }
            if (_reloadAnimCo != null) { StopCoroutine(_reloadAnimCo); _reloadAnimCo = null; }
            ResetAnimatorFlags();
            gameObject.SetActive(false);
        }

        protected virtual void Awake()
        {
            if (!aimCamera && useCameraAim) aimCamera = Camera.main;
            if (!inventory) inventory = FindObjectOfType<PlayerInventory>(true);
            if (!db) db = FindObjectOfType<ItemDatabase>(true);
            if (!animator) animator = GetComponentInChildren<Animator>(true);

            if (!muzzle)
            {
                var t = GetComponentsInChildren<Transform>(true).FirstOrDefault(x => string.Equals(x.name, "Muzzle", StringComparison.OrdinalIgnoreCase));
                if (t) muzzle = t;
            }
        }

        protected virtual void OnEnable()
        {
            // Použij startLoaded jen PŘI PRVNÍM enable (po instanci/pickupu).
            if (HasMagazine)
            {
                if (!_appliedStartLoaded)
                {
                    _inMagazine = Mathf.Clamp(StartLoaded, 0, MagazineSize);
                    _appliedStartLoaded = true;
                }
                else
                {
                    _inMagazine = Mathf.Clamp(_inMagazine, 0, MagazineSize);
                }
            }
            else
            {
                _inMagazine = 0;
            }

            UpdateLoadedVisual();

            // volitelně: autoreload při equipu
            if (autoReloadOnEquipIfEmpty 
                && HasMagazine 
                && _inMagazine < ShotsPerUse 
                && inventory != null 
                && !string.IsNullOrEmpty(AmmoKey) 
                && inventory.GetAmmoReserve(AmmoKey) > 0)
            {
                BeginReload();
            }

            RaiseAmmoChanged();
        }

        protected virtual void Update()
        {
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;
        }

        public int GetReserve()
        {
            if (string.IsNullOrEmpty(AmmoKey) || inventory == null) return 0;
            return inventory.GetAmmoReserve(AmmoKey);
        }

        // === RELOAD (časem) ===
        public bool BeginReload()
        {
            if (!HasMagazine) return Fail("No magazine.");
            if (IsReloading)  return false;
            if (inventory == null || string.IsNullOrEmpty(AmmoKey)) return Fail("No inventory/ammo key.");
            if (_inMagazine >= MagazineSize) return false;
            if (inventory.GetAmmoReserve(AmmoKey) <= 0) return false;

            _reloadCo = StartCoroutine(CoReload());
            StartReloadAnimation();
            return true;
        }

        IEnumerator CoReload()
        {
            IsReloading = true;

            // (volitelné) během reloadu ukaž prázdno:
            UpdateLoadedVisual(false);

            float t = 0f;
            float effReload = ReloadSeconds * (perks ? perks.TimeMult_QuickHands : 1f);
            while (t < effReload) { t += Time.deltaTime; yield return null; }

            int need  = Mathf.Max(0, MagazineSize - _inMagazine);
            int taken = (inventory != null && !string.IsNullOrEmpty(AmmoKey))
                ? inventory.TakeAmmo(AmmoKey, need)
                : 0;

            if (taken > 0)
            {
                _inMagazine += taken;
                PlayOneShot(reloadSfx);
                RaiseAmmoChanged();
                UpdateLoadedVisual(); // hotovo, ukaž „loaded“ pokud je co střílet
            }

            IsReloading = false;
            _reloadCo = null;
        }

        // === VÝSTŘEL ===
        public bool TryShoot()
        {
            if (weaponDef == null) return Fail("Missing weaponDef.");
            if (muzzle == null)    return Fail("Missing muzzle.");
            if (!IsReady)          return Fail(IsReloading ? "Reloading." : "Cooldown.");

            int shots   = ShotsPerUse;
            string aKey = AmmoKey;

            if (HasMagazine)
            {
                if (requireAmmo)
                {
                    if (_inMagazine < shots) { PlayOneShot(emptySfx); return Fail("Empty magazine."); }
                    _inMagazine -= shots;
                    RaiseAmmoChanged();
                    UpdateLoadedVisual(); // po výstřelu nastavíme Loaded=false, pokud došlo
                }
            }
            else
            {
                if (requireAmmo && !string.IsNullOrEmpty(aKey))
                {
                    if (!inventory) inventory = FindObjectOfType<PlayerInventory>(true);
                    if (!inventory) return Fail("Missing inventory.");
                    int reserve = inventory.GetAmmoReserve(aKey);
                    if (reserve < shots) { PlayOneShot(emptySfx); return Fail($"Not enough '{aKey}' (have {reserve}, need {shots})."); }
                    inventory.TakeAmmo(aKey, shots);
                    RaiseAmmoChanged();
                }
            }

            if (useCameraAim && !aimCamera) aimCamera = Camera.main;

            // směr: do středu obrazovky
            Vector3 dir = GetAimDirectionFromCameraCenter();

            // DAMAGE + AMMO MULT
            float damage = BaseDamage;
            if (!string.IsNullOrEmpty(aKey) && db != null)
            {
                var ammoDef = db.FindAmmoByKey(aKey);
                if (ammoDef != null && ammoDef.ammo != null)
                    damage *= Mathf.Max(0f, ammoDef.ammo.damageMultiplier);
            }

            // CRIT
            bool isCrit = (UnityEngine.Random.value * 100f) < CritChance;
            if (isCrit) damage *= Mathf.Max(1f, critMultiplier);

            FireOneShot(dir, damage);

            // COOLDOWN: APS → 1/APS; fallback na fireCooldown
            float cd = fireCooldown;
            if (AttackSpeed > 0.001f) cd = 1f / AttackSpeed;

            _cooldown = Mathf.Max(0.01f, cd);

            PlayOneShot(fireSfx);
            StartFireAnimation();

            return true;
        }

        /// směr z MUZZLE do bodu, kam míří střed obrazovky
        protected Vector3 GetAimDirectionFromCameraCenter()
        {
            if (aimCamera)
            {
                var ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                Vector3 aimPoint;

                if (Physics.Raycast(ray, out RaycastHit hit, aimRayMaxDistance, aimRayMask, QueryTriggerInteraction.Ignore))
                    aimPoint = hit.point;
                else
                    aimPoint = ray.GetPoint(aimRayMaxDistance);

                // když je cíl příliš blízko → střílej rovně z kamery
                if (Vector3.Distance(aimCamera.transform.position, aimPoint) < 1f)
                    return aimCamera.transform.forward;

                // jinak směr od muzzle na aimPoint
                return (aimPoint - muzzle.position).normalized;
            }

            return muzzle.forward;
        }


        /// Implementuj v potomkovi: vystřel projektil / raycast, a po zásahu zavolej Perk_OnHit(...)
        protected abstract void FireOneShot(Vector3 dir, float damage);

        // ===== Vitriol helper: volej po zásahu cíle (z projektilu/raycastu) =====
        protected void Perk_OnHit(GameObject target, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (!perks || !target) return;

            if (perks.corrosionTag && target.TryGetComponent<IArmorShredReceiver>(out var shred))
                shred.ApplyArmorShred(vitriolArmorShred, gameObject);

            if (perks.weakSpotGlow && target.TryGetComponent<IWeakSpotRevealReceiver>(out var weak))
                weak.RevealWeakSpot(vitriolWeakSpotSec, gameObject);

            if (perks.pinprickDot && target.TryGetComponent<IDotReceiver>(out var dot))
                dot.ApplyDot(vitriolDotDps, vitriolDotSec, gameObject);

            if (perks.lingeringAcid && target.TryGetComponent<IAcidPoolReceiver>(out var acid))
                acid.LeaveAcidPool(hitPoint, vitriolAcidSec, gameObject);
        }

        // ===== Animator helpers =====
        void StartFireAnimation()
        {
            if (!animator || string.IsNullOrEmpty(fireParam)) return;
            if (_fireAnimCo != null) StopCoroutine(_fireAnimCo);

            if (animatorUsesTriggers) animator.SetTrigger(fireParam);
            else animator.SetBool(fireParam, true);

            _fireAnimCo = StartCoroutine(CoResetParamWhenStateEnds(fireParam, fireStateName));
        }

        void StartReloadAnimation()
        {
            if (!animator || string.IsNullOrEmpty(reloadParam)) return;
            if (_reloadAnimCo != null) StopCoroutine(_reloadAnimCo);

            if (animatorUsesTriggers) animator.SetTrigger(reloadParam);
            else animator.SetBool(reloadParam, true);

            _reloadAnimCo = StartCoroutine(CoResetParamWhenStateEnds(reloadParam, reloadStateName));
        }

        IEnumerator CoResetParamWhenStateEnds(string param, string stateName)
        {
            yield return null;
            float safety = 5f;
            while (safety > 0f && animator && !IsInState(stateName))
            {
                safety -= Time.deltaTime;
                yield return null;
            }

            safety = 10f;
            while (safety > 0f && animator && IsInState(stateName))
            {
                var info = animator.GetCurrentAnimatorStateInfo(animatorLayer);
                if (info.normalizedTime >= 0.99f && !info.loop) break;
                safety -= Time.deltaTime;
                yield return null;
            }

            if (animator && !animatorUsesTriggers)
                animator.SetBool(param, false);
        }

        bool IsInState(string stateName)
        {
            if (!animator || string.IsNullOrEmpty(stateName)) return false;
            return animator.GetCurrentAnimatorStateInfo(animatorLayer).IsName(stateName);
        }

        void ResetAnimatorFlags()
        {
            if (!animator) return;
            if (!animatorUsesTriggers)
            {
                if (!string.IsNullOrEmpty(fireParam))   animator.SetBool(fireParam, false);
                if (!string.IsNullOrEmpty(reloadParam)) animator.SetBool(reloadParam, false);
            }
        }

        // ===== events / sfx =====
        protected void RaiseAmmoChanged() => AmmoChanged?.Invoke();

        protected bool Fail(string reason)
        {
            if (logReasons) Debug.LogWarning($"[{GetType().Name}] Cannot fire: {reason}", this);
            return false;
        }

        protected void PlayOneShot(AudioClip clip)
        {
            if (!clip) return;
            if (audioSource) audioSource.PlayOneShot(clip);
            else AudioSource.PlayClipAtPoint(clip, muzzle ? muzzle.position : transform.position);
        }
    }
}
