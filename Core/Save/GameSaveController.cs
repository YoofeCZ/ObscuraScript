// Assets/Obscurus/Scripts/Core/Save/GameSaveController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using AASave;              // AA Save & Load System
using Obscurus.Items;
using Obscurus.Player;
using Obscurus.UI;        // kvůli WeaponCarouselUI (fallback na DB)
using Obscurus.Weapons; 

namespace Obscurus.Save
{
    /// <summary>
    /// Ukládá/načítá data hráče přes AA Save & Load.
    /// - Automaticky si najde runtime reference po spawnnutí hráče (OnPlayerSpawned).
    /// - Ukládá i směr pohledu (yaw/pitch) + NÁZEV LEVELU (pro bootstrap→level workflow).
    /// - ItemDatabase nastav ručně (nebo se vezme z WeaponCarouselUI.database).
    /// - Sloty (1–3) řeší prefix S{slot}_KeyName.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameSaveController : MonoBehaviour
    {
        [Header("Reference (můžou zůstat prázdné – najdou se automaticky)")]
        public SaveSystem saveSystem;
        public Transform playerTransform;
        public HealthSystem healthSystem;
        public ArmorSystem armorSystem;
        public StaminaSystem staminaSystem;
        public PlayerInventory playerInventory;
        public AlchemyPerks alchemyPerks;
        public WeaponHolder weaponHolder;

        [Tooltip("CENTRÁLNÍ databáze itemů (ScriptableObject). Nastav ručně. Když je prázdné, vezme se z WeaponCarouselUI.database.")]
        public ItemDatabase itemDatabase;

        [Header("Upgrade provider (volitelné)")]
        [Tooltip("Přetáhni sem GameObject s WeaponUpgradeService NEBO s WeaponCarouselUI (kvůli DB).")]
        [SerializeField] private Component upgradeProvider;
        private WeaponUpgradeService weaponUpgradeService; // pokud existuje
        private WeaponCarouselUI weaponCarouselUI;         // kvůli fallback DB

        [Header("Slot")]
        [SerializeField] private int currentSlot = 1;

        /// <summary>Flag pro GameManager při startu nové hry (reset statů při spawnování).</summary>
        public static bool IsNewGame { get; set; } = true;

        // kamera/head pro ukládání pitch
        Transform _lookTransform;

        // odložená aplikace savu po přepnutí levelu
        bool _applyOnNextSpawn;
        int  _applySlotVersion;

        // ===== lifecycle =====
        void Awake()
        {
            SetSlot(currentSlot);
            TryFindSaveSystem();
            CacheUpgradeProvider(upgradeProvider);
        }

        void OnEnable()
        {
            GameManager.OnPlayerSpawned   += HandlePlayerSpawned;
            GameManager.OnPlayerDespawned += HandlePlayerDespawned;

            var gm = GameManager.I;
            if (gm && gm.CurrentPlayer) HandlePlayerSpawned(gm.CurrentPlayer);
            else TryGlobalAutoBind();
        }

        void OnDisable()
        {
            GameManager.OnPlayerSpawned   -= HandlePlayerSpawned;
            GameManager.OnPlayerDespawned -= HandlePlayerDespawned;
        }

        void OnValidate() => CacheUpgradeProvider(upgradeProvider);

        // === eventy ===
        void HandlePlayerSpawned(GameObject playerGo)
        {
            if (!playerGo) return;

            AutoWireFrom(playerGo);

            // pokud čekáme na přepnutí levelu → teď aplikuj load
            if (_applyOnNextSpawn && _applySlotVersion == currentSlot)
            {
                _applyOnNextSpawn = false;
                // malá prodleva – nech UI/Addressables dožít
                StartCoroutine(WaitAndApplyAfterSpawn());
            }
        }

        System.Collections.IEnumerator WaitAndApplyAfterSpawn()
        {
            yield return null; // jeden frame
            LoadGame_ApplyToCurrentPlayer();
        }

        void HandlePlayerDespawned()
        {
            // reference nechávám – při dalším spawnu se přepíšou
        }

        /// <summary>Naváže komponenty z hráče a dohledá ostatní ve scéně.</summary>
        public void AutoWireFrom(GameObject playerGo)
        {
            if (!playerGo) return;

            TryFindSaveSystem();

            // 1) věci přímo u Playera
            playerTransform = playerGo.transform;
            healthSystem    = FindOnSelfOrChildren<HealthSystem>(playerGo);
            armorSystem     = FindOnSelfOrChildren<ArmorSystem>(playerGo);
            staminaSystem   = FindOnSelfOrChildren<StaminaSystem>(playerGo);
            weaponHolder    = FindOnSelfOrChildren<WeaponHolder>(playerGo);

            // 2) věci mimo playera (ve scéně)
            if (!playerInventory) playerInventory = FindComponentInScene<PlayerInventory>();
            if (!alchemyPerks)    alchemyPerks    = FindComponentInScene<AlchemyPerks>();

            // 3) upgrade provider (jen pokud ho používáš)
            if (!weaponUpgradeService) weaponUpgradeService = FindComponentInScene<WeaponUpgradeService>();
            if (!weaponCarouselUI)     weaponCarouselUI     = FindComponentInScene<WeaponCarouselUI>();

            // 4) kamera/head pro pitch
            _lookTransform = FindPlayerLook(playerTransform);

            // 5) ItemDatabase fallback z carouselu
            if (!itemDatabase && weaponCarouselUI) itemDatabase = weaponCarouselUI.database;
        }

        /// <summary>Fallback, když CurrentPlayer neexistuje.</summary>
        void TryGlobalAutoBind()
        {
            TryFindSaveSystem();

            var tagged = GameObject.FindWithTag("Player");
            if (tagged) { AutoWireFrom(tagged); return; }

            healthSystem    = FindComponentInScene<HealthSystem>();
            armorSystem     = FindComponentInScene<ArmorSystem>();
            staminaSystem   = FindComponentInScene<StaminaSystem>();
            playerInventory = FindComponentInScene<PlayerInventory>();
            alchemyPerks    = FindComponentInScene<AlchemyPerks>();
            weaponHolder    = FindComponentInScene<WeaponHolder>();

            if (!weaponUpgradeService) weaponUpgradeService = FindComponentInScene<WeaponUpgradeService>();
            if (!weaponCarouselUI)     weaponCarouselUI     = FindComponentInScene<WeaponCarouselUI>();
            if (!itemDatabase && weaponCarouselUI) itemDatabase = weaponCarouselUI.database;

            if (!playerTransform)
            {
                if (weaponHolder)         playerTransform = weaponHolder.transform.root;
                else if (playerInventory) playerTransform = playerInventory.transform.root;
                else if (healthSystem)    playerTransform = healthSystem.transform.root;
            }
            _lookTransform = FindPlayerLook(playerTransform);
        }

        void TryFindSaveSystem()
        {
            if (saveSystem) return;
#if UNITY_2022_2_OR_NEWER
            saveSystem = UnityEngine.Object.FindFirstObjectByType<SaveSystem>(FindObjectsInactive.Include);
#else
            var all = Resources.FindObjectsOfTypeAll<SaveSystem>();
            foreach (var s in all)
            {
                if (!s) continue;
                var go = s.gameObject;
                if (go && go.scene.IsValid() && (go.hideFlags & HideFlags.HideInHierarchy) == 0)
                { saveSystem = s; break; }
            }
#endif
            if (!saveSystem)
                Debug.LogWarning("[GameSaveController] SaveSystem nenalezen – uložení/načtení nepojede, dokud nebude SaveSystem ve scéně.");
        }

        // === helpers ===
        static T FindOnSelfOrChildren<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c ? c : go.GetComponentInChildren<T>(true);
        }

