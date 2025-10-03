using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// This script provides a centralised way to save and load player data using the
// AA Save and Load System.  Attach it to a suitable GameObject (for example
// an empty "GameSaveController" object) and assign references in the inspector.
// When calling SaveGame() or LoadGame() the script will persist or restore
// values such as position, stats, resources, ammo reserves, owned weapons,
// perk flags and per‑weapon upgrade levels.  The script requires the
// AA Save and Load System asset (namespace AASave) to be present in the
// project.

namespace Obscurus.Save
{
    using AASave;
    using Obscurus.Items;
    using Obscurus.Player;

    [DisallowMultipleComponent]
    public class GameSaveController : MonoBehaviour
    {
        [Header("Core References")]
        [Tooltip("Reference to the SaveSystem component from the AA Save & Load asset.")]
        public SaveSystem saveSystem;

        [Tooltip("Transform of the player character (for position/rotation).")]
        public Transform playerTransform;

        [Tooltip("Player health system (current HP).")]
        public HealthSystem healthSystem;

        [Tooltip("Player armour system (current armour).")]
        public ArmorSystem armorSystem;

        [Tooltip("Player stamina system (current stamina).")]
        public StaminaSystem staminaSystem;

        [Tooltip("Inventory for resources, ammo and owned weapons.")]
        public PlayerInventory playerInventory;

        [Tooltip("Perk system controlling which perks are unlocked.")]
        public AlchemyPerks alchemyPerks;

        [Tooltip("Service tracking per‑weapon upgrade tiers and rune flags.")]
        public WeaponUpgradeService weaponUpgradeService;

        [Tooltip("Database of items used to look up ItemDefinition objects by Id.")]
        public ItemDatabase itemDatabase;

        [Tooltip("Weapon holder controlling which weapon is currently equipped.")]
        public WeaponHolder weaponHolder;

        // -------------- PUBLIC API --------------

        /// <summary>
        /// Saves all configured pieces of player data into the SaveSystem.  Call
        /// this method when the player requests a save or at checkpoints.
        /// </summary>
        public void SaveGame()
        {
            if (saveSystem == null) { Debug.LogWarning("[GameSaveController] SaveSystem not assigned."); return; }
            if (playerTransform == null) { Debug.LogWarning("[GameSaveController] Player transform not assigned."); return; }

            // Save position and rotation (Euler angles)
            saveSystem.Save("PlayerPos", playerTransform.position);
            saveSystem.Save("PlayerRot", playerTransform.eulerAngles);

            // Save current health, armour and stamina
            if (healthSystem != null) saveSystem.Save("PlayerHP", healthSystem.Current);
            if (armorSystem  != null) saveSystem.Save("PlayerArmor", armorSystem.Current);
            if (staminaSystem!= null) saveSystem.Save("PlayerStamina", staminaSystem.Current);

            // Save fixed resources (PG, Gold, Mercury, Essence, Scrap, Substances)
            if (playerInventory != null)
            {
                foreach (ResourceKey key in Enum.GetValues(typeof(ResourceKey)))
                {
                    int current = playerInventory.GetResource(key);
                    saveSystem.Save($"Res_{key}", current);
                }

                // Save owned weapons (playerInventory holds them as weaponEntries)
                var ownedWeaponIds = playerInventory.GetOwnedWeaponIds();
                // Arrays can be saved directly via Save() – SaveSystem automatically handles array types
                saveSystem.Save("OwnedWeaponIds", ownedWeaponIds.ToArray());

                // Save ammo reserves: keys and counts only for ammo > 0
                if (itemDatabase != null)
                {
                    var ammoKeys   = new List<string>();
                    var ammoCounts = new List<int>();
                    foreach (var ammo in itemDatabase.GetAllAmmo())
                    {
                        string key = ammo != null && ammo.ammo != null ? ammo.ammo.ammoKey : null;
                        if (string.IsNullOrEmpty(key)) continue;
                        int count = playerInventory.GetAmmoReserve(key);
                        if (count > 0)
                        {
                            ammoKeys.Add(key);
                            ammoCounts.Add(count);
                        }
                    }
                    saveSystem.Save("AmmoKeys", ammoKeys.ToArray());
                    saveSystem.Save("AmmoCounts", ammoCounts.ToArray());
                }
            }

            // Save perks: store IDs of unlocked perks (as ints)
            if (alchemyPerks != null)
            {
                var unlocked = new List<int>();
                foreach (PerkId pid in Enum.GetValues(typeof(PerkId)))
                    if (alchemyPerks.IsUnlocked(pid)) unlocked.Add((int)pid);
                saveSystem.Save("UnlockedPerkIds", unlocked.ToArray());
            }

            // Save per‑weapon upgrades
            if (weaponUpgradeService != null && playerInventory != null)
            {
                var upgradeIds    = new List<string>();
                var damageTiers   = new List<int>();
                var vitriolRunes  = new List<int>();
                foreach (var wid in playerInventory.GetOwnedWeaponIds())
                {
                    var def = itemDatabase != null ? itemDatabase.FindById(wid) : null;
                    if (!def) continue;
                    var st = weaponUpgradeService.GetState(def);
                    if (st != null)
                    {
                        upgradeIds.Add(wid);
                        damageTiers.Add(st.damageTiers);
                        vitriolRunes.Add(st.vitriolRune ? 1 : 0);
                    }
                }
                saveSystem.Save("UpgradeWeaponIds", upgradeIds.ToArray());
                saveSystem.Save("UpgradeDamageTiers", damageTiers.ToArray());
                saveSystem.Save("UpgradeVitriolRune", vitriolRunes.ToArray());
            }

            // Save currently equipped weapon (if any)
            if (weaponHolder != null && weaponHolder.Current != null)
            {
                try
                {
                    var def = weaponHolder.Current.Definition;
                    string id = def != null ? def.Id : string.Empty;
                    saveSystem.Save("CurrentWeaponId", id);
                }
                catch
                {
                    saveSystem.Save("CurrentWeaponId", string.Empty);
                }
            }
            else
            {
                saveSystem.Save("CurrentWeaponId", string.Empty);
            }

            // Data persisted; optionally flush to disk if the asset exposes such API.
        }

