using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

[DisallowMultipleComponent]
[AddComponentMenu("Obscurus/UI/HUD Slider Binder Pro")]
public class HudSliderBinderPro : MonoBehaviour
{
    // ---------- Jeden blok pro jeden bar ----------
    [System.Serializable]
    public class SliderBlock
    {
        [Header("UI")]
        [Tooltip("Slider, který budeme řídit (0..1).")]
        public Slider slider;

        [Tooltip("TextMeshPro label (např. \"100/120\"). Volitelné.")]
        public TMP_Text tmpLabel;
        [Tooltip("Legacy UI.Text label. Pokud nepoužíváš TMP.")]
        public Text legacyLabel;

        [Tooltip("CanvasGroup pro fade-in/out (doporučeno aspoň pro staminu).")]
        public CanvasGroup canvasGroup;

        [Header("Zobrazení / animace")]
        [Tooltip("Vynutit Min=0, Max=1, WholeNumbers=false při startu.")]
        public bool enforce01 = true;

        [Tooltip("Invertovat zobrazení (1-v).")]
        public bool invert = false;

        [Tooltip("Plynulé dorovnání hodnoty.")]
        public bool smooth = true;

        [Tooltip("Rychlost dorovnání (1/s). Unscaled time.")]
        public float lerpSpeed = 12f;

        [Tooltip("Menší hodnoty ber jako 0 (odstraní 'zbytkový pixel').")]
        [Range(0f, 0.03f)] public float epsilonFloor = 0.01f;

        [Header("Zero-proof (zbytky u 9-slice fill)")]
        [Tooltip("Skryje fill úplně, pokud je hodnota <= epsilonFloor.")]
        public bool hideFillAtZero = true;

        [Tooltip("Vynutí Image.type=Simple a vypne Preserve Aspect na Fill Image slideru (pomáhá proti 9-slice okrajům).")]
        public bool forceFillImageSimple = true;

        [Header("Auto-hide (typicky pro Staminu)")]
        [Tooltip("Když je plné, po chvíli se schová; při utrácení se ukáže.")]
        public bool autoHideWhenFull = true;
        [Tooltip("Doba fade-in/out (s).")]
        public float fadeDuration = 0.25f;
        [Tooltip("Jak dlouho zůstat viditelný po doplnění do plna (s).")]
        public float holdAfterFull = 1.0f;

        // ---- Runtime ----
        float _cur01 = 1f;
        float _target01 = 1f;
        float _lastCur = -1f;      // na detekci utrácení
        float _desiredAlpha = 1f;
        float _hideTimer = -1f;

        RectTransform _fillRect;
        Image _fillImage;
        
        bool _isFull = true;

        public void EnsureSetup()
        {
            if (slider && enforce01)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.wholeNumbers = false;
            }

            // pokusit se najít Fill Image slideru
            _fillRect = slider ? slider.fillRect : null;
            _fillImage = _fillRect ? _fillRect.GetComponent<Image>() : null;

            if (_fillImage && forceFillImageSimple)
            {
                _fillImage.type = Image.Type.Simple;     // žádný 9-slice
                _fillImage.preserveAspect = false;
            }

            if (!canvasGroup && slider)
                canvasGroup = slider.GetComponentInParent<CanvasGroup>();

            // výchozí viditelnost
            if (canvasGroup)
            {
                _desiredAlpha = autoHideWhenFull ? 0f : 1f;   // stamina: 0 → neviditelná
                canvasGroup.alpha = _desiredAlpha;
                canvasGroup.interactable = canvasGroup.blocksRaycasts = (_desiredAlpha > 0.5f);
            }
            
            if (!canvasGroup && slider && autoHideWhenFull)
                slider.gameObject.SetActive(true);

            UpdateLabel(1f, 1f);
            ApplySlider(true);
            ApplyZeroVisual();
        }

        public void InitialSet(float cur, float max)
        {
            UpdateFromPool(cur, max, immediate: true, forceShow: false);
            _lastCur = cur;
        }

