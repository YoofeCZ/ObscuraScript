using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using Obscurus.Save;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    public static event Action<GameObject> OnPlayerSpawned;
    public static event Action OnPlayerDespawned;

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
    [SerializeField] string playerSpawnTag = "PlayerSpawn";
    [SerializeField] string playerStartName = "PlayerStart";

    [SerializeField] string pendingSpawnId;
    public void SetNextSpawn(string id) => pendingSpawnId = id;

    string _loadingLevelName;
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

    IEnumerator LoadLevelRoutine(string levelName)
    {
        // despawn starého hráče
        if (CurrentPlayer)
        {
            OnPlayerDespawned?.Invoke();
            CurrentPlayer = null;
        }

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

        // načti scénu additivně
        var op = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;

        // nastav jako aktivní
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

        if (CurrentPlayer)
        {
            CurrentPlayer = null;
        }
        OnPlayerDespawned?.Invoke();

        SetBootstrapCameraActive(true);
        if (showCursorInMenus) EnableMenuCursor();
    }

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

    // po načtení scény spawnuje hráče
    void OnSceneLoaded_SpawnPlayer(Scene s, LoadSceneMode mode)
    {
        if (s.name == "_Bootstrap") return;

        // ochrana proti additivním loadům
        if (!string.IsNullOrEmpty(_loadingLevelName) && s.name != _loadingLevelName)
            return;

        if (PlayerAlreadyExists())
        {
            if (CurrentPlayer)
            {
                OnPlayerSpawned?.Invoke(CurrentPlayer);
                StartCoroutine(EmitSpawnNextFrame(CurrentPlayer));
            }
            return;
        }

        if (!playerPrefab)
        {
            Debug.LogWarning("[GameManager] Player prefab není přiřazen.");
            return;
        }

        var spawn = FindSpawnInScene(s);
        var pos = spawn ? spawn.position : Vector3.zero;
        var rot = spawn ? spawn.rotation : Quaternion.identity;

        var player = Instantiate(playerPrefab, pos, rot);
        player.name = playerPrefab.name;

        if (GameSaveController.IsNewGame)
        {
            var health  = player.GetComponent<HealthSystem>();
            var armor   = player.GetComponent<ArmorSystem>();
            var stamina = player.GetComponent<StaminaSystem>();
            health?.ResetToBase();
            armor?.ResetToBase();
            stamina?.ResetToBase();
        }

        CurrentPlayer = player;
        OnPlayerSpawned?.Invoke(player);
        StartCoroutine(EmitSpawnNextFrame(player));

        pendingSpawnId = null;
        _loadingLevelName = null;
    }

    IEnumerator EmitSpawnNextFrame(GameObject player)
    {
        yield return null;
        if (player) OnPlayerSpawned?.Invoke(player);
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
        // už nehledáme SaveAgent, jen tag "Player"
        return GameObject.FindWithTag("Player");
    }

    Transform FindSpawnInScene(Scene scene)
    {
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
                if (sp.isDefault)
                    candidate = sp;

            if (candidate == null) candidate = spawners[0];
        }

        if (candidate) return candidate.transform;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (!string.IsNullOrEmpty(playerSpawnTag))
            {
                if (root.CompareTag(playerSpawnTag)) return root.transform;
                var tagged = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in tagged)
                    if (t.CompareTag(playerSpawnTag)) return t;
            }
        }

        foreach (var root in scene.GetRootGameObjects())
        {
            if (!string.IsNullOrEmpty(playerStartName))
            {
                if (root.name == playerStartName) return root.transform;
                var named = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in named)
                    if (t.name == playerStartName) return t;
            }
        }

        return null;
    }

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
