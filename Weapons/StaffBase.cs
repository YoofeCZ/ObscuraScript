using UnityEngine;
using Obscurus.Player;

namespace Obscurus.Weapons
{
    /// Staff = SINGLE-SHOT:
    /// - klik → okamžitý výstřel (bez channelingu/overheatu)
    /// - cooldown řeší RangedWeaponBase přes fireCooldown AŽ po FireOneShot()
    /// - tady NESAHEJ na _cooldown
    public abstract class StaffBase : RangedWeaponBase
    {
        protected override void Awake()
        {
            base.Awake();
            // Použij JEDINÝ cooldown z parenta (fireCooldown po výstřelu).
            ignoreBaseCooldown = false;
            // Stafky typicky neberou kusové ammo; nech si dle potřeby.
            requireAmmo = false;
        }

        public override void OnEquip(WeaponHolder holder)
        {
            base.OnEquip(holder);

            // ✅ Důležité: první střela MUSÍ být hned → vynuluj cooldown na equipu
            _cooldown = 0f;

            // ✅ Zabráníme auto-reloadu (ten by blokoval IsReady==false kvůli IsReloading)
            autoReloadOnEquipIfEmpty = false;
        }

        /// Okamžitý výstřel. ŽÁDNÝ channeling, žádné tickování.
        protected sealed override void FireOneShot(Vector3 dir, float _ignoredDamage)
        {
            if (UseCameraAim && !aimCamera) aimCamera = Camera.main;
            dir = GetAimDirectionFromCameraCenter();

            FireImmediate(dir); // 💥 hned vystřel

            // ⚠️ _cooldown neřeš – parent (RangedWeaponBase.TryShoot) ho nastaví po návratu.
        }

        /// ⚠️ Důležité: TryShoot volá po float overloadu ještě typed overload.
        /// Abychom nestříleli 2×, typed verze je tady NO-OP. Typed context si řeší konkrétní
        /// staff weapon (např. MercuriusChainStaffWeapon) při spawnování projektilu.
        protected sealed override void FireOneShot(Vector3 dir, float damage, in Obscurus.Combat.DamageContext ctx)
        {
            if (UseCameraAim && !aimCamera) aimCamera = Camera.main;
            dir = GetAimDirectionFromCameraCenter();

            FireImmediate(dir); // 💥 okamžitý výstřel

            // cooldown řeší parent (RangedWeaponBase)
        }


        /// Implementuj v potomkovi: udělej reálný výstřel (projektil/raycast).
        protected abstract void FireImmediate(Vector3 dir);
    }
}
