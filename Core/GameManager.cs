using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using Obscurus.Save;
using Obscurus.Player;
public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    // Události pro napojení dalších systémů (UI, SaveController)
    public static event Action<GameObject> OnPlayerSpawned;
    public static event Action OnPlayerDespawned;

    public GameObject CurrentPlayer { get; private set; }
    
    [Header("Dev / Testing")]
    [Tooltip("Když je zapnuto, New Game nepromaže inventář (rychlé testování).")]
    [SerializeField] bool devKeepInventoryOnNewGame = false;


    [Header("Startup")]
    [SerializeField] string defaultLevel = "Dev";
    [SerializeField] bool startInMainMenu = true;

    [Header("Cursor Policy")]
    [SerializeField] bool lockCursorInGameplay = true;
    [SerializeField] bool showCursorInMenus = true;

    [Header("_Bootstrap Camera & Audio")]
    [SerializeField] Camera bootstrapCamera;
    [SerializeField] AudioListener bootstrapAudio;

    [Header("Player Spawn")]
    [SerializeField] GameObject playerPrefab;
    [Tooltip("Fallback – použije se, pokud ve scéně není PlayerSpawner.")]
    [SerializeField] string playerSpawnTag = "PlayerSpawn";
    [Tooltip("Fallback – použije se, pokud ve scéně není PlayerSpawner.")]
    [SerializeField] string playerStartName = "PlayerStart";

    [SerializeField] string pendingSpawnId; // jednorázově před LoadLevel()
    public void SetNextSpawn(string id) => pendingSpawnId = id;

    string _loadingLevelName;
#if ODIN_INSPECTOR
    [ShowInInspector, ReadOnly]
#endif
    public string CurrentLevel { get; private set; }
    public bool InLevel => !string.IsNullOrEmpty(CurrentLevel);

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        TryCacheBootstrapCameraAndAudio();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded_SpawnPlayer;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded_SpawnPlayer;
    }

    void Start()
    {
        if (startInMainMenu)
        {
            if (showCursorInMenus) EnableMenuCursor();
            SetBootstrapCameraActive(true);
            return;
        }

        if (SceneManager.sceneCount == 1 && SceneManager.GetActiveScene().name == "_Bootstrap")
            LoadLevel(defaultLevel);
    }

#if ODIN_INSPECTOR
    [Button(ButtonSizes.Medium)]
#endif
    public void LoadDefault() => LoadLevel(defaultLevel);

#if ODIN_INSPECTOR
    [Button(ButtonSizes.Medium)]
#endif
    public void LoadDev() => LoadLevel("Dev");

#if ODIN_INSPECTOR
    [Button(ButtonSizes.Medium)]
#endif
    public void LoadExample() => LoadLevel("Example");

    public void LoadLevel(string levelName)
    {
        _loadingLevelName = levelName;
        CurrentLevel = levelName;
        StartCoroutine(LoadLevelRoutine(levelName));
    }
    // GameManager.cs (uvnitř třídy)
    bool PlayerAlreadyExistsIn(Scene target)
    {
        var go = GameObject.FindWithTag("Player");
        if (!go) return false;

        // Pokud je hráč v jiné scéně (např. DontDestroyOnLoad), znič ho a vrať false,
        // aby proběhl čistý spawn v target scéně.
        if (go.scene != target)
        {
            Debug.LogWarning($"[GameManager] Found stray Player in scene '{go.scene.name}'. Destroying so we can respawn in '{target.name}'.");
            Destroy(go);
            return false;
        }

        CurrentPlayer = go;
        return true;
    }


    IEnumerator LoadLevelRoutine(string levelName)
    {
        // Despawn starého hráče (event pro UI/Save apod.)
        DespawnPlayerHard("LoadLevel");

        // Unload vše kromě _Bootstrap
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != "_Bootstrap")
            {
                var u = SceneManager.UnloadSceneAsync(s);
                if (u != null) while (!u.isDone) yield return null;
            }
        }

        // Load additivně
        var op = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;

        // Nastav jako active
        var lvl = SceneManager.GetSceneByName(levelName);
        if (lvl.IsValid()) SceneManager.SetActiveScene(lvl);

        Time.timeScale = 1f;
        SetBootstrapCameraActive(false);
        if (lockCursorInGameplay) EnableGameplayCursor(); else EnableMenuCursor();
    }

#if ODIN_INSPECTOR
    [Button(ButtonSizes.Medium)]