        static T FindComponentInScene<T>() where T : Component
        {
#if UNITY_2022_2_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
            var all = Resources.FindObjectsOfTypeAll<T>();
            foreach (var c in all)
            {
                if (!c) continue;
                var go = c.gameObject;
                if (go && go.scene.IsValid() && (go.hideFlags & HideFlags.HideInHierarchy) == 0)
                    return c;
            }
            return null;
#endif
        }

        void CacheUpgradeProvider(Component c)
        {
            if (!c) return;
            weaponUpgradeService = c as WeaponUpgradeService ?? c.GetComponent<WeaponUpgradeService>();
            weaponCarouselUI     = c as WeaponCarouselUI     ?? c.GetComponent<WeaponCarouselUI>();
            if (!itemDatabase && weaponCarouselUI) itemDatabase = weaponCarouselUI.database;
        }

        static Transform FindPlayerLook(Transform playerRoot)
        {
            if (!playerRoot) return null;

            // Hledej CameraPivot prioritně
            foreach (var t in playerRoot.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name.ToLowerInvariant();
                if (n.Contains("pivot"))   // <--- přidáno
                    return t;
            }

            // fallback na samotnou kameru
            var cam = playerRoot.GetComponentInChildren<Camera>(true);
            if (cam) return cam.transform;

            if (Camera.main && Camera.main.transform.IsChildOf(playerRoot))
                return Camera.main.transform;

            foreach (var t in playerRoot.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name.ToLowerInvariant();
                if (n.Contains("camera") || n.Contains("head") || n.Contains("look") || n.Contains("view"))
                    return t;
            }
            return null;
        }


