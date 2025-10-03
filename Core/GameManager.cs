using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    // 🔔 Události pro UI a další systémy
    public static event Action<GameObject> OnPlayerSpawned;
    public static event Action OnPlayerDespawned;

    // Živá reference na hráče (pokud existuje v aktuální scéně)
    public GameObject CurrentPlayer { get; private set; }

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

    // === Named spawn (volitelné) ===
    [SerializeField] string pendingSpawnId; // jednorázově před LoadLevel()
    public void SetNextSpawn(string id) => pendingSpawnId = id;

    // === Cílová scéna, která se právě loaduje (kvůli OnSceneLoaded pořadí)
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
        SaveManager.OnAfterLoad += HandleSaveAfterLoad;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded_SpawnPlayer;
        SaveManager.OnAfterLoad -= HandleSaveAfterLoad;
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

    void HandleSaveAfterLoad()
    {
        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.name != Scenes.Bootstrap)
            CurrentLevel = active.name;
        else
            CurrentLevel = null;

        SetBootstrapCameraActive(false);
        if (lockCursorInGameplay) EnableGameplayCursor(); else EnableMenuCursor();

        // ⬇️ Po načtení savu může hráč už existovat – dej vědět UI
        var existing = FindExistingPlayerGO();
        if (existing)
        {
            CurrentPlayer = existing;
            Debug.Log("[GameManager] HandleSaveAfterLoad → player exists, sending OnPlayerSpawned");
            OnPlayerSpawned?.Invoke(existing);
            StartCoroutine(EmitSpawnNextFrame(existing)); // pojistka o frame později
        }
        else
        {
            if (CurrentPlayer) { CurrentPlayer = null; }
            Debug.Log("[GameManager] HandleSaveAfterLoad → no player, sending OnPlayerDespawned");
            OnPlayerDespawned?.Invoke();
        }
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
        // 🔧 DŮLEŽITÉ: označ cílovou scénu hned, ať OnSceneLoaded ví, že má spawnovat
        _loadingLevelName = levelName;
        CurrentLevel = levelName; // InLevel = true už během SceneLoaded
        StartCoroutine(LoadLevelRoutine(levelName));
    }

    IEnumerator LoadLevelRoutine(string levelName)
    {
        // 0) Oznám předchozí „despawn“, pokud nějaký hráč žije
        if (CurrentPlayer)
        {
            Debug.Log("[GameManager] LoadLevelRoutine → pre-despawn");
            OnPlayerDespawned?.Invoke();
            CurrentPlayer = null;
        }

        // 1) Unload vše kromě _Bootstrap
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != "_Bootstrap")
            {
                var u = SceneManager.UnloadSceneAsync(s);
                if (u != null) while (!u.isDone) yield return null;
            }
        }

        // 2) Load additivně
        var op = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;

        // 3) Nastav jako active
        var lvl = SceneManager.GetSceneByName(levelName);
        if (lvl.IsValid()) SceneManager.SetActiveScene(lvl);

        Time.timeScale = 1f;

        // 4) Bootstrap cam/audio OFF
        SetBootstrapCameraActive(false);

        // 5) Kurzor do gameplay
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
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != "_Bootstrap")
            {
                var u = SceneManager.UnloadSceneAsync(s);
                if (u != null) while (!u.isDone) yield return null;
            }
        }
        CurrentLevel = null;
        _loadingLevelName = null;
        Time.timeScale = 1f;

        // 🚪 Odchod z levelu => despawn
        if (CurrentPlayer)
        {
            CurrentPlayer = null;
        }
        Debug.Log("[GameManager] ReturnToMainMenu → sending OnPlayerDespawned");
        OnPlayerDespawned?.Invoke();

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

    // === SceneLoaded → spawn hráče ===
    // === SceneLoaded → spawn hráče ===