#endif
    public void ReturnToMainMenu()
    {
        StartCoroutine(ReturnRoutine());
    }

    IEnumerator ReturnRoutine()
    {
        // 1) Nejdřív znič hráče (ať nezůstane v DontDestroyOnLoad)
        DespawnPlayerHard("ReturnToMainMenu");

        // 2) Pak unload vše kromě _Bootstrap
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != "_Bootstrap")
            {
                var u = SceneManager.UnloadSceneAsync(s);
                if (u != null) while (!u.isDone) yield return null;
            }
        }

        // 3) Reset stavů UI/GM
        CurrentLevel = null;
        _loadingLevelName = null;
        Time.timeScale = 1f;

        SetBootstrapCameraActive(true);
        if (showCursorInMenus) EnableMenuCursor();
    }


    // === Cursor helpers ===
    public void EnableMenuCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    public void EnableGameplayCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // GameManager.cs (uvnitř třídy)
    void DespawnPlayerHard(string reason = null)
    {
        if (!CurrentPlayer) return;

        var go = CurrentPlayer;
        CurrentPlayer = null;

        try { OnPlayerDespawned?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }

        if (go) Destroy(go);
        if (!string.IsNullOrEmpty(reason))
            Debug.Log($"[GameManager] Player despawned ({reason}).");
    }
    
    void ApplyNewGameResets(GameObject p)
{
    if (!GameSaveController.IsNewGame)
    {
        Debug.Log("[GameManager] NewGame=false → reset inventáře/zbraní přeskočen.");
        return;
    }
    if (!p)
    {
        Debug.LogWarning("[GameManager] ApplyNewGameResets: player instance je null.");
        return;
    }

    // --- Staty ---
    var health  = p.GetComponent<HealthSystem>();
    var armor   = p.GetComponent<ArmorSystem>();
    var stamina = p.GetComponent<StaminaSystem>();
    health?.ResetToBase();
    armor?.ResetToBase();
    stamina?.ResetToBase();

    // --- Inventář ---
    if (!devKeepInventoryOnNewGame)
    {
        PlayerInventory inv = p.GetComponent<PlayerInventory>();

        if (!inv)
        {
#if UNITY_2022_2_OR_NEWER
            inv = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
#else
            var allInv = Resources.FindObjectsOfTypeAll<PlayerInventory>();
            foreach (var ii in allInv)
            {
                if (ii && ii.gameObject.scene.IsValid() && (ii.gameObject.hideFlags & HideFlags.HideInHierarchy) == 0)
                { inv = ii; break; }
            }
#endif
            if (inv)
                Debug.LogWarning($"[GameManager] PlayerInventory nebyl na Playerovi, používám '{inv.name}' nalezený ve scéně.");
        }

        if (inv)
        {
            Debug.Log("[GameManager] NewGame → wiping inventory.");
            inv.ResetAll(); // zdroje, munice, zbraně -> 0/empty
        }
        else
        {
            Debug.LogWarning("[GameManager] Nenašel jsem žádný PlayerInventory k wipe.");
        }
    }
    else
    {
        Debug.Log("[GameManager] DevKeepInventoryOnNewGame = true → inventář se nemaže.");
    }

    // --- Zbraně: zamknout všechno a vypnout instance ---
    WeaponHolder holder = p.GetComponentInChildren<WeaponHolder>(true);
    if (!holder)
    {
#if UNITY_2022_2_OR_NEWER
        holder = FindFirstObjectByType<WeaponHolder>(FindObjectsInactive.Include);
#else
        var allH = Resources.FindObjectsOfTypeAll<WeaponHolder>();
        foreach (var h in allH)
        {
            if (h && h.gameObject.scene.IsValid() && (h.gameObject.hideFlags & HideFlags.HideInHierarchy) == 0)
            { holder = h; break; }
        }
#endif
        if (!holder && WeaponHolder.Local)
            holder = WeaponHolder.Local;
    }

    if (holder)
    {
        Debug.Log("[GameManager] NewGame → locking all weapons.");

        // 1) preferuj veřejné API, pokud existuje (LockAll)
        var mi = typeof(WeaponHolder).GetMethod(
            "LockAll",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
        );
        if (mi != null)
        {
            // očekávaný sign.: void LockAll(bool keepDefaults)
            mi.Invoke(holder, new object[] { false });
        }
        else
        {
            // 2) fallback: tvrdě uzamkni sloty a vypni GO
            try
            {
                // Deaktivuj aktuální
                var cur = holder.Current;
                if (cur is MonoBehaviour curMb)
                {
                    try { cur.OnHolster(); } catch { /* ignore */ }
                    if (curMb && curMb.gameObject.activeSelf) curMb.gameObject.SetActive(false);
                }

                // Uzamkni všechny sloty a vypni instance
                if (holder.slots != null)
                {
                    foreach (var s in holder.slots)
                    {
                        if (s == null) continue;
                        s.unlocked = false;
                        if (s.instance && s.instance.gameObject.activeSelf)
                            s.instance.gameObject.SetActive(false);
                        if (s.weaponIfc is MonoBehaviour mb && mb.gameObject.activeSelf)
                            mb.gameObject.SetActive(false);
                    }
                }

                // Pokusně vynuluj Current přes reflexi (setter je private)
                var prop = typeof(WeaponHolder).GetProperty(
                    "Current",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic
                );
                if (prop != null && prop.CanWrite)
                    prop.SetValue(holder, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameManager] Fallback lock weapons selhal: {e.Message}");
            }
        }
    }
    else
    {
        Debug.LogWarning("[GameManager] WeaponHolder nenalezen – zbraně nezamknuty.");
    }

    // Proběhne jen jednou
    GameSaveController.IsNewGame = false;
}






    // === SceneLoaded → spawn hráče ===
    void OnSceneLoaded_SpawnPlayer(Scene s, LoadSceneMode mode)
    {
        if (s.name == "_Bootstrap") return;

        // Spawn jen pro právě cílovanou scénu
        if (!string.IsNullOrEmpty(_loadingLevelName) && s.name != _loadingLevelName)
            return;

        GameObject p = null;

        // 1) Pokud už ve stejné scéně hráč je, použij ho
        if (PlayerAlreadyExistsIn(s))
        {
            p = CurrentPlayer; // nastavuje se uvnitř PlayerAlreadyExistsIn()
        }
        else
        {
            // 2) Jinak spawnni nového
            if (!playerPrefab)
            {
                Debug.LogWarning("[GameManager] Player prefab není přiřazen.");
                return;
            }

            // Najdi spawn
            var spawn = FindSpawnInScene(s);
            var pos = spawn ? spawn.position : Vector3.zero;
            var rot = spawn ? spawn.rotation : Quaternion.identity;

            // Spawnni hráče
            p = Instantiate(playerPrefab, pos, rot);
            p.name = playerPrefab.name;

            // Jistota: přiřaď ho do právě načtené scény
            SceneManager.MoveGameObjectToScene(p, s);
        }

        // 3) Reset pouze při "Nové hře" (staty + případně inventář)
        ApplyNewGameResets(p);

        // 4) Ulož a vystřel eventy
        CurrentPlayer = p;
        OnPlayerSpawned?.Invoke(p);
        StartCoroutine(EmitSpawnNextFrame(p));

        // 5) Úklid loaderu
        pendingSpawnId = null;
        _loadingLevelName = null;
    }



    IEnumerator EmitSpawnNextFrame(GameObject player)
    {
        yield return null; // UI/Addressables ready
        if (player) OnPlayerSpawned?.Invoke(player);
    }

    bool PlayerAlreadyExists()
    {
        var go = FindExistingPlayerGO();
        if (go) { CurrentPlayer = go; return true; }
        return false;
    }

    GameObject FindExistingPlayerGO()
    {
        return GameObject.FindWithTag("Player"); // jednoduché a spolehlivé
    }

    Transform FindSpawnInScene(Scene scene)
    {
        // 0) PlayerSpawner komponenta
        PlayerSpawner candidate = null;

        foreach (var root in scene.GetRootGameObjects())
        {
            var spawners = root.GetComponentsInChildren<PlayerSpawner>(true);
            if (spawners == null || spawners.Length == 0) continue;

            if (!string.IsNullOrEmpty(pendingSpawnId))
            {
                foreach (var sp in spawners)
                    if (!string.IsNullOrEmpty(sp.id) && sp.id == pendingSpawnId)
                        return sp.transform;
            }

            foreach (var sp in spawners)
                if (sp.isDefault) candidate = sp;

            if (candidate == null) candidate = spawners[0];
        }

        if (candidate) return candidate.transform;

        // 1) Fallback: tag
        foreach (var root in scene.GetRootGameObjects())
        {
            if (!string.IsNullOrEmpty(playerSpawnTag))
            {
                if (root.CompareTag(playerSpawnTag)) return root.transform;
                var tagged = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in tagged) if (t.CompareTag(playerSpawnTag)) return t;
            }
        }

        // 2) Fallback: jméno
        foreach (var root in scene.GetRootGameObjects())
        {
            if (!string.IsNullOrEmpty(playerStartName))
            {
                if (root.name == playerStartName) return root.transform;
                var named = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in named) if (t.name == playerStartName) return t;
            }
        }

        return null;
    }

    // === Camera helpers ===
    void SetBootstrapCameraActive(bool on)
    {
        TryCacheBootstrapCameraAndAudio();
        if (bootstrapCamera) bootstrapCamera.enabled = on;
        if (bootstrapAudio)  bootstrapAudio.enabled  = on;
    }

    void TryCacheBootstrapCameraAndAudio()
    {
        if (bootstrapCamera && bootstrapAudio) return;

        var s = SceneManager.GetSceneByName("_Bootstrap");
        if (!s.IsValid()) return;

        foreach (var root in s.GetRootGameObjects())
        {
            if (!bootstrapCamera)
                bootstrapCamera = root.GetComponentInChildren<Camera>(true);
            if (!bootstrapAudio)
                bootstrapAudio = root.GetComponentInChildren<AudioListener>(true);
            if (bootstrapCamera && bootstrapAudio) break;
        }
    }
}