        ItemDatabase EffectiveDB => itemDatabase ? itemDatabase : (weaponCarouselUI ? weaponCarouselUI.database : null);

        // ===== Slot API =====
        public void SetSlot(int slot) => currentSlot = Mathf.Clamp(slot, 1, 3);
        private string K(string key) => $"S{currentSlot}_{key}";

        public void SaveSlot(int slot)
        {
            SetSlot(slot);
            IsNewGame = false;
            SaveGame();
        }

        public void LoadSlot(int slot)
        {
            SetSlot(slot);
            IsNewGame = false; // aby GameManager při spawnu neresetoval staty
            if (!playerTransform) TryGlobalAutoBind();
            LoadGame(); // tohle případně přepne level a zbytek aplikuje po spawnu
        }

        public void DeleteSlot(int slot)
        {
            SetSlot(slot);
            if (saveSystem == null) { TryFindSaveSystem(); if (saveSystem == null) return; }

            // level + pozice + pohled
            saveSystem.Save(K("Level"), string.Empty);
            saveSystem.Save(K("PlayerPos"), Vector3.zero);
            saveSystem.Save(K("PlayerRot"), Vector3.zero);
            saveSystem.Save(K("LookYaw"),   0f);
            saveSystem.Save(K("LookPitch"), 0f);

            // staty
            saveSystem.Save(K("PlayerHP"), 0f);
            saveSystem.Save(K("PlayerHP_Max"), 0f);
            saveSystem.Save(K("PlayerArmor"), 0f);
            saveSystem.Save(K("PlayerArmor_Max"), 0f);
            saveSystem.Save(K("PlayerStamina"), 0f);
            saveSystem.Save(K("PlayerStamina_Max"), 0f);

            // inventář
            saveSystem.Save(K("OwnedWeaponIds"), Array.Empty<string>());
            saveSystem.Save(K("AmmoKeys"), Array.Empty<string>());
            saveSystem.Save(K("AmmoCounts"), Array.Empty<int>());
            saveSystem.Save(K("UnlockedPerkIds"), Array.Empty<int>());
            saveSystem.Save(K("UpgradeWeaponIds"), Array.Empty<string>());
            saveSystem.Save(K("UpgradeDamageTiers"), Array.Empty<int>());
            saveSystem.Save(K("UpgradeVitriolRune"), Array.Empty<int>());
            saveSystem.Save(K("CurrentWeaponId"), string.Empty);

            foreach (ResourceKey key in Enum.GetValues(typeof(ResourceKey)))
                saveSystem.Save(K($"Res_{key}"), 0);
        }

        public string SlotSummary(int slot)
        {
            SetSlot(slot);
            if (saveSystem == null) TryFindSaveSystem();
            return (saveSystem != null && saveSystem.DoesDataExists(K("PlayerPos"))) ? "Saved" : "Empty";
        }

