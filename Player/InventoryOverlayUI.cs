using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Obscurus.Player;
using Obscurus.Items;

namespace Obscurus.UI
{
    [DisallowMultipleComponent]
    public class InventoryOverlayUI : MonoBehaviour
    {
        [Header("Core")]
        public CanvasGroup panel;
        public Button closeButton;

        [Header("Behavior")]
        public bool pauseGame = true;

        [Header("Tabs - Buttons")]
        public Button tabStatsBtn;
        public Button tabAlchemyBtn;
        public Button tabCraftBtn;
        public Button tabWeaponBtn;

        [Header("Tabs - Content Roots")]
        public RectTransform tabStatsContent;
        public RectTransform tabAlchemyContent;
        public RectTransform tabCraftContent;
        public RectTransform tabWeaponContent;

        [Header("Row Prefab (optional)")]
        public KeyValueRow kvRowPrefab;

        [Header("Stats Sources (auto-find if null)")]
        public HealthSystem  healthSystem;
        public ArmorSystem   armorSystem;
        public StaminaSystem staminaSystem;

        [Header("Stats UI (assign your own UI)")]
        public TMP_Text healthText;
        public TMP_Text armorText;
        public TMP_Text staminaText;
        public Button   healthPlusBtn;
        public Button   armorPlusBtn;
        public Button   staminaPlusBtn;
        
        [Header("Formatting")]
        public bool useFigureSpaces = true; // když vypneš, použijou se 2 normální mezery

        static readonly string TwoNormalSpaces  = " ";
        static readonly string TwoFigureSpaces  = "\u2007\u2007"; // "figure space" (šířka číslice)

        [Header("Tuning")]
        [Min(1f)] public float upgradeStep = 10f;
        public float uiRefreshInterval = 0.1f;

        // ===== INVENTORY (RESOURCES) =====
        [Header("Inventory — Resources (bind UI here)")]
        public PlayerInventory playerInventory;
        public bool autoFindInventory = true;

        [Serializable]
        public struct ResourceBinding
        {
            public ResourceKey key;
            public TMP_Text valueText;
            public TMP_InputField setInput;
            public Button setBtn;
            public Button plusBtn;
            public Button minusBtn;
            public int step;
        }
        public ResourceBinding[] resourceBindings;

        // ===== COSTS & FEEDBACK =====
        [Header("Costs")]
        [Min(1)] public int essencePerStatUpgrade = 1;

        [Header("Feedback (optional)")]
        public TMP_Text feedbackLabel;
        public float feedbackSeconds = 1.8f;
        Coroutine _feedbackCo;

        // ===== TABS =====
        enum Tab { Stats, Alchemy, Craft, Weapon }
        Tab _activeTab = Tab.Stats;

        bool isOpen;
        CursorLockMode prevLock;
        bool prevCursorVisible;
        float _refreshTimer;
        float _findTimer;

        public bool IsOpen => isOpen;

        void Awake()
        {
            // výchozí zobrazení – jen Stats
            OpenTab(Tab.Stats);

            if (autoFindInventory && !playerInventory)
                playerInventory = FindRuntimeObject<PlayerInventory>();

            if (autoFindSystems) AutoFindSystems();

            if (closeButton)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }

            WireTabButtons();       // ⬅️ nové
            WireButtons();
            WireResourceBindings();

            if (feedbackLabel) feedbackLabel.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (playerInventory) playerInventory.Changed += OnInventoryChanged;
        }
        void OnDisable()
        {
            if (playerInventory) playerInventory.Changed -= OnInventoryChanged;
        }

        void Start()
        {
            Hide();
            _findTimer = 0f;
            _refreshTimer = 0f;
            RefreshAllStats();
            RefreshAllResources();
        }

