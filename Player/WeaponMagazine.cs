using UnityEngine;
using Obscurus.Items;

namespace Obscurus.Player
{
    /// <summary>
    /// Drží stav zásobníku a provádí reload z rezerv hráče.
    /// Pokud magazineSize == 0, střelba bere munici přímo z rezerv.
    /// </summary>
    public class WeaponMagazine : MonoBehaviour
    {
        [Header("Vstupy")]
        public ItemDefinition weaponDef;    // ItemDefinition typu Weapon
        public PlayerInventory inventory;   // odkaz na hráčův inventář

        [Header("Stav (runtime)")]
        [SerializeField] private int inMagazine;
        public int InMagazine => inMagazine;

        void OnEnable()
        {
            if (weaponDef && weaponDef.weapon != null && weaponDef.weapon.magazineSize > 0)
                inMagazine = Mathf.Clamp(weaponDef.weapon.startLoaded, 0, weaponDef.weapon.magazineSize);
        }

        /// <summary>Pokuste se vystřelit. Vrací true pokud se povedlo odečíst munici.</summary>
        public bool TryFire(int shots = 1)
        {
            if (!weaponDef || weaponDef.weapon == null) return true; // zbraň bez dat neřešíme
            shots = Mathf.Max(shots, weaponDef.weapon.shotsPerUse);

            if (weaponDef.weapon.magazineSize <= 0)
            {
                // Bez zásobníku – bere přímo z rezervy
                var key = weaponDef.weapon.ammoKey;
                if (string.IsNullOrEmpty(key)) return true; // zbraň bez munice
                int taken = inventory.TakeAmmo(key, shots);
                return taken == shots;
            }
            else
            {
                if (inMagazine >= shots) { inMagazine -= shots; return true; }
                return false; // prázdný zásobník -> přebij
            }
        }

        /// <summary>Přebije do plna z rezerv v inventáři. Vrátí kolik dotažených nábojů.</summary>
        public int ReloadFull()
        {
            if (!weaponDef || weaponDef.weapon == null) return 0;
            if (weaponDef.weapon.magazineSize <= 0) return 0;

            string key = weaponDef.weapon.ammoKey;
            if (string.IsNullOrEmpty(key)) return 0;

            int need = weaponDef.weapon.magazineSize - inMagazine;
            if (need <= 0) return 0;

            int taken = inventory.TakeAmmo(key, need);
            inMagazine += taken;
            return taken;
        }
    }
}
