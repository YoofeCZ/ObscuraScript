using UnityEngine;

[DefaultExecutionOrder(2000)] // zpracuj až po jiných UI / kontrolerech
public class CrosshairController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject crosshairRoot; // přetáhni svůj crosshair objekt (UI GameObject)
    [SerializeField] private Obscurus.UI.InventoryOverlayUI inventoryOverlay; // volitelné – auto-find
    [SerializeField] private MenuController menuController; // volitelné – auto-find

    [Header("Behavior")]
    [Tooltip("Dohledat overlay/menu automaticky, pokud nejsou přiřazeny.")]
    [SerializeField] private bool autoFindUIRefs = true;

    [Tooltip("Dohledávat hráče průběžně, pokud event dorazí dřív než UI.")]
    [SerializeField] private bool pollForPlayer = true;

    private bool _playerAlive;
    private bool _lastAppliedActive;
    private float _refindTimer;
    private bool _loggedOnce;

    void Awake()
    {
        if (crosshairRoot) crosshairRoot.SetActive(false);
        TryAutoFindUI();
    }

    void OnEnable()
    {
        GameManager.OnPlayerSpawned  += HandlePlayerSpawned;
        GameManager.OnPlayerDespawned += HandlePlayerDespawned;

        // Pojistka: pokud už ve scéně hráč je, rovnou zapni
        if (IsPlayerPresentNow())
        {
            _playerAlive = true;
            EvaluateAndApply();
        }
    }

    void OnDisable()
    {
        GameManager.OnPlayerSpawned  -= HandlePlayerSpawned;
        GameManager.OnPlayerDespawned -= HandlePlayerDespawned;
    }

    void Update()
    {
        // UI reference se mohou načíst později (Addressables) → občas zkus dohledat
        if (autoFindUIRefs && (!inventoryOverlay || !menuController))
        {
            _refindTimer -= Time.unscaledDeltaTime;
            if (_refindTimer <= 0f)
            {
                TryAutoFindUI();
                _refindTimer = 0.25f;
            }
        }

        // Volitelný polling hráče, pokud event proběhl dřív než subscribe
        if (pollForPlayer && !_playerAlive && IsPlayerPresentNow())
            _playerAlive = true;

        EvaluateAndApply();
    }

    void HandlePlayerSpawned(GameObject player)
    {
        _playerAlive = true;
        if (!_loggedOnce)
        {
            Debug.Log("[Crosshair] OnPlayerSpawned received → will show if not in menus.");
            _loggedOnce = true;
        }
        EvaluateAndApply();
    }

    void HandlePlayerDespawned()
    {
        _playerAlive = false;
        Apply(false);
    }

    bool IsPlayerPresentNow()
    {
        if (GameManager.I != null && GameManager.I.CurrentPlayer != null) return true;
        var tagged = GameObject.FindWithTag("Player");
        return tagged != null;
    }

    void EvaluateAndApply()
    {
        // Hlavní logika: crosshair jen když hráč žije a nejsi v menu/pausě
        bool inMenus =
            (menuController != null && menuController.IsPaused) ||                // pauza
            (inventoryOverlay != null && inventoryOverlay.IsOpen) ||              // inventář
            (Cursor.lockState != CursorLockMode.Locked) ||                        // kurzor není zamknutý → UI režim
            (Time.timeScale == 0f);                                               // pauza přes TS

        bool shouldBeVisible = _playerAlive && !inMenus;
        Apply(shouldBeVisible);
    }

    void Apply(bool visible)
    {
        if (!crosshairRoot)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[CrosshairController] Není přiřazen 'crosshairRoot'.");
#endif
            return;
        }

        if (_lastAppliedActive == visible) return;
        crosshairRoot.SetActive(visible);

#if UNITY_EDITOR
        Debug.Log($"[Crosshair] SetActive({visible})  alive={_playerAlive}, locked={(Cursor.lockState==CursorLockMode.Locked)}, ts={Time.timeScale}, inv={(inventoryOverlay && inventoryOverlay.IsOpen)}, paused={(menuController && menuController.IsPaused)}");
#endif

        _lastAppliedActive = visible;
    }

    void TryAutoFindUI()
    {
        if (!autoFindUIRefs) return;

        if (!menuController)
        {
#if UNITY_2022_2_OR_NEWER
            menuController = Object.FindFirstObjectByType<MenuController>(FindObjectsInactive.Include);
#else
            var menus = Resources.FindObjectsOfTypeAll<MenuController>();
            foreach (var m in menus)
            {
                var go = m ? m.gameObject : null;
                if (go && go.scene.IsValid() && (go.hideFlags & HideFlags.HideInHierarchy) == 0) { menuController = m; break; }
            }
#endif
        }

        if (!inventoryOverlay)
        {
#if UNITY_2022_2_OR_NEWER
            inventoryOverlay = Object.FindFirstObjectByType<Obscurus.UI.InventoryOverlayUI>(FindObjectsInactive.Include);
#else
            var ovs = Resources.FindObjectsOfTypeAll<Obscurus.UI.InventoryOverlayUI>();
            foreach (var o in ovs)
            {
                var go = o ? o.gameObject : null;
                if (go && go.scene.IsValid() && (go.hideFlags & HideFlags.HideInHierarchy) == 0) { inventoryOverlay = o; break; }
            }
#endif
        }
    }
}
