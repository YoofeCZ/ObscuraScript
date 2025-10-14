using UnityEngine;
using UnityEngine.InputSystem; // ⬅️ kvůli Mouse.current
using Obscurus.Weapons;

namespace Obscurus.Player
{
    [DisallowMultipleComponent]
    public class PlayerShooter : MonoBehaviour
    {
        [Header("Runtime")]
        public WeaponHolder holder;

        /// <summary>True, pokud hráč právě drží tlačítko střelby (drží spoušť).</summary>
        public static bool IsFiring { get; private set; }

        void Awake()
        {
            holder = GetComponentInParent<WeaponHolder>();
            if (!holder) holder = gameObject.AddComponent<WeaponHolder>();
            holder.isPlayerHolder = true;
        }

        void Update()
        {
            // ✅ Sleduj levé tlačítko myši (Input System)
            if (Mouse.current != null)
            {
                IsFiring = Mouse.current.leftButton.isPressed;
            }
            else
            {
                // fallback pro starý Input manager
                IsFiring = Input.GetMouseButton(0);
            }

            // Pokud držíš spoušť, automaticky spouštěj střelbu
            if (IsFiring)
            {
                Fire();
            }
        }

        public void Fire(bool heavy = false)
        {
            var w = holder?.Current;
            if (w is RangedWeaponBase rw)
                rw.TryShoot();
            else if (w is MeleeWeaponBase mw)
                mw.TryAttack();
        }

        public void Reload() => (holder?.Current as RangedWeaponBase)?.BeginReload();

        public void SetAim(bool value) => (holder?.Current as RangedWeaponBase)?.SetAim(value);

        public void Next() => holder?.Next();
        public void Prev() => holder?.Prev();
        public void EquipSlot(int slotIndex) => holder?.EquipByIndex(slotIndex);
    }
}