        void Update()
        {
            // dohledávání systémů
            if (autoFindSystems && (!healthSystem || !armorSystem || !staminaSystem))
            {
                _findTimer -= Time.unscaledDeltaTime;
                if (_findTimer <= 0f)
                {
                    if (AutoFindSystems() && isOpen)
                        RefreshAllStats();
                    _findTimer = Mathf.Max(0.1f, autoFindInterval);
                }
            }

            // dohledání inventáře
            if (autoFindInventory && !playerInventory)
            {
                _findTimer -= Time.unscaledDeltaTime;
                if (_findTimer <= 0f)
                {
                    playerInventory = FindRuntimeObject<PlayerInventory>();
                    if (playerInventory)
                    {
                        playerInventory.Changed += OnInventoryChanged;
                        if (isOpen) RefreshAllResources();
                    }
                    _findTimer = Mathf.Max(0.1f, autoFindInterval);
                }
            }

            if (!isOpen) return;

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f)
            {
                if (_activeTab == Tab.Stats) // refreshe má smysl pro Stats
                    RefreshAllStats();
                _refreshTimer = Mathf.Max(0.01f, uiRefreshInterval);
            }
        }

        // ---------- TAB SWITCHING ----------
        void WireTabButtons()
        {
            if (tabStatsBtn)
            {
                tabStatsBtn.onClick.RemoveAllListeners();
                tabStatsBtn.onClick.AddListener(() => OpenTab(Tab.Stats));
            }
            if (tabAlchemyBtn)
            {
                tabAlchemyBtn.onClick.RemoveAllListeners();
                tabAlchemyBtn.onClick.AddListener(() => OpenTab(Tab.Alchemy));
            }
            if (tabCraftBtn)
            {
                tabCraftBtn.onClick.RemoveAllListeners();
                tabCraftBtn.onClick.AddListener(() => OpenTab(Tab.Craft));
            }
            if (tabWeaponBtn)
            {
                tabWeaponBtn.onClick.RemoveAllListeners();
                tabWeaponBtn.onClick.AddListener(() => OpenTab(Tab.Weapon));
            }
        }

        void OpenTab(Tab tab)
        {
            _activeTab = tab;

            SetActive(tabStatsContent,   tab == Tab.Stats);
            SetActive(tabAlchemyContent, tab == Tab.Alchemy);
            SetActive(tabCraftContent,   tab == Tab.Craft);
            SetActive(tabWeaponContent,  tab == Tab.Weapon);

            // volitelné “vizuální” stavy tlačítek: aktivní = nekliknutelné
            if (tabStatsBtn)   tabStatsBtn.interactable   = tab != Tab.Stats;
            if (tabAlchemyBtn) tabAlchemyBtn.interactable = tab != Tab.Alchemy;
            if (tabCraftBtn)   tabCraftBtn.interactable   = tab != Tab.Craft;
            if (tabWeaponBtn)  tabWeaponBtn.interactable  = tab != Tab.Weapon;

            // jednorázové refreshe při přepnutí
            if (tab == Tab.Stats)   RefreshAllStats();
            if (tab == Tab.Alchemy) { /* případně zavolej refresh Alchemy tree UI, pokud máš komponentu */ }
        }

        // ---------- RESOURCES UI ----------
        void WireResourceBindings()
        {
            if (resourceBindings == null) return;

            for (int i = 0; i < resourceBindings.Length; i++)
            {
                var rb = resourceBindings[i];
                int step = rb.step > 0 ? rb.step : 1;
                var key  = rb.key;

                if (rb.plusBtn)
                {
                    rb.plusBtn.onClick.RemoveAllListeners();
                    rb.plusBtn.onClick.AddListener(() =>
                    {
                        if (!playerInventory) { Toast("Inventář nenalezen."); return; }
                        playerInventory.AddResource(key, step);
                        RefreshResource(rb);
                    });
                }

                if (rb.minusBtn)
                {
                    rb.minusBtn.onClick.RemoveAllListeners();
                    rb.minusBtn.onClick.AddListener(() =>
                    {
                        if (!playerInventory) { Toast("Inventář nenalezen."); return; }
                        playerInventory.SpendResource(key, step);
                        RefreshResource(rb);
                    });
                }

                if (rb.setBtn)
                {
                    rb.setBtn.onClick.RemoveAllListeners();
                    rb.setBtn.onClick.AddListener(() =>
                    {
                        if (!playerInventory || rb.setInput == null) return;
                        if (!int.TryParse(rb.setInput.text, out int target)) return;
                        target = Mathf.Max(0, target);
                        int cur = playerInventory.GetResource(key);
                        int delta = target - cur;
                        if (delta > 0) playerInventory.AddResource(key, delta);
                        else if (delta < 0) playerInventory.SpendResource(key, -delta);
                        RefreshResource(rb);
                    });
                }
            }
        }

        void OnInventoryChanged() => RefreshAllResources();

        void RefreshAllResources()
        {
            if (resourceBindings == null) return;
            for (int i = 0; i < resourceBindings.Length; i++)
                RefreshResource(resourceBindings[i]);
        }

        void RefreshResource(ResourceBinding rb)
        {
            if (!rb.valueText) return;
            int val = playerInventory ? playerInventory.GetResource(rb.key) : 0;
            rb.valueText.text = val.ToString();
        }

        // ---------- Stats UI ----------
        public bool autoFindSystems = true;
        public float autoFindInterval = 0.5f;

        void WireButtons()
        {
            if (healthPlusBtn)
            {
                healthPlusBtn.onClick.RemoveAllListeners();
                healthPlusBtn.onClick.AddListener(() =>
                {
                    if (!TryPayEssence()) return;
                    if (TryIncreaseMaxAndBoostCurrent(healthSystem, upgradeStep))
                        RefreshStatText(healthText, healthSystem);
                    else RefundEssence();
                });
            }
            if (armorPlusBtn)
            {
                armorPlusBtn.onClick.RemoveAllListeners();
                armorPlusBtn.onClick.AddListener(() =>
                {
                    if (!TryPayEssence()) return;
                    if (TryIncreaseMaxAndBoostCurrent(armorSystem, upgradeStep))
                        RefreshStatText(armorText, armorSystem);
                    else RefundEssence();
                });
            }
            if (staminaPlusBtn)
            {
                staminaPlusBtn.onClick.RemoveAllListeners();
                staminaPlusBtn.onClick.AddListener(() =>
                {
                    if (!TryPayEssence()) return;
                    if (TryIncreaseMaxAndBoostCurrent(staminaSystem, upgradeStep))
                        RefreshStatText(staminaText, staminaSystem);
                    else RefundEssence();
                });
            }
        }

        bool TryPayEssence()
        {
            if (!playerInventory) { Toast("Inventář nenalezen."); return false; }
            if (!playerInventory.SpendResource(ResourceKey.Essence, Mathf.Max(1, essencePerStatUpgrade)))
            {
                Toast("Nedostatek esence.");
                return false;
            }
            return true;
        }

        void RefundEssence()
        {
            if (!playerInventory) return;
            playerInventory.AddResource(ResourceKey.Essence, Mathf.Max(1, essencePerStatUpgrade));
        }

        void Toast(string msg)
        {
            if (!feedbackLabel)
            {
                Debug.Log($"[InventoryOverlayUI] {msg}");
                return;
            }
            feedbackLabel.text = msg;
            feedbackLabel.gameObject.SetActive(true);
            if (_feedbackCo != null) StopCoroutine(_feedbackCo);
            _feedbackCo = StartCoroutine(HideToast());
        }

        IEnumerator HideToast()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, feedbackSeconds));
            if (feedbackLabel) feedbackLabel.gameObject.SetActive(false);
        }

        void RefreshAllStats()
        {
            RefreshStatText(healthText,  healthSystem);
            RefreshStatText(armorText,   armorSystem);
            RefreshStatText(staminaText, staminaSystem);
        }

        void RefreshStatText(TMP_Text target, object system)
        {
            if (!target)
                return;

            string sep = " "; // jedna mezera

            if (system == null || !TryGetPair(system, out var cur, out var max))
            {
                target.text = $"--{sep}--";
                return;
            }

            target.text = $"{Mathf.RoundToInt(cur)}{sep}{Mathf.RoundToInt(max)}";
        }


        public void Toggle() { if (isOpen) Hide(); else Show(); }

        public void Show()
        {
            if (pauseGame) Time.timeScale = 0f;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (panel)
            {
                panel.alpha = 1f;
                panel.interactable = true;
                panel.blocksRaycasts = true;
            }

            prevLock = Cursor.lockState;
            prevCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            isOpen = true;

            if (autoFindSystems) AutoFindSystems();
            if (autoFindInventory && !playerInventory)
                playerInventory = FindRuntimeObject<PlayerInventory>();

            _refreshTimer = 0f;
            OpenTab(_activeTab);      // ⬅️ po otevření respektuj poslední aktivní tab
            RefreshAllResources();
        }

        public void Hide()
        {
            if (pauseGame)
            {
                bool otherOverlayOpen = MenuController.I != null && MenuController.I.IsOverlayOpen;
                if (!otherOverlayOpen)
                    Time.timeScale = 1f;
            }

            if (panel)
            {
                panel.alpha = 0f;
                panel.interactable = false;
                panel.blocksRaycasts = false;
            }
            Cursor.lockState = prevLock;
            Cursor.visible = prevCursorVisible;
            isOpen = false;
            gameObject.SetActive(false);
        }

        public void Close() => Hide();

        static void SetActive(Component c, bool v)
        {
            if (c) c.gameObject.SetActive(v);
        }

        // ---------- Auto-find ----------
        bool AutoFindSystems()
        {
            bool found = false;

            if (!healthSystem)
            {
                healthSystem = FindRuntimeObject<HealthSystem>();
                found |= healthSystem;
            }
            if (!armorSystem)
            {
                armorSystem = FindRuntimeObject<ArmorSystem>();
                found |= armorSystem;
            }
            if (!staminaSystem)
            {
                staminaSystem = FindRuntimeObject<StaminaSystem>();
                found |= staminaSystem;
            }
            return found;
        }

        static T FindRuntimeObject<T>() where T : Component
        {
#if UNITY_2022_2_OR_NEWER
            var obj = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (obj) return obj;
#endif
            var all = Resources.FindObjectsOfTypeAll<T>();
            foreach (var c in all)
            {
                if (!c) continue;
                var go = c.gameObject;
                if (go && go.scene.IsValid() && (go.hideFlags & HideFlags.HideInHierarchy) == 0)
                    return c;
            }
            return null;
        }

        // ================= Reflection helpers =================

        bool TryIncreaseMaxAndBoostCurrent(object system, float step)
        {
            if (system == null) return false;
            var t = system.GetType();

            if (!TryFindNumberMember(t, new[] { "max" }, out var maxProp, out var maxField, requireWritable:true))
                return false;

            float oldMax = ReadNumber(system, maxProp, maxField);
            float newMax = oldMax + step;

            if (!WriteNumber(system, maxProp, maxField, newMax)) return false;

            if (TryFindNumberMember(t, new[] { "current", "value" }, out var curProp, out var curField, requireWritable:true))
            {
                float oldCur = ReadNumber(system, curProp, curField);
                float newCur = Mathf.Min(newMax, oldCur + step);
                WriteNumber(system, curProp, curField, newCur);
            }
            else
            {
                var setCurrent = t.GetMethod("SetCurrent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(float) }, null);
                if (setCurrent != null)
                {
                    float oldCur = 0f;
                    TryGetFloat(t, system, new[] { "current", "value" }, out oldCur);
                    float newCur = Mathf.Min(newMax, oldCur + step);
                    setCurrent.Invoke(system, new object[] { newCur });
                }
            }

            return true;
        }

        static bool TryFindNumberMember(Type t, string[] names, out PropertyInfo prop, out FieldInfo field, bool requireWritable)
        {
            prop = null; field = null;
            foreach (var n in names)
            {
                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (p != null && (p.PropertyType == typeof(float) || p.PropertyType == typeof(int)))
                {
                    if (!requireWritable || (p.CanRead && p.CanWrite)) { prop = p; return true; }
                }
                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (f != null && (f.FieldType == typeof(float) || f.FieldType == typeof(int))) { field = f; return true; }
            }
            return false;
        }

        static float ReadNumber(object instance, PropertyInfo p, FieldInfo f)
        {
            if (p != null) return Convert.ToSingle(p.GetValue(instance, null));
            if (f != null) return Convert.ToSingle(f.GetValue(instance));
            return 0f;
        }

        static bool WriteNumber(object instance, PropertyInfo p, FieldInfo f, float value)
        {
            try
            {
                if (p != null)
                {
                    if (p.PropertyType == typeof(float)) { p.SetValue(instance, value); return true; }
                    if (p.PropertyType == typeof(int))   { p.SetValue(instance, Mathf.RoundToInt(value)); return true; }
                }
                if (f != null)
                {
                    if (f.FieldType == typeof(float)) { f.SetValue(instance, value); return true; }
                    if (f.FieldType == typeof(int))   { f.SetValue(instance, Mathf.RoundToInt(value)); return true; }
                }
            }
            catch { }
            return false;
        }

        bool TryGetPair(object system, out float current, out float max)
        {
            current = max = 0f;
            if (system == null) return false;

            var t = system.GetType();
            if (!TryGetFloat(t, system, new[] { "max" }, out max)) return false;

            if (!TryGetFloat(t, system, new[] { "current", "value" }, out current))
            {
                var m = t.GetMethod("GetCurrent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m != null && (m.ReturnType == typeof(float) || m.ReturnType == typeof(int)) && m.GetParameters().Length == 0)
                    current = Convert.ToSingle(m.Invoke(system, null));
            }
            return true;
        }

        static bool TryGetFloat(Type t, object instance, string[] names, out float v)
        {
            foreach (var n in names)
            {
                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (p != null && p.CanRead && (p.PropertyType == typeof(float) || p.PropertyType == typeof(int)))
                {
                    v = Convert.ToSingle(p.GetValue(instance, null));
                    return true;
                }
                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (f != null && (f.FieldType == typeof(float) || f.FieldType == typeof(int)))
                {
                    v = Convert.ToSingle(f.GetValue(instance));
                    return true;
                }
            }
            v = 0f;
            return false;
        }
    }
}