void OnSceneLoaded_SpawnPlayer(Scene s, LoadSceneMode mode)
{
    if (s.name == "_Bootstrap") return;

    // Spawn jen pro právě cílovanou scénu (chrání před additivními loady)
    if (!string.IsNullOrEmpty(_loadingLevelName) && s.name != _loadingLevelName)
        return;

    if (PlayerAlreadyExists())
    {
        // Hráč už existuje (např. načten save / instancován jinde)
        if (CurrentPlayer)
        {
            Debug.Log("[GameManager] SceneLoaded → player already exists, sending OnPlayerSpawned");
            OnPlayerSpawned?.Invoke(CurrentPlayer);
            StartCoroutine(EmitSpawnNextFrame(CurrentPlayer)); // pojistka o frame později
        }
        return;
    }

    if (!playerPrefab)
    {
        Debug.LogWarning("[GameManager] Player prefab není přiřazen.");
        return;
    }

    // --- Najdi spawn point ---
    var spawn = FindSpawnInScene(s);
    var pos = spawn ? spawn.position : Vector3.zero;
    var rot = spawn ? spawn.rotation : Quaternion.identity;

    // --- Spawn hráče ---
    var player = Instantiate(playerPrefab, pos, rot);
    player.name = playerPrefab.name;

    // --- Reset statů jen pokud jde o novou hru ---
    if (SaveManager.IsNewGame)
    {
        var health  = player.GetComponent<HealthSystem>();
        var armor   = player.GetComponent<ArmorSystem>();
        var stamina = player.GetComponent<StaminaSystem>();

        health?.ResetToBase();
        armor?.ResetToBase();
        stamina?.ResetToBase();
    }

    // --- Ulož referenci a vystřel event ---
    CurrentPlayer = player;
    Debug.Log("[GameManager] Spawned Player → sending OnPlayerSpawned");
    OnPlayerSpawned?.Invoke(player);
    StartCoroutine(EmitSpawnNextFrame(player));

    Debug.Log(spawn
        ? $"[GameManager] Spawned Player at '{spawn.name}' ({spawn.position})"
        : "[GameManager] Spawn not found -> (0,0,0)");

    // jednorázové hodnoty pryč
    pendingSpawnId = null;
    _loadingLevelName = null;
}


    IEnumerator EmitSpawnNextFrame(GameObject player)
    {
        // Počkej do dalšího framu – UI (Addressables/HUD) už bude OnEnable/Start připravené
        yield return null;
        if (player)
        {
            Debug.Log("[GameManager] EmitSpawnNextFrame → sending OnPlayerSpawned (delayed)");
            OnPlayerSpawned?.Invoke(player);
        }
    }

    bool PlayerAlreadyExists()
    {
        var go = FindExistingPlayerGO();
        if (go)
        {
            CurrentPlayer = go;
            return true;
        }
        return false;
    }

    GameObject FindExistingPlayerGO()
    {
        var tagged = GameObject.FindWithTag("Player");
        if (tagged) return tagged;

        var agents = FindObjectsOfType<SaveAgent>(true);
        foreach (var a in agents)
            if (a && a.role == SaveAgent.Role.Player)
                return a.gameObject;

        return null;
    }

    Transform FindSpawnInScene(Scene scene)
    {
        // 0) Preferuj PlayerSpawner komponentu
        PlayerSpawner candidate = null;

        foreach (var root in scene.GetRootGameObjects())
        {
            var spawners = root.GetComponentsInChildren<PlayerSpawner>(true);
            if (spawners == null || spawners.Length == 0) continue;

            // a) podle pendingSpawnId
            if (!string.IsNullOrEmpty(pendingSpawnId))
            {
                foreach (var sp in spawners)
                    if (!string.IsNullOrEmpty(sp.id) && sp.id == pendingSpawnId)
                        return sp.transform;
            }

            // b) isDefault
            foreach (var sp in spawners)
                if (sp.isDefault)
                    candidate = sp;

            // c) první nalezený
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
