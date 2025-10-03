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
    /// Umožňuje volit slot (1–3) nastavením přípony souboru.
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
        private string baseExtension;                   // původní přípona z inspektoru

        /// <summary>
        /// Označuje, zda se spouští nová hra.  Resetuje se při načtení slotu.
        /// </summary>
        public static bool IsNewGame { get; set; } = true;

        void Awake()
        {
            if (saveSystem != null)
                baseExtension = saveSystem.SaveFileExtension;
            SetSlot(currentSlot);
        }

        /// <summary>
        /// Nastaví slot (1–3) a změní příponu ukládacího souboru, např. .slot1.
        /// </summary>
        public void SetSlot(int slot)
        {
            currentSlot = Mathf.Clamp(slot, 1, 3);
            if (saveSystem != null && !string.IsNullOrEmpty(baseExtension))
                saveSystem.SaveFileExtension = $"{baseExtension}.slot{currentSlot}";
        }

        /// <summary>Uloží veškerá data do aktuálního slotu.</summary>
        public void SaveGame()
        {
            if (saveSystem == null || playerTransform == null) return;

            // pozice a rotace (Euler)
            saveSystem.Save("PlayerPos", playerTransform.position);
            saveSystem.Save("PlayerRot", playerTransform.eulerAngles);

            // životy, brnění, stamina
            if (healthSystem  != null) saveSystem.Save("PlayerHP",    healthSystem.Current);
            if (armorSystem   != null) saveSystem.Save("PlayerArmor", armorSystem.Current);
            if (staminaSystem != null) saveSystem.Save("PlayerStamina", staminaSystem.Current);

            // suroviny a zbraně
            if (playerInventory != null)
            {
                foreach (ResourceKey key in Enum.GetValues(typeof(ResourceKey)))
                    saveSystem.Save($"Res_{key}", playerInventory.GetResource(key));

                saveSystem.Save("OwnedWeaponIds", playerInventory.GetOwnedWeaponIds().ToArray());

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
                    saveSystem.Save("AmmoKeys",   keys.ToArray());
                    saveSystem.Save("AmmoCounts", counts.ToArray());
                }
            }

            // odemčené perky → pole intů
            if (alchemyPerks != null)
            {
                var list = new List<int>();
                foreach (PerkId pid in Enum.GetValues(typeof(PerkId)))
                    if (alchemyPerks.IsUnlocked(pid)) list.Add((int)pid);
                saveSystem.Save("UnlockedPerkIds", list.ToArray());
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
                saveSystem.Save("UpgradeWeaponIds",   ids.ToArray());
                saveSystem.Save("UpgradeDamageTiers", tiers.ToArray());
                saveSystem.Save("UpgradeVitriolRune", runes.ToArray());
            }

            // aktuálně vybavená zbraň
            string currentId = (weaponHolder != null && weaponHolder.Current != null && weaponHolder.Current.Definition != null)
                             ? weaponHolder.Current.Definition.Id : string.Empty;
            saveSystem.Save("CurrentWeaponId", currentId);
        }

        /// <summary>Načte data z aktuálního slotu.</summary>
        public void LoadGame()
        {
            if (saveSystem == null || playerTransform == null) return;

            // pozice a rotace
            Vector3 pos = saveSystem.Load("PlayerPos", playerTransform.position);
            Vector3 rot = saveSystem.Load("PlayerRot", playerTransform.eulerAngles);
            playerTransform.position    = pos;
            playerTransform.eulerAngles = rot;

            // HP, Armor, Stamina
            if (healthSystem  != null) healthSystem.Refill(saveSystem.Load("PlayerHP",    healthSystem.Current));
            if (armorSystem   != null) armorSystem.Refill(saveSystem.Load("PlayerArmor", armorSystem.Current));
            if (staminaSystem != null) staminaSystem.Refill(saveSystem.Load("PlayerStamina", staminaSystem.Current));

            // suroviny, zbraně, munice
            if (playerInventory != null)
            {
                foreach (ResourceKey key in Enum.GetValues(typeof(ResourceKey)))
                {
                    int current = playerInventory.GetResource(key);
                    int target  = saveSystem.Load($"Res_{key}", current);
                    int diff = target - current;
                    if (diff > 0) playerInventory.AddResource(key, diff);
                    else if (diff < 0) playerInventory.SpendResource(key, -diff);
                }

                // seznam vlastněných zbraní – načtení pole stringů
                var savedOwned = saveSystem.LoadArray("OwnedWeaponIds", new string[0]).AsStringArray();
                var currentOwned = playerInventory.GetOwnedWeaponIds().ToList();
                foreach (var wid in currentOwned)
                    if (!savedOwned.Contains(wid)) playerInventory.RemoveWeapon(itemDatabase.FindById(wid));
                foreach (var wid in savedOwned)
                    if (!currentOwned.Contains(wid)) playerInventory.AddWeapon(itemDatabase.FindById(wid));

                // rezervy munice
                var ammoKeys   = saveSystem.LoadArray("AmmoKeys",   new string[0]).AsStringArray();
                var ammoCounts = saveSystem.LoadArray("AmmoCounts", new int[0]).AsIntArray();
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
                var ids = saveSystem.LoadArray("UnlockedPerkIds", new int[0]).AsIntArray();
                foreach (var v in ids)
                    if (Enum.IsDefined(typeof(PerkId), v))
                        alchemyPerks.SetUnlocked((PerkId)v, true);
            }

            // upgrady zbraní
            if (weaponUpgradeService != null && playerInventory != null)
            {
                var ids        = saveSystem.LoadArray("UpgradeWeaponIds",   new string[0]).AsStringArray();
                var tiers      = saveSystem.LoadArray("UpgradeDamageTiers", new int[0]).AsIntArray();
                var runes      = saveSystem.LoadArray("UpgradeVitriolRune", new int[0]).AsIntArray();
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
                var id = saveSystem.Load("CurrentWeaponId", string.Empty);
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
            // Kompletně smaže všechna data v daném souboru
            saveSystem.DeleteAll();
        }

        public string SlotSummary(int slot)
        {
            SetSlot(slot);
            return saveSystem.DoesDataExists("PlayerPos") ? "Saved" : "Empty";
        }
    }
}