        public void UpdateFromPool(float cur, float max, bool immediate, bool forceShow)
        {
            float v = (max <= 0f) ? 0f : Mathf.Clamp01(cur / max);
            if (invert) v = 1f - v;
            if (v < epsilonFloor) v = 0f;

            _target01 = v;
            if (immediate || !smooth) _cur01 = _target01;

            UpdateLabel(cur, max);
            ApplySlider(immediate);
            ApplyZeroVisual();

            // Auto-hide (stamina)
            // Auto-hide (stamina) – verze bez odpočtu v UpdateFromPool (ten probíhá v Tick)
            if (autoHideWhenFull) // záměrně bez "&& canvasGroup" kvůli fallbacku v SetVisible()
            {
                // uložíme si, jestli je plno – Tick() podle toho odtiká schování
                _isFull = cur >= max - 0.0001f;

                // utrácíme teď? (příp. vynucené zobrazení zvenku)
                bool spendingNow = (_lastCur >= 0f && cur < _lastCur - 0.0001f) || forceShow;

                if (spendingNow || !_isFull)
                {
                    // při utrácení, nebo pokud není plno → ukaž a resetni držení
                    _hideTimer = holdAfterFull;
                    SetVisible(true);
                }

                // POZOR: žádný _hideTimer -= Time.unscaledDeltaTime tady!
                // Odpočet a finální schování řeší Tick() při _isFull == true.
            }


            _lastCur = cur;
        }