        // ===== Save / Load =====
        public void SaveGame()
        {
            if (saveSystem == null) TryFindSaveSystem();
            if (saveSystem == null || playerTransform == null) return;

            // LEVEL (pro _Bootstrap→level model)
            string level =
                (GameManager.I != null && !string.IsNullOrEmpty(GameManager.I.CurrentLevel))
                ? GameManager.I.CurrentLevel
                : SceneManager.GetActiveScene().name;
            saveSystem.Save(K("Level"), level);

            // pozice + pohled (yaw/pitch)
            saveSystem.Save(K("PlayerPos"), playerTransform.position);

            float yaw = playerTransform.eulerAngles.y;
            if (!_lookTransform) _lookTransform = FindPlayerLook(playerTransform);

// Ulož pitch normalizovaný na -180…+180
            float pitch = 0f;
            if (_lookTransform)
            {
                pitch = _lookTransform.localEulerAngles.x;
                if (pitch > 180f) pitch -= 360f;  // převede 350° na -10°
            }

            saveSystem.Save(K("LookYaw"), yaw);
            saveSystem.Save(K("LookPitch"), pitch);


            // pro kompatibilitu ukládám i celou rotaci
            saveSystem.Save(K("PlayerRot"), playerTransform.eulerAngles);

            // staty
            if (healthSystem != null) {
                saveSystem.Save(K("PlayerHP"), healthSystem.Current);
                saveSystem.Save(K("PlayerHP_Max"), healthSystem.max);
            }
            if (armorSystem != null) {
                saveSystem.Save(K("PlayerArmor"), armorSystem.Current);
                saveSystem.Save(K("PlayerArmor_Max"), armorSystem.max);
            }
            if (staminaSystem != null) {
                saveSystem.Save(K("PlayerStamina"), staminaSystem.Current);
                saveSystem.Save(K("PlayerStamina_Max"), staminaSystem.max);
            }

            // inventář
            if (playerInventory != null)
            {
                foreach (ResourceKey key in Enum.GetValues(typeof(ResourceKey)))
                    saveSystem.Save(K($"Res_{key}"), playerInventory.GetResource(key));

                saveSystem.Save(K("OwnedWeaponIds"), playerInventory.GetOwnedWeaponIds().ToArray());

                // ammo → jen pokud známe seznam klíčů (DB)
                var db = EffectiveDB;
                if (db != null)
                {
                    var keys   = new List<string>();
                    var counts = new List<int>();
                    foreach (var ammo in db.GetAllAmmo())
                    {
                        var key = ammo?.ammo?.ammoKey;
                        if (string.IsNullOrEmpty(key)) continue;
                        int count = playerInventory.GetAmmoReserve(key);
                        if (count > 0) { keys.Add(key); counts.Add(count); }
                    }
                    saveSystem.Save(K("AmmoKeys"),   keys.ToArray());
                    saveSystem.Save(K("AmmoCounts"), counts.ToArray());
                }
            }

            // perky
            if (alchemyPerks != null)
            {
                var list = new List<int>();
                foreach (PerkId pid in Enum.GetValues(typeof(PerkId)))
                    if (alchemyPerks.IsUnlocked(pid)) list.Add((int)pid);
                saveSystem.Save(K("UnlockedPerkIds"), list.ToArray());
            }

            // upgrady – jen pokud máš skutečný WeaponUpgradeService
            if (weaponUpgradeService != null && playerInventory != null)
            {
                var db = EffectiveDB;
                var ids   = new List<string>();
                var tiers = new List<int>();
                var runes = new List<int>();

                foreach (var wid in playerInventory.GetOwnedWeaponIds())
                {
                    var def = db?.FindById(wid);
                    var st  = weaponUpgradeService.GetState(def);
                    if (st == null) continue;
                    ids.Add(wid);
                    tiers.Add(st.damageTiers);
                    runes.Add(st.vitriolRune ? 1 : 0);
                }

                saveSystem.Save(K("UpgradeWeaponIds"),   ids.ToArray());
                saveSystem.Save(K("UpgradeDamageTiers"), tiers.ToArray());
                saveSystem.Save(K("UpgradeVitriolRune"), runes.ToArray());
            }

            // aktuálně vybavená zbraň
            // aktuálně vybavená zbraň
            string currentId =
                (weaponHolder != null && weaponHolder.Current != null && weaponHolder.Current.Definition != null)
                    ? weaponHolder.Current.Definition.Id : string.Empty;
            saveSystem.Save(K("CurrentWeaponId"), currentId);

// uložit i počet nábojů v zásobníku, pokud je to ranged zbraň
            int inMag = 0;
            if (weaponHolder != null && weaponHolder.Current is RangedWeaponBase rangedWeapon)
            {
                inMag = rangedWeapon.InMagazine;  // vlastnost InMagazine u RangedWeaponBase
            }
            saveSystem.Save(K("CurrentWeaponInMag"), inMag);

        }

