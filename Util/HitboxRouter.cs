// Assets/Obscurus/Scripts/AI/HitboxRouter.cs
using UnityEngine;
using Obscurus.Weapons;

namespace Obscurus.AI
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Obscurus/AI/Hitbox Router")]
    public class HitboxRouter : MonoBehaviour, IDamageReceiver, IDamageable
    {
        [Tooltip("Kam se přepošle damage. Pokud necháš prázdné, vezme se z parenta.")]
        public EnemyStats target;

        void Reset()
        {
            if (!target) target = GetComponentInParent<EnemyStats>();
        }

        // === Primární cesta (zbraně přes IDamageReceiver)
        public void ApplyDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, GameObject source)
        {
            if (!target) target = GetComponentInParent<EnemyStats>();
            if (target) target.ApplyDamage(amount, hitPoint, hitNormal, source);
        }

        // === Unified cesta (DamageUtil → IDamageable)
        public void ApplyDamage(DamageMessage msg)
        {
            ApplyDamage(msg.amount, msg.point, msg.normal, msg.source);
        }

        // === Fallback pro SendMessage s „něčím“ (třeba float, anonymní payload)
        public void ApplyDamage(object payload)
        {
            if (payload is DamageMessage m) { ApplyDamage(m); return; }
            if (payload is float f)        { ApplyDamage(f, transform.position, Vector3.up, null); return; }

            // Pokus o „vyzobání“ známých polí reflexí (když přijde custom typ)
            try
            {
                var t  = payload.GetType();
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
            catch { /* ignoruj */ }
        }
    }
}
