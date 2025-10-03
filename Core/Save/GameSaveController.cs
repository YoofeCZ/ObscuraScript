using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AASave;              // namespace nového assetu
using Obscurus.Items;
using Obscurus.Player;

namespace Obscurus.Save
{
    /// <summary>
    /// Ukládá a načítá všechna potřebná data hráče pomocí AA Save & Load Systemu.
    /// Sloty (1–3) řešíme prefixem klíčů (S{slot}_KeyName).
    /// </summary>
    [DisallowMultipleComponent]
    public class GameSaveController : MonoBehaviour
    {
        [Header("Reference na komponenty")]
        public SaveSystem saveSystem;
        public Transform playerTransform;
        public HealthSystem healthSystem;
        public ArmorSystem armorSystem;
        public StaminaSystem staminaSystem;
        public PlayerInventory playerInventory;
        public AlchemyPerks alchemyPerks;
        public WeaponUpgradeService weaponUpgradeService;
        public ItemDatabase itemDatabase;
        public WeaponHolder weaponHolder;

        [SerializeField] private int currentSlot = 1;   // výchozí slot

        /// <summary>
        /// Označuje, zda se spouští nová hra.  Resetuje se při načtení slotu.
        /// </summary>
        public static bool IsNewGame { get; set; } = true;

        void Awake()
        {
            SetSlot(currentSlot);
        }

        /// <summary>
        /// Pomocná funkce pro prefixování klíčů podle slotu (S1_, S2_, S3_).
        /// </summary>
        private string K(string key) => $"S{currentSlot}_{key}";

        /// <summary>
        /// Nastaví slot (1–3).
        /// </summary>
        public void SetSlot(int slot)
        {
            currentSlot = Mathf.Clamp(slot, 1, 3);
        }

        /// <summary>Uloží veškerá data do aktuálního slotu.</summary>
        public void SaveGame()
        {
            if (saveSystem == null || playerTransform == null) return;

            // pozice a rotace (Euler)
            saveSystem.Save(K("PlayerPos"), playerTransform.position);
            saveSystem.Save(K("PlayerRot"), playerTransform.eulerAngles);

            // životy, brnění, stamina
            if (healthSystem  != null) saveSystem.Save(K("PlayerHP"),      healthSystem.Current);
            if (armorSystem   != null) saveSystem.Save(K("PlayerArmor"),   armorSystem.Current);
            if (staminaSystem != null) saveSystem.Save(K("PlayerStamina"), staminaSystem.Current);

            // suroviny a zbraně
            if (playerInventory != null)
            {
                foreach (ResourceKey key in Enum.GetValues(typeof(ResourceKey)))
                    saveSystem.Save(K($"Res_{key}"), playerInventory.GetResource(key));

                saveSystem.Save(K("OwnedWeaponIds"), playerInventory.GetOwnedWeaponIds().ToArray());

                // rezervy munice (pouze klíče s nenulovým množstvím)
                if (itemDatabase != null)
                {
                    var keys   = new List<string>();
                    var counts = new List<int>();
                    foreach (var ammo in itemDatabase.GetAllAmmo())
                    {
                        var key = ammo?.ammo?.ammoKey;
                        if (string.IsNullOrEmpty(key)) continue;
                        int count = playerInventory.GetAmmoReserve(key);
                        if (count > 0) { keys.Add(key); counts.Add(count); }
                    }
                    saveSystem.Save(K("AmmoKeys"),   keys.ToArray());
                    saveSystem.Save(K("AmmoCounts"), counts.ToArray()); // int[]
                }
            }

            // odemčené perky → pole intů
            if (alchemyPerks != null)
            {
                var list = new List<int>();
                foreach (PerkId pid in Enum.GetValues(typeof(PerkId)))
                    if (alchemyPerks.IsUnlocked(pid)) list.Add((int)pid);
                saveSystem.Save(K("UnlockedPerkIds"), list.ToArray()); // int[]
            }

            // upgrady zbraní
            if (weaponUpgradeService != null && playerInventory != null)
            {
                var ids      = new List<string>();
                var tiers    = new List<int>();
                var runes    = new List<int>();
                foreach (var wid in playerInventory.GetOwnedWeaponIds())
                {
                    var def = itemDatabase?.FindById(wid);
                    var st  = weaponUpgradeService.GetState(def);
                    if (st == null) continue;
                    ids.Add(wid);
                    tiers.Add(st.damageTiers);
                    runes.Add(st.vitriolRune ? 1 : 0);
                }
                saveSystem.Save(K("UpgradeWeaponIds"),   ids.ToArray());   // string[]
                saveSystem.Save(K("UpgradeDamageTiers"), tiers.ToArray()); // int[]
                saveSystem.Save(K("UpgradeVitriolRune"), runes.ToArray()); // int[]
            }

            // aktuálně vybavená zbraň
            string currentId = (weaponHolder != null && weaponHolder.Current != null && weaponHolder.Current.Definition != null)
                             ? weaponHolder.Current.Definition.Id : string.Empty;
            saveSystem.Save(K("CurrentWeaponId"), currentId);
        }