        public void LoadGame()
        {
            if (saveSystem == null) TryFindSaveSystem();
            if (saveSystem == null) return;

            // 1) načti cílový LEVEL
            string savedLevel = saveSystem.Load(K("Level"), string.Empty);

            // aktuální level podle GameManageru (nebo aktivní scény)
            string currentLevel =
                (GameManager.I != null && !string.IsNullOrEmpty(GameManager.I.CurrentLevel))
                ? GameManager.I.CurrentLevel
                : SceneManager.GetActiveScene().name;

            // 2) je potřeba přepnout scénu?
            if (!string.IsNullOrEmpty(savedLevel) && !string.Equals(savedLevel, currentLevel, StringComparison.Ordinal))
            {
                // odlož aplikaci savu po spawnu v cílové scéně
                _applyOnNextSpawn  = true;
                _applySlotVersion  = currentSlot;

                // zajisti, že budeme mít hráče po přepnutí
                if (GameManager.I != null)
                {
                    GameManager.I.LoadLevel(savedLevel);
                }
                else
                {
                    // nouzově: načti scénu přímo (bez GameManageru)
                    StartCoroutine(LoadSceneDirectThenApply(savedLevel));
                }
                return; // zbytek se aplikuje po spawnu
            }

            // 3) jsme už v cílovém levelu → aplikuj přímo
            LoadGame_ApplyToCurrentPlayer();
        }