        public void Tick()
        {
            if (smooth && slider && !Mathf.Approximately(_cur01, _target01))
            {
                _cur01 = Mathf.MoveTowards(_cur01, _target01, lerpSpeed * Time.unscaledDeltaTime);
                ApplySlider(false);
                ApplyZeroVisual();
            }

            if (canvasGroup && !Mathf.Approximately(canvasGroup.alpha, _desiredAlpha))
            {
                float step = (fadeDuration <= 0f) ? 1f : (Time.unscaledDeltaTime / Mathf.Max(0.0001f, fadeDuration));
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _desiredAlpha, step);
                canvasGroup.interactable = canvasGroup.blocksRaycasts = (canvasGroup.alpha > 0.5f);
            }
            // --- v Tick() na konec functonu přidej tento blok ---
            if (autoHideWhenFull)
            {
                if (_isFull)
                {
                    // první vstup do "plno" – nastartuj odpočet jednou
                    if (_hideTimer < 0f) _hideTimer = holdAfterFull;
                    // odpočítej plynule KAŽDÝ frame
                    else if (_hideTimer > 0f) _hideTimer -= Time.unscaledDeltaTime;

                    if (_hideTimer <= 0f) SetVisible(false);
                }
            }

        }

        void ApplySlider(bool force)
        {
            if (!slider) return;
            if (force || !Mathf.Approximately(slider.value, _cur01))
                slider.value = _cur01;
        }

        void ApplyZeroVisual()
        {
            if (!_fillImage) return;
            if (hideFillAtZero)
            {
                bool isZero = _cur01 <= epsilonFloor;
                if (_fillImage.enabled == isZero) // invert (enabled=false při 0)
                    _fillImage.enabled = !isZero;
            }
        }

        void UpdateLabel(float cur, float max)
        {
            string s = $"{Mathf.RoundToInt(cur)}/{Mathf.RoundToInt(max)}";
            if (tmpLabel) tmpLabel.text = s;
            if (legacyLabel) legacyLabel.text = s;
        }

        // --- v SetVisible(...) přidej fallback, když chybí CanvasGroup ---
        void SetVisible(bool on)
        {
            _desiredAlpha = on ? 1f : 0f;
            if (!smooth && canvasGroup)
            {
                canvasGroup.alpha = _desiredAlpha;
                canvasGroup.interactable = canvasGroup.blocksRaycasts = on;
            }

            // NEW: když nemáš CanvasGroup, aspoň zap/vyp objekt se sliderem
            if (!canvasGroup && slider)
                slider.gameObject.SetActive(on);
        }

    }

    // ---------- Přiřaď v inspektoru ----------
    [Header("Bars")]
    public SliderBlock hp;
    public SliderBlock armor;
    public SliderBlock stamina; // pro staminu zapni autoHideWhenFull

    [Header("Hledání hráče / systémů")]
    public string playerTag = "Player";
    public float retryEverySeconds = 0.5f;
    public bool rebindOnSceneChange = true;

    [Header("Fallback polling (když někdo mění hodnoty bez eventů)")]
    [Tooltip("Pravidelně přečteme Current/Max a přepíšeme UI, i kdyby nepřišla událost OnChanged.")]
    public bool pollIfNoEvents = true;
    [Tooltip("Interval pollingu v sekundách.")]
    public float pollInterval = 0.2f;

    // runtime
    GameObject playerGO;
    HealthSystem  health;
    ArmorSystem   armorSys;
    StaminaSystem staminaSys;
    bool boundH, boundA, boundS;
    Coroutine loop;
    float pollT;

    // ---------- Lifecycle ----------
    void Reset()
    {
        if (stamina != null)
        {
            stamina.autoHideWhenFull = true;
            stamina.fadeDuration = 0.25f;
            stamina.holdAfterFull = 1.0f;
        }
    }

    void OnEnable()
    {
        // → jistota, že stamina je defaultně skrytá + bude fadeovat
        if (stamina != null)
        {
            stamina.autoHideWhenFull = true;  // kdybys omylem vypnul v Inspectoru
            stamina.fadeDuration     = Mathf.Max(0.05f, stamina.fadeDuration);
            stamina.holdAfterFull    = Mathf.Max(0f, stamina.holdAfterFull);
        }

        hp?.EnsureSetup();
        armor?.EnsureSetup();
        stamina?.EnsureSetup();

        GameManager.OnPlayerSpawned += OnPlayerSpawned;
        GameManager.OnPlayerDespawned += OnPlayerDespawned;

        if (rebindOnSceneChange)
            SceneManager.activeSceneChanged += OnSceneChanged;

        StatsHudRefreshBus.OnRefreshRequested += HardRefreshNow;

        loop = StartCoroutine(BindLoop());
    }


    void OnDisable()
    {
        GameManager.OnPlayerSpawned -= OnPlayerSpawned;
        GameManager.OnPlayerDespawned -= OnPlayerDespawned;
        if (rebindOnSceneChange)
            SceneManager.activeSceneChanged -= OnSceneChanged;

        // <<< PŘIDÁNO: odhlášení z busu
        StatsHudRefreshBus.OnRefreshRequested -= HardRefreshNow;

        if (loop != null) StopCoroutine(loop);
        UnsubAll();
    }

    void Update()
    {
        hp?.Tick();
        armor?.Tick();
        stamina?.Tick();

        // Fallback polling – řeší upgrady/max změny bez RaiseChanged
        if (pollIfNoEvents && (health || armorSys || staminaSys))
        {
            pollT -= Time.unscaledDeltaTime;
            if (pollT <= 0f)
            {
                pollT = pollInterval;

                if (health && hp != null)
                    hp.UpdateFromPool(health.Current, health.max, immediate:false, forceShow:false);
                if (armorSys && armor != null)
                    armor.UpdateFromPool(armorSys.Current, armorSys.max, immediate:false, forceShow:false);
                if (staminaSys && stamina != null)
                    stamina.UpdateFromPool(staminaSys.Current, staminaSys.max, immediate:false, forceShow:false);
            }
        }
    }

    // ---------- Scene / GM hooks ----------
    void OnSceneChanged(Scene a, Scene b) => Rebind(true);
    void OnPlayerSpawned(GameObject go)   => BindFromPlayer(go, immediate: true);
    void OnPlayerDespawned()              => Rebind(true);

    // ---------- Binding ----------
    IEnumerator BindLoop()
    {
        TryBind();
        var wait = new WaitForSecondsRealtime(retryEverySeconds);
        while (true)
        {
            if (!IsBoundAll())
                TryBind();
            yield return wait;
        }
    }

    bool IsBoundAll() => health && armorSys && staminaSys && boundH && boundA && boundS;

    void Rebind(bool force)
    {
        if (force)
        {
            UnsubAll();
            playerGO = null;
            health = null; armorSys = null; staminaSys = null;
            boundH = boundA = boundS = false;
        }
        TryBind();
    }

    void TryBind()
    {
        if (!playerGO && GameManager.I != null && GameManager.I.CurrentPlayer)
            playerGO = GameManager.I.CurrentPlayer;

        if (!playerGO && !string.IsNullOrEmpty(playerTag))
            playerGO = GameObject.FindGameObjectWithTag(playerTag);

        if (playerGO)
        {
            if (!health)     health     = playerGO.GetComponentInChildren<HealthSystem>(true);
            if (!armorSys)   armorSys   = playerGO.GetComponentInChildren<ArmorSystem>(true);
            if (!staminaSys) staminaSys = playerGO.GetComponentInChildren<StaminaSystem>(true);
        }

#if UNITY_2023_1_OR_NEWER
        if (!health)     health     = Object.FindFirstObjectByType<HealthSystem>(FindObjectsInactive.Exclude);
        if (!armorSys)   armorSys   = Object.FindFirstObjectByType<ArmorSystem>(FindObjectsInactive.Exclude);
        if (!staminaSys) staminaSys = Object.FindFirstObjectByType<StaminaSystem>(FindObjectsInactive.Exclude);
        if (!playerGO && health)     playerGO = health.gameObject;
        if (!playerGO && armorSys)   playerGO = armorSys.gameObject;
        if (!playerGO && staminaSys) playerGO = staminaSys.gameObject;
#else
        if (!health)     health     = Object.FindObjectOfType<HealthSystem>();
        if (!armorSys)   armorSys   = Object.FindObjectOfType<ArmorSystem>();
        if (!staminaSys) staminaSys = Object.FindObjectOfType<StaminaSystem>();
        if (!playerGO && health)     playerGO = health.gameObject;
        if (!playerGO && armorSys)   playerGO = armorSys.gameObject;
        if (!playerGO && staminaSys) playerGO = staminaSys.gameObject;
#endif

        if (health && !boundH)
        {
            health.OnChanged += OnHpChanged;
            boundH = true;
            hp?.InitialSet(health.Current, health.max);
        }
        if (armorSys && !boundA)
        {
            armorSys.OnChanged += OnArmorChanged;
            boundA = true;
            armor?.InitialSet(armorSys.Current, armorSys.max);
        }
        if (staminaSys && !boundS)
        {
            staminaSys.OnChanged += OnStaminaChanged;
            boundS = true;
            stamina?.InitialSet(staminaSys.Current, staminaSys.max);
        }
    }

    void BindFromPlayer(GameObject go, bool immediate)
    {
        if (!go) return;

        UnsubAll();
        playerGO = go;

        health     = go.GetComponentInChildren<HealthSystem>(true);
        armorSys   = go.GetComponentInChildren<ArmorSystem>(true);
        staminaSys = go.GetComponentInChildren<StaminaSystem>(true);

        if (health)
        {
            health.OnChanged += OnHpChanged; boundH = true;
            if (hp != null)
            {
                if (immediate) hp.InitialSet(health.Current, health.max);
                else           hp.UpdateFromPool(health.Current, health.max, false, false);
            }
        }
        if (armorSys)
        {
            armorSys.OnChanged += OnArmorChanged; boundA = true;
            if (armor != null)
            {
                if (immediate) armor.InitialSet(armorSys.Current, armorSys.max);
                else           armor.UpdateFromPool(armorSys.Current, armorSys.max, false, false);
            }
        }
        if (staminaSys)
        {
            staminaSys.OnChanged += OnStaminaChanged; boundS = true;
            if (stamina != null)
            {
                if (immediate) stamina.InitialSet(staminaSys.Current, staminaSys.max);
                else           stamina.UpdateFromPool(staminaSys.Current, staminaSys.max, false, false);
            }
        }
    }

    void UnsubAll()
    {
        if (health && boundH)       health.OnChanged -= OnHpChanged;
        if (armorSys && boundA)     armorSys.OnChanged -= OnArmorChanged;
        if (staminaSys && boundS)   staminaSys.OnChanged -= OnStaminaChanged;
        boundH = boundA = boundS = false;
    }

    // ---------- Handlery ----------
    void OnHpChanged(float cur, float max)
    {
        if (hp == null) return;
        hp.UpdateFromPool(cur, max, immediate:false, forceShow:false);
    }

    void OnArmorChanged(float cur, float max)
    {
        if (armor == null) return;
        armor.UpdateFromPool(cur, max, immediate:false, forceShow:false);
    }

    void OnStaminaChanged(float cur, float max)
    {
        if (stamina == null) return;
        stamina.UpdateFromPool(cur, max, immediate:false, forceShow:false);
    }

    // >>> PŘIDÁNO: okamžitý tvrdý refresh po upgradu (ping ze sběrnice)
    void HardRefreshNow()
    {
        if (health    && hp      != null) hp.UpdateFromPool(health.Current,     health.max,     immediate:true, forceShow:true);
        if (armorSys  && armor   != null) armor.UpdateFromPool(armorSys.Current, armorSys.max,  immediate:true, forceShow:true);
        if (staminaSys&& stamina != null) stamina.UpdateFromPool(staminaSys.Current, staminaSys.max, immediate:true, forceShow:true);
    }
}
