using System;
using UnityEngine;
using Obscurus.Items;
using Obscurus.Player;
using ItemWeaponKind = Obscurus.Items.WeaponKind;

namespace Obscurus.Weapons
{
    public interface IDamageReceiver
    {
        void ApplyDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, GameObject source);
    }

    public class MeleeWeaponBase : MonoBehaviour, IWeapon
    {
        [Header("Definition & Links")]
        public ItemDefinition weaponDef;
        public PlayerInventory inventory;
        public ItemDatabase db;

        [Header("Aim")]
        [SerializeField] private bool useCameraAim = true;
        [SerializeField] private Camera aimCamera;

        [Header("Melee Setup")]
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private bool debugDraw;
        public float critMultiplier = 2f;

        [Header("Animator")]
        public Animator animator;
        [Tooltip("Trigger v Animatoru pro útok")]
        public string attackParam = "Attack";
        public int animatorLayer = 0;

        [Tooltip("Kdy aplikovat zásah: false = hned po stisku, true = až z Animation Eventu")]
        public bool hitOnAnimEvent = false;

        [Header("SFX")]
        public AudioSource audioSource;
        public AudioClip swingSfx, hitSfx;
        
        // do MeleeWeaponBase
        public bool TryFire() => TryAttack();   // alias kvůli stávajícímu vstupu


        float _cooldown;

        // queue pro eventové trefy
        bool _waitingForAnimHit;
        Ray  _queuedRay;
        float _queuedDamage;

        // IWeapon
        public WeaponKind Kind => WeaponKind.Melee;
        public ItemDefinition Definition => weaponDef;

        // ====== Staty z ItemDefinition (melee) + modifikátory ======
        private (float baseDamage, float attackCooldown, float critChance, float durability, float range, float radius) GetStats()
        {
            float baseDamage = 0f, attackCooldown = 0.5f, critChance = 0f, durability = 100f, range = 2f, radius = 0.25f;

            if (weaponDef != null && weaponDef.weaponKind == ItemWeaponKind.Melee && weaponDef.melee != null)
            {
                var m = weaponDef.melee;
                baseDamage     = m.baseDamage;
                attackCooldown = m.attackCooldown;
                critChance     = m.critChance;
                durability     = m.durability;
                range          = m.range;
                radius         = m.radius;
            }

            if (weaponDef != null && weaponDef.modifiers != null)
            {
                foreach (var mod in weaponDef.modifiers)
                {
                    var key = (mod.key ?? "").Trim().ToLowerInvariant();
                    var v   = mod.value;
                    switch (key)
                    {
                        case "damage":
                        case "basedamage": baseDamage += v; break;
                        case "damage_mult": baseDamage *= Mathf.Max(0f, v); break;

                        case "cooldown": attackCooldown += v; break;
                        case "cooldown_mult": attackCooldown *= Mathf.Max(0f, v); break;

                        case "crit":
                        case "critchance": critChance += v; break;

                        case "range": range += v; break;
                        case "range_mult": range *= Mathf.Max(0f, v); break;

                        case "radius": radius += v; break;
                        case "radius_mult": radius *= Mathf.Max(0f, v); break;
                    }
                }
            }

            baseDamage     = Mathf.Max(0f, baseDamage);
            attackCooldown = Mathf.Max(0.05f, attackCooldown);
            critChance     = Mathf.Clamp(critChance, 0f, 100f);
            range          = Mathf.Max(0f, range);
            radius         = Mathf.Max(0f, radius);

            return (baseDamage, attackCooldown, critChance, durability, range, radius);
        }

        // Helpers
        protected float BaseDamage     => GetStats().baseDamage;
        protected float AttackCooldown => GetStats().attackCooldown;
        protected float CritChance     => GetStats().critChance;
        protected float Range          => GetStats().range;
        protected float Radius         => GetStats().radius;

        void Awake()
        {
            if (!aimCamera && useCameraAim) aimCamera = Camera.main;
            if (!inventory) inventory = FindObjectOfType<PlayerInventory>(true);
            if (!db) db = FindObjectOfType<ItemDatabase>(true);
            if (!animator) animator = GetComponentInChildren<Animator>(true);
        }

        void OnEnable() { _cooldown = 0f; _waitingForAnimHit = false; }
        void Update() { if (_cooldown > 0f) _cooldown -= Time.deltaTime; }

        public void OnEquip(WeaponHolder holder)
        {
            if (!aimCamera && useCameraAim) aimCamera = Camera.main;
            if (!inventory) inventory = holder ? holder.GetComponentInParent<PlayerInventory>() : FindObjectOfType<PlayerInventory>(true);
            if (!db) db = FindObjectOfType<ItemDatabase>(true);
            if (!animator) animator = GetComponentInChildren<Animator>(true);
            gameObject.SetActive(true);
        }

        public void OnHolster()
        {
            _waitingForAnimHit = false;
            gameObject.SetActive(false);
        }

        public bool TryAttack()
        {
            if (_cooldown > 0f) return false;

            // SPUSTIT ANIMACI HNED
            if (animator && !string.IsNullOrEmpty(attackParam))
                animator.SetTrigger(attackParam);

            // Najdi kameru, ale neblokuj animaci, když chybí
            var cam = useCameraAim ? (aimCamera ? aimCamera : Camera.main) : null;
            Ray ray = cam
                ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : new Ray(transform.position, transform.forward);

            if (debugDraw) Debug.DrawRay(ray.origin, ray.direction * Range, Color.magenta, 0.2f);

            float damage = BaseDamage;
            bool isCrit = (UnityEngine.Random.value * 100f) < CritChance;
            if (isCrit) damage *= Mathf.Max(1f, critMultiplier);

            PlayOneShot(swingSfx);

            bool hitNow = false;
            if (hitOnAnimEvent)
            {
                _queuedRay = ray;
                _queuedDamage = damage;
                _waitingForAnimHit = true;
            }
            else
            {
                if (Physics.SphereCast(ray, Radius, out var hit, Range, hitMask, QueryTriggerInteraction.Ignore))
                {
                    ApplyDamage(hit, damage);
                    PlayOneShot(hitSfx);
                    hitNow = true;
                }
            }

            _cooldown = AttackCooldown;
            return hitNow || hitOnAnimEvent;
        }


        // Volá se buď hned, nebo z Animation Eventu
        void ApplyDamage(RaycastHit hit, float damage)
        {
            var go = hit.collider.attachedRigidbody ? hit.collider.attachedRigidbody.gameObject : hit.collider.gameObject;

            if (go.TryGetComponent<IDamageReceiver>(out var recv))
            {
                recv.ApplyDamage(damage, hit.point, hit.normal, gameObject);
                return;
            }

            var hp = go.GetComponentInParent<HealthSystem>();
            if (hp != null)
            {
                // hp.TakeDamage(damage, DamageType.Melee, gameObject);
            }

            Debug.Log($"[Melee] Hit {go.name} for {damage} dmg @ {hit.point}");
        }

        // === Animation Event hook ===
        // Přidej do klipu útoku animation event s názvem "Anim_DoMeleeHit"
        public void Anim_DoMeleeHit()
        {
            if (!_waitingForAnimHit) return;

            if (debugDraw) Debug.DrawRay(_queuedRay.origin, _queuedRay.direction * Range, Color.cyan, 0.2f);

            if (Physics.SphereCast(_queuedRay, Radius, out var hit, Range, hitMask, QueryTriggerInteraction.Ignore))
            {
                ApplyDamage(hit, _queuedDamage);
                PlayOneShot(hitSfx);
            }

            _waitingForAnimHit = false;
        }

        void PlayOneShot(AudioClip clip)
        {
            if (!clip) return;
            if (audioSource) audioSource.PlayOneShot(clip);
            else AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }
}
