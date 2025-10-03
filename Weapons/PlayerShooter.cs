using UnityEngine;
using Obscurus.Weapons;

namespace Obscurus.Player
{
    [DisallowMultipleComponent]
    public class PlayerShooter : MonoBehaviour
    {
        [Header("Runtime")]
        public WeaponHolder holder;

        void Awake()
        {
            holder = GetComponentInParent<WeaponHolder>();
            if (!holder) holder = gameObject.AddComponent<WeaponHolder>();
            holder.isPlayerHolder = true;
        }

        public void Fire(bool heavy = false)
        {
            var w = holder?.Current;
            if (w is RangedWeaponBase rw) rw.TryShoot();
            else if (w is MeleeWeaponBase mw) mw.TryAttack();
        }

        public void Reload() => (holder?.Current as RangedWeaponBase)?.BeginReload();

        public void SetAim(bool value) => (holder?.Current as RangedWeaponBase)?.SetAim(value);

        public void Next() => holder?.Next();
        public void Prev() => holder?.Prev();
        public void EquipSlot(int slotIndex) => holder?.EquipByIndex(slotIndex);
    }
}