// Assets/Obscurus/Scripts/AI/EnemyStats.cs
using System;
using UnityEngine;
using UnityEngine.Events;
using Obscurus.Weapons; // kvůli IDamageReceiver, DamageMessage

namespace Obscurus.AI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Obscurus/AI/Enemy Stats")]
    public class EnemyStats : MonoBehaviour, IDamageReceiver, IDamageable
    {
        [Header("Pool")]
        [Min(1f)] public float maxHealth = 100f;
        [Min(0f)] public float maxArmor  = 50f;

        [SerializeField, Min(0f)] private float health;
        [SerializeField, Min(0f)] private float armor;

        [Header("Regen (optional)")]
        [Tooltip("Zpoždění po zásahu, po kterém začne lehká regenerace.")]
        public float regenDelay = 3f;
        [Tooltip("HP/s po regenDelay. 0 = vypnuto.")]
        public float healthRegenPerSec = 0f;
        [Tooltip("Armor/s po regenDelay. 0 = vypnuto.")]
        public float armorRegenPerSec = 0f;

        [Header("Události")]
        public UnityEvent<float, float> OnHealthChanged;   // (current, max)
        public UnityEvent<float, float> OnArmorChanged;    // (current, max)
        public UnityEvent OnDamaged;
        public UnityEvent OnDied;

        public bool IsDead { get; private set; }

        float lastHitTime;

        void OnEnable()
        {
            health = Mathf.Clamp(health <= 0f ? maxHealth : health, 0f, maxHealth);
            armor  = Mathf.Clamp(armor  <= 0f ? maxArmor  : armor,  0f, maxArmor);
            IsDead = false;

            OnHealthChanged?.Invoke(health, maxHealth);
            OnArmorChanged?.Invoke(armor, maxArmor);
        }

        void Update()
        {
            if (IsDead) return;

            // jednoduchá regenerace po čase
            if (regenDelay > 0f && Time.time - lastHitTime >= regenDelay)
            {
                if (healthRegenPerSec > 0f && health < maxHealth)
                {
                    health = Mathf.Min(maxHealth, health + healthRegenPerSec * Time.deltaTime);
                    OnHealthChanged?.Invoke(health, maxHealth);
                }
                if (armorRegenPerSec > 0f && armor < maxArmor)
                {
                    armor = Mathf.Min(maxArmor, armor + armorRegenPerSec * Time.deltaTime);
                    OnArmorChanged?.Invoke(armor, maxArmor);
                }
            }
        }

        public float Health01 => maxHealth > 0f ? health / maxHealth : 0f;
        public float Armor01  => maxArmor  > 0f ? armor  / maxArmor  : 0f;

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            health = Mathf.Min(maxHealth, health + amount);
            OnHealthChanged?.Invoke(health, maxHealth);
        }

        public void AddArmor(float amount)
        {
            if (IsDead || amount <= 0f) return;
            armor = Mathf.Min(maxArmor, armor + amount);
            OnArmorChanged?.Invoke(armor, maxArmor);
        }

        public void Kill()
        {
            if (IsDead) return;
            health = 0f;
            IsDead = true;
            OnHealthChanged?.Invoke(health, maxHealth);
            OnDied?.Invoke();
        }

        // ---- IDamageReceiver implementace (napojení na tvoje zbraně) ----
        public void ApplyDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, GameObject source)
        {
            if (IsDead || amount <= 0f) return;

            lastHitTime = Time.time;

            // nejdřív armor, zbytek do zdraví
            float dmgLeft = amount;

            if (armor > 0f)
            {
                float absorb = Mathf.Min(armor, dmgLeft);
                armor  -= absorb;
                dmgLeft -= absorb;
                OnArmorChanged?.Invoke(armor, maxArmor);
            }

            if (dmgLeft > 0f)
            {
                health = Mathf.Max(0f, health - dmgLeft);
                OnHealthChanged?.Invoke(health, maxHealth);
            }

            OnDamaged?.Invoke();

            if (health <= 0f)
                Kill();
        }

        // ---- IDamageable (unifikovaná cesta pro DamageUtil) ----
        public void ApplyDamage(DamageMessage msg)
        {
            ApplyDamage(msg.amount, msg.point, msg.normal, msg.source);
        }

        // ---- Fallback pro SendMessage("ApplyDamage", něco) z cizích systémů ----
        public void ApplyDamage(object payload)
        {
            if (payload is DamageMessage m) { ApplyDamage(m); return; }
            if (payload is float f)         { ApplyDamage(f, transform.position, Vector3.up, null); return; }

            // Pokus o vytažení známých polí reflexí (když přijde custom typ)
            try
            {
                var t = payload.GetType();
                float a = t.GetField("amount") != null ? (float)t.GetField("amount").GetValue(payload)
                        : t.GetField("damage") != null ? (float)t.GetField("damage").GetValue(payload) : 0f;
                Vector3 p = t.GetField("point") != null ? (Vector3)t.GetField("point").GetValue(payload)
                         : t.GetField("hitPoint") != null ? (Vector3)t.GetField("hitPoint").GetValue(payload) : transform.position;
                Vector3 n = t.GetField("normal") != null ? (Vector3)t.GetField("normal").GetValue(payload)
                         : t.GetField("hitNormal") != null ? (Vector3)t.GetField("hitNormal").GetValue(payload) : Vector3.up;
                GameObject s = t.GetField("source") != null ? (GameObject)t.GetField("source").GetValue(payload)
                            : t.GetField("owner")  != null ? (GameObject)t.GetField("owner").GetValue(payload)  : null;
                ApplyDamage(a, p, n, s);
            }
            catch
            {
                // ignoruj, když payload nedává smysl
            }
        }
    }
}