        System.Collections.IEnumerator LoadSceneDirectThenApply(string levelName)
        {
            // unload vše kromě _Bootstrap
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name != "_Bootstrap")
                {
                    var u = SceneManager.UnloadSceneAsync(s);
                    if (u != null) while (!u.isDone) yield return null;
                }
            }

            var op = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
            while (!op.isDone) yield return null;

            var lvl = SceneManager.GetSceneByName(levelName);
            if (lvl.IsValid()) SceneManager.SetActiveScene(lvl);

            yield return null;
            LoadGame_ApplyToCurrentPlayer();
        }

        void LoadGame_ApplyToCurrentPlayer()
{
    if (playerTransform == null) TryGlobalAutoBind();
    if (playerTransform == null || saveSystem == null) return;

    // --- pozice ---
    Vector3 pos = saveSystem.Load(K("PlayerPos"), playerTransform.position);
    playerTransform.position = pos;

    // --- yaw/pitch ---
    float yaw   = saveSystem.Load(K("LookYaw"),   playerTransform.eulerAngles.y);
    float pitch = saveSystem.Load(K("LookPitch"), 0f);

    var pc = playerTransform.GetComponent<PlayerController>();
    if (pc != null)
    {
        pc.SetLookAngles(yaw, pitch);
    }
    else
    {
        var e = playerTransform.eulerAngles; e.y = yaw; playerTransform.eulerAngles = e;
        if (!_lookTransform) _lookTransform = FindPlayerLook(playerTransform);
        if (_lookTransform)
        {
            float pitch360 = (pitch < 0f) ? pitch + 360f : pitch;
            var le = _lookTransform.localEulerAngles; le.x = pitch360; _lookTransform.localEulerAngles = le;
        }
    }

    // --- inventář (resources, ammo, owned weapons) ---
    if (playerInventory != null)
    {
        foreach (ResourceKey key in Enum.GetValues(typeof(ResourceKey)))
        {
            int cur = playerInventory.GetResource(key);
            int tgt = saveSystem.Load(K($"Res_{key}"), cur);
            int diff = tgt - cur;
            if (diff > 0) playerInventory.AddResource(key, diff);
            else if (diff < 0) playerInventory.SpendResource(key, -diff);
        }

        var db = EffectiveDB;
        string[] savedOwnedIds = saveSystem.LoadArray(K("OwnedWeaponIds"), new string[0]).AsStringArray();

        // sync vlastněných zbraní
        var currentOwned = playerInventory.GetOwnedWeaponIds().ToList();
        foreach (var wid in currentOwned)
            if (!savedOwnedIds.Contains(wid)) playerInventory.RemoveWeapon(db?.FindById(wid));
        foreach (var wid in savedOwnedIds)
            if (!currentOwned.Contains(wid)) playerInventory.AddWeapon(db?.FindById(wid));

        // ammo
        if (db != null)
        {
            var ammoKeys   = saveSystem.LoadArray(K("AmmoKeys"),   new string[0]).AsStringArray();
            var ammoCounts = saveSystem.LoadArray(K("AmmoCounts"), new int[0]);
            for (int i = 0; i < ammoKeys.Length && i < ammoCounts.Length; i++)
            {
                string key = ammoKeys[i];
                int tgt = ammoCounts[i];
                int cur = playerInventory.GetAmmoReserve(key);
                int diff = tgt - cur;
                if (diff > 0) playerInventory.AddAmmo(key, diff);
                else if (diff < 0) playerInventory.TakeAmmo(key, -diff);
            }
        }
    }

    // --- perky ---
    if (alchemyPerks != null)
    {
        foreach (PerkId pid in Enum.GetValues(typeof(PerkId)))
            alchemyPerks.SetUnlocked(pid, false);
        var ids = saveSystem.LoadArray(K("UnlockedPerkIds"), new int[0]);
        foreach (var v in ids)
            if (Enum.IsDefined(typeof(PerkId), v))
                alchemyPerks.SetUnlocked((PerkId)v, true);
    }

    // --- upgrady ---
    if (weaponUpgradeService != null && playerInventory != null)
    {
        var db = EffectiveDB;
        var ids   = saveSystem.LoadArray(K("UpgradeWeaponIds"),   new string[0]).AsStringArray();
        var tiers = saveSystem.LoadArray(K("UpgradeDamageTiers"), new int[0]);
        var runes = saveSystem.LoadArray(K("UpgradeVitriolRune"), new int[0]);
        for (int i = 0; i < ids.Length && i < tiers.Length && i < runes.Length; i++)
        {
            var def = db?.FindById(ids[i]);
            var st  = weaponUpgradeService.GetState(def);
            if (st != null) { st.damageTiers = tiers[i]; st.vitriolRune = runes[i] != 0; }
        }
    }

    // --- WEAPON HOLDER: rebuild podle inventáře + equip uložené zbraně ---
    if (weaponHolder != null)
    {
        var db = EffectiveDB;
        Obscurus.Items.ItemDefinition prefer = null;
        string currentId = saveSystem.Load(K("CurrentWeaponId"), string.Empty);
        if (!string.IsNullOrEmpty(currentId) && db != null)
            prefer = db.FindById(currentId);

        // TADY JE TEN KLÍČ: po wipe/lock v NewGame přestav holder dle inventáře
        weaponHolder.RebuildFromInventory(prefer, autoEquipFirst: true);

        // magazine (pokud je to ranged)
        int savedInMag = saveSystem.Load(K("CurrentWeaponInMag"), 0);
        if (weaponHolder.Current is RangedWeaponBase rangedWeapon)
            rangedWeapon.SetMagazine(savedInMag);
    }
}



    }

    // --- drobný pomocník pro AA Save pole stringů ---
    internal static class SaveArrayExtensions
    {
        public static string[] AsStringArray(this Array a)
        {
            if (a == null) return new string[0];
            var arr = new string[a.Length];
            for (int i = 0; i < a.Length; i++)
                arr[i] = a.GetValue(i)?.ToString() ?? string.Empty;
            return arr;
        }
    }
}