        /// <summary>Načte data z aktuálního slotu.</summary>
        public void LoadGame()
        {
            if (saveSystem == null || playerTransform == null) return;

            // pozice a rotace
            Vector3 pos = saveSystem.Load(K("PlayerPos"), playerTransform.position);
            Vector3 rot = saveSystem.Load(K("PlayerRot"), playerTransform.eulerAngles);
            playerTransform.position    = pos;
            playerTransform.eulerAngles = rot;

            // HP, Armor, Stamina
            if (healthSystem  != null) healthSystem.Refill(saveSystem.Load(K("PlayerHP"),      healthSystem.Current));
            if (armorSystem   != null) armorSystem.Refill(saveSystem.Load(K("PlayerArmor"),    armorSystem.Current));
            if (staminaSystem != null) staminaSystem.Refill(saveSystem.Load(K("PlayerStamina"), staminaSystem.Current));

            // suroviny, zbraně, munice
            if (playerInventory != null)
            {
                foreach (ResourceKey key in Enum.GetValues(typeof(ResourceKey)))
                {
                    int current = playerInventory.GetResource(key);
                    int target  = saveSystem.Load(K($"Res_{key}"), current);
                    int diff = target - current;
                    if (diff > 0) playerInventory.AddResource(key, diff);
                    else if (diff < 0) playerInventory.SpendResource(key, -diff);
                }

                // seznam vlastněných zbraní – načtení pole stringů
                var savedOwned = saveSystem.LoadArray(K("OwnedWeaponIds"), new string[0]).AsStringArray();
                var currentOwned = playerInventory.GetOwnedWeaponIds().ToList();
                foreach (var wid in currentOwned)
                    if (!savedOwned.Contains(wid)) playerInventory.RemoveWeapon(itemDatabase.FindById(wid));
                foreach (var wid in savedOwned)
                    if (!currentOwned.Contains(wid)) playerInventory.AddWeapon(itemDatabase.FindById(wid));

                // rezervy munice
                var ammoKeys   = saveSystem.LoadArray(K("AmmoKeys"),   new string[0]).AsStringArray();
                var ammoCounts = saveSystem.LoadArray(K("AmmoCounts"), new int[0]); // ← žádné .AsIntArray()
                for (int i = 0; i < ammoKeys.Length && i < ammoCounts.Length; i++)
                {
                    string key = ammoKeys[i];
                    int target = ammoCounts[i];
                    int current = playerInventory.GetAmmoReserve(key);
                    int diff = target - current;
                    if (diff > 0) playerInventory.AddAmmo(key, diff);
                    else if (diff < 0) playerInventory.TakeAmmo(key, -diff);
                }
            }

            // perky
            if (alchemyPerks != null)
            {
                foreach (PerkId pid in Enum.GetValues(typeof(PerkId)))
                    alchemyPerks.SetUnlocked(pid, false);

                var ids = saveSystem.LoadArray(K("UnlockedPerkIds"), new int[0]); // ← žádné .AsIntArray()
                foreach (var v in ids)
                    if (Enum.IsDefined(typeof(PerkId), v))
                        alchemyPerks.SetUnlocked((PerkId)v, true);
            }

            // upgrady zbraní
            if (weaponUpgradeService != null && playerInventory != null)
            {
                var ids   = saveSystem.LoadArray(K("UpgradeWeaponIds"),   new string[0]).AsStringArray();
                var tiers = saveSystem.LoadArray(K("UpgradeDamageTiers"), new int[0]); // ← int[]
                var runes = saveSystem.LoadArray(K("UpgradeVitriolRune"), new int[0]); // ← int[]
                for (int i = 0; i < ids.Length && i < tiers.Length && i < runes.Length; i++)
                {
                    var def = itemDatabase?.FindById(ids[i]);
                    var st  = weaponUpgradeService.GetState(def);
                    if (st != null)
                    {
                        st.damageTiers = tiers[i];
                        st.vitriolRune = runes[i] != 0;
                    }
                }
            }

            // aktuálně vybavená zbraň
            if (weaponHolder != null)
            {
                var id = saveSystem.Load(K("CurrentWeaponId"), string.Empty);
                if (!string.IsNullOrEmpty(id) && itemDatabase != null)
                {
                    var def = itemDatabase.FindById(id);
                    if (def != null) weaponHolder.EquipByDefinition(def);
                }
            }
        }

        // === Slot API ===
        public void SaveSlot(int slot)
        {
            SetSlot(slot);
            IsNewGame = false;
            SaveGame();
        }

        public void LoadSlot(int slot)
        {
            SetSlot(slot);
            LoadGame();
        }

        public void DeleteSlot(int slot)
        {
            SetSlot(slot);
            if (saveSystem == null) return;

            // Přepsání známých klíčů "prázdnou" hodnotou:
            saveSystem.Save(K("PlayerPos"), Vector3.zero);
            saveSystem.Save(K("PlayerRot"), Vector3.zero);
            saveSystem.Save(K("PlayerHP"), 0);
            saveSystem.Save(K("PlayerArmor"), 0);
            saveSystem.Save(K("PlayerStamina"), 0);
            saveSystem.Save(K("OwnedWeaponIds"), Array.Empty<string>());
            saveSystem.Save(K("AmmoKeys"), Array.Empty<string>());
            saveSystem.Save(K("AmmoCounts"), Array.Empty<int>());
            saveSystem.Save(K("UnlockedPerkIds"), Array.Empty<int>());
            saveSystem.Save(K("UpgradeWeaponIds"), Array.Empty<string>());
            saveSystem.Save(K("UpgradeDamageTiers"), Array.Empty<int>());
            saveSystem.Save(K("UpgradeVitriolRune"), Array.Empty<int>());
            saveSystem.Save(K("CurrentWeaponId"), string.Empty);

            // Suroviny: vynulovat všechny ResourceKey
            foreach (ResourceKey key in Enum.GetValues(typeof(ResourceKey)))
                saveSystem.Save(K($"Res_{key}"), 0);
        }

        public string SlotSummary(int slot)
        {
            SetSlot(slot);
            return saveSystem.DoesDataExists(K("PlayerPos")) ? "Saved" : "Empty";
        }
    }
}
