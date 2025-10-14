// Assets/Obscurus/Scripts/Combat/DamageUtil.cs
using UnityEngine;
using Obscurus.Player; // pokud máš HealthSystem v Obscurus.Player; když ne, klidně ten using smaž

namespace Obscurus.Weapons
{
    public struct DamageMessage
    {
        public float amount;
        public GameObject source;
        public Vector3 point;
        public Vector3 normal;
        public bool isCrit;
    }

    // Volitelné unified rozhraní
    public interface IDamageable
    {
        void ApplyDamage(DamageMessage msg);
    }

    public static class DamageUtil
    {
        public static void DealDamage(Collider hitCol, float amount, GameObject source, Vector3 point, Vector3 normal, bool isCrit)
        {
            if (!hitCol) return;

            GameObject target = hitCol.attachedRigidbody ? hitCol.attachedRigidbody.gameObject : hitCol.gameObject;
            var msg = new DamageMessage { amount = amount, source = source, point = point, normal = normal, isCrit = isCrit };

            // 1) Prefer IDamageable
            if (target.TryGetComponent<IDamageable>(out var dmg))
            {
                dmg.ApplyDamage(msg);
                return;
            }

            // 2) Fallback: SendMessage (zachytí kdo chce – Hitbox, Health, cokoliv)
            target.SendMessage("ApplyDamage", msg, SendMessageOptions.DontRequireReceiver);

            // 3) (Volitelné) Pokud máš HealthSystem a chceš explicitně podpořit:
            // target.TryGetComponent<HealthSystem>(out var hs); // jen side-channel (nepovinné)
        }
    }
}