        /// <summary>
        /// Loads all persisted pieces of player data from the SaveSystem.  Call
        /// this method when a level or game starts to restore the previous
        /// progress.  Defaults are used when no save entry exists.
        /// </summary>
        public void LoadGame()
        {
            if (saveSystem == null) { Debug.LogWarning("[GameSaveController] SaveSystem not assigned."); return; }
            if (playerTransform == null) { Debug.LogWarning("[GameSaveController] Player transform not assigned."); return; }

            // Load position and rotation
            // Load position and rotation directly; when passing a default value, SaveSystem returns the same type
            Vector3 pos = saveSystem.Load("PlayerPos", playerTransform.position);
            Vector3 rot = saveSystem.Load("PlayerRot", playerTransform.eulerAngles);
            playerTransform.position    = pos;
            playerTransform.eulerAngles = rot;

            // Load current health, armour and stamina (use Refill() to set values)
            if (healthSystem != null)
            {
                float hp = saveSystem.Load("PlayerHP", healthSystem.Current);
                healthSystem.Refill(hp);
            }
            if (armorSystem != null)
            {
                float ar = saveSystem.Load("PlayerArmor", armorSystem.Current);
                armorSystem.Refill(ar);
            }
            if (staminaSystem != null)
            {
                float st = saveSystem.Load("PlayerStamina", staminaSystem.Current);
                staminaSystem.Refill(st);
            }

            // Load resources
            if (playerInventory != null)
            {
                foreach (ResourceKey key in Enum.GetValues(typeof(ResourceKey)))
                {
                    int current = playerInventory.GetResource(key);
                    int target  = saveSystem.Load($"Res_{key}", current);
                    int diff    = target - current;
                    if (diff > 0) playerInventory.AddResource(key, diff);
                    else if (diff < 0) playerInventory.SpendResource(key, -diff);
                }

                // Load owned weapons
                // Arrays are loaded directly via Load() – returns string[]
                var savedOwned = saveSystem.Load("OwnedWeaponIds", new string[0]);
                // Remove any weapons not in the saved list
                var currentOwned = playerInventory.GetOwnedWeaponIds().ToList();
                foreach (var wid in currentOwned)
                {
                    if (!Array.Exists(savedOwned, id => id == wid))
                    {
                        var def = itemDatabase != null ? itemDatabase.FindById(wid) : null;
                        if (def != null) playerInventory.RemoveWeapon(def);
                    }
                }
                // Add missing weapons
                foreach (var wid in savedOwned)
                {
                    if (!currentOwned.Contains(wid))
                    {
                        var def = itemDatabase != null ? itemDatabase.FindById(wid) : null;
                        if (def != null) playerInventory.AddWeapon(def);
                    }
                }

                // Load ammo reserves
                if (itemDatabase != null)
                {
                    var keys   = saveSystem.Load("AmmoKeys", new string[0]);
                    var counts = saveSystem.Load("AmmoCounts", new int[0]);
                    for (int i = 0; i < keys.Length && i < counts.Length; i++)
                    {
                        string key = keys[i];
                        int target = counts[i];
                        int current = playerInventory.GetAmmoReserve(key);
                        int diff = target - current;
                        if (diff > 0) playerInventory.AddAmmo(key, diff);
                        else if (diff < 0) playerInventory.TakeAmmo(key, -diff);
                    }
                }
            }

            // Load perks
            if (alchemyPerks != null)
            {
                // Reset all perks to false
                foreach (PerkId pid in Enum.GetValues(typeof(PerkId)))
                    alchemyPerks.SetUnlocked(pid, false);
                // Set loaded perks to true
                var ids = saveSystem.Load("UnlockedPerkIds", new int[0]);
                foreach (var v in ids)
                {
                    if (Enum.IsDefined(typeof(PerkId), v))
                        alchemyPerks.SetUnlocked((PerkId)v, true);
                }
            }

            // Load per‑weapon upgrades
            if (weaponUpgradeService != null && playerInventory != null)
            {
                var ids         = saveSystem.Load("UpgradeWeaponIds",     new string[0]);
                var damageTiers = saveSystem.Load("UpgradeDamageTiers", new int[0]);
                var vitriolRunes= saveSystem.Load("UpgradeVitriolRune",  new int[0]);
                for (int i = 0; i < ids.Length && i < damageTiers.Length && i < vitriolRunes.Length; i++)
                {
                    var def = itemDatabase != null ? itemDatabase.FindById(ids[i]) : null;
                    if (!def) continue;
                    var st = weaponUpgradeService.GetState(def);
                    if (st != null)
                    {
                        st.damageTiers = damageTiers[i];
                        st.vitriolRune = vitriolRunes[i] != 0;
                    }
                }
            }

            // Load currently equipped weapon
            if (weaponHolder != null)
            {
                var currentId = saveSystem.Load("CurrentWeaponId", string.Empty);
                if (!string.IsNullOrEmpty(currentId) && itemDatabase != null)
                {
                    var def = itemDatabase.FindById(currentId);
                    if (def != null)
                    {
                        weaponHolder.EquipByDefinition(def);
                    }
                }
            }
        }
    }
}