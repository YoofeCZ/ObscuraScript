#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Obscurus.Save;
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

namespace Obscurus
{
    [DisallowMultipleComponent]
#if HAS_ODIN
    [HideMonoScript, InfoBox("Game Over UI – fade to black + velký titul, po fadu vysune kartu s tlačítky.")]
#endif
    public class GameOverUI : MonoBehaviour
    {
        [Header("Root/Focus")]
        public GameObject panelRoot;
        public Selectable firstSelected;

        [Header("Fade")]
        [Tooltip("Zpoždění před začátkem fadu (s, unscaled).")]
        public float startDelay = 1.0f;
        public Image blackout;
        public TextMeshProUGUI overTitle;
        [Tooltip("Cílová neprůhlednost černého pozadí po fadu.")]
        public float blackoutTargetAlpha = 1.0f;
        [Tooltip("Délka fadu pozadí (s).")]
        public float fadeDuration = 1.0f;
        [Tooltip("Kdy začít rozsvěcet nápis během fadu.")]
        public float titleFadeDelay = 0.15f;
        [Tooltip("Délka fadu nápisu (s).")]
        public float titleFadeDuration = 0.6f;
        [Tooltip("Po fadu pauza než se objeví karta (s).")]
        public float showMenuDelay = 0.25f;
        [Tooltip("Po zobrazení karty skrýt velký fade titul.")]
        public bool hideFadeTitleWhenMenuShows = true;

        [Header("Card/Menu")]
        public RectTransform card;
        public CanvasGroup cardCg;
        public float cardShowDuration = 0.18f;
        public float cardPopScale = 1.04f;

        [Header("Behavior")]
        public bool freezeOnShow = true;
        public bool unfreezeOnHideOrLeave = true;

        [Header("Integrations")]
        [Tooltip("Při zobrazení Game Over zavřít vývojářskou konzoli, pokud je otevřená.")]
        public bool closeConsoleOnShow = true;

        public static event Action GameOverShown;

        CanvasGroup _rootCg;
        bool _shown;
        static bool _cursorWasLocked;
        static CursorLockMode _prevLock;

        void Awake()
        {
            var root = panelRoot ? panelRoot : gameObject;

            _rootCg = root.GetComponent<CanvasGroup>();
            if (!_rootCg) _rootCg = root.AddComponent<CanvasGroup>();
            _rootCg.alpha = 0f;
            _rootCg.blocksRaycasts = false;
            _rootCg.interactable = false;

            if (card && !cardCg) cardCg = card.GetComponent<CanvasGroup>();
            if (card && !cardCg) cardCg = card.gameObject.AddComponent<CanvasGroup>();

            root.SetActive(true);

            if (blackout) { var c = blackout.color; blackout.color = new Color(c.r,c.g,c.b,0f); blackout.raycastTarget = true; }
            if (overTitle) overTitle.alpha = 0f;
            if (cardCg) { cardCg.alpha = 0f; cardCg.blocksRaycasts = false; cardCg.interactable = false; }
        }
        // přidej do GameOverUI
        void BringToFront()
        {
            if (panelRoot) panelRoot.transform.SetAsLastSibling(); // GameOver overlay nad ostatními (pro fade + kartu)
        }

        void SendBehindMenus()
        {
            if (panelRoot) panelRoot.transform.SetAsFirstSibling(); // overlay pod ostatní UI panely (main/pause/load)
        }

        public void Show()
        {
            if (_shown) return;
            _shown = true;
            BringToFront();

            if (freezeOnShow) FreezeGameplay(true);
            EnsureParentsActive();
            if (closeConsoleOnShow) TryCloseConsole();

            _rootCg.alpha = 1f;
            _rootCg.blocksRaycasts = true;
            _rootCg.interactable   = false;

            if (blackout) { var c = blackout.color; blackout.color = new Color(c.r,c.g,c.b,0f); }
            if (overTitle) overTitle.alpha = 0f;
            if (cardCg)
            {
                cardCg.alpha = 0f;
                cardCg.blocksRaycasts = false;
                cardCg.interactable = false;
            }
            if (card) card.localScale = Vector3.one * cardPopScale;

            StartCoroutine(ShowRoutine());
        }

        IEnumerator ShowRoutine()
        {
            if (startDelay > 0f)
                yield return new WaitForSecondsRealtime(startDelay);

            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fadeDuration);

                if (blackout)
                {
                    var b = blackout.color;
                    b.a = Mathf.Lerp(0f, blackoutTargetAlpha, Smooth01(k));
                    blackout.color = b;
                }

                if (overTitle)
                {
                    float kt = Mathf.InverseLerp(titleFadeDelay, titleFadeDelay + titleFadeDuration, t);
                    overTitle.alpha = Mathf.Clamp01(Smooth01(kt));
                }

                yield return null;
            }

            if (showMenuDelay > 0f)
                yield return new WaitForSecondsRealtime(showMenuDelay);

            SetupCursor(true);

            float t2 = 0f;
            while (t2 < cardShowDuration)
            {
                t2 += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t2 / cardShowDuration);

                if (card) card.localScale = Vector3.one * Mathf.Lerp(cardPopScale, 1f, EaseOutQuad(k));
                if (cardCg) cardCg.alpha = k;

                yield return null;
            }

            if (card) card.localScale = Vector3.one;
            if (cardCg) { cardCg.alpha = 1f; cardCg.blocksRaycasts = true; cardCg.interactable = true; }
            

            FocusFirst();
            _rootCg.interactable = true;

            GameOverShown?.Invoke();
        }

        public void Hide(bool restoreTimeScale = true, bool keepCursor = false, bool soft = false)
        {
            if (!_shown) return;
            _shown = false;

            _rootCg.alpha = 0f;
            _rootCg.blocksRaycasts = false;
            _rootCg.interactable = false;

            if (!keepCursor) SetupCursor(false);
            if (unfreezeOnHideOrLeave && restoreTimeScale) FreezeGameplay(false);
        }

        void EnsureParentsActive()
        {
            var t = panelRoot ? panelRoot.transform : transform;
            for (Transform p = t; p != null; p = p.parent)
                if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
        }
        void FocusFirst()
        {
            if (!firstSelected) return;
            var es = EventSystem.current;
            if (!es)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es = go.GetComponent<EventSystem>();
            }
            es.SetSelectedGameObject(firstSelected.gameObject);
        }
        void SetupCursor(bool visible)
        {
            if (visible)
            {
                _cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
                _prevLock = Cursor.lockState;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.visible = !_cursorWasLocked ? Cursor.visible : false;
                Cursor.lockState = _cursorWasLocked ? CursorLockMode.Locked : _prevLock;
            }
        }

        static float Smooth01(float x) => x * x * (3f - 2f * x);
        static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);

        public static void FreezeGameplay(bool freeze) => Time.timeScale = freeze ? 0f : 1f;
        
        // přidej metodu pro návrat z Load panelu bez refade
        public void RestoreAfterLoads()
        {
            // vrať overlay nad ostatní, zapni kartu a znovu blokuj raycasty pro GameOver UI
            BringToFront();

            if (cardCg) { cardCg.alpha = 1f; cardCg.blocksRaycasts = true; cardCg.interactable = true; }
            _rootCg.blocksRaycasts = true;
            _rootCg.interactable   = true;

            // titul nechávám podle tvé volby – typicky už skrytý
            // if (overTitle) overTitle.gameObject.SetActive(true); // pokud chceš ho zase ukázat

            SetupCursor(true);
            FocusFirst();
            _shown = true; // zůstáváme ve "zobrazeném" stavu
        }


        // Buttons
        public void OnClick_LoadLast()
        {
            Hide(restoreTimeScale: true, keepCursor: false, soft: false);
            var ctrl = FindObjectOfType<GameSaveController>();
            if (ctrl != null)
            {
                // zde zvolte slot, který chcete považovat za „poslední“
                ctrl.LoadSlot(1);
            }
        }

        // nahraď celou OnClick_LoadMenu
        public void OnClick_LoadMenu()
        {
            // nech GameOver overlay VIDLITELNÝ (černý), ale neblokuj kliky
            if (cardCg) { cardCg.alpha = 0f; cardCg.blocksRaycasts = false; cardCg.interactable = false; }
            _rootCg.blocksRaycasts = false;   // umožní klikat na Load panel
            _rootCg.interactable   = false;

            // volitelně schovej velký titul, ať nepřekrývá menu
            if (hideFadeTitleWhenMenuShows && overTitle) overTitle.gameObject.SetActive(false);

            // pošli overlay POD menu, aby bylo vidět (ale pozadí zůstává černé)
            SendBehindMenus();

            var mc = MenuController.I;
            if (mc) mc.ShowLoadsFromGameOver();
            else Debug.LogWarning("[GameOverUI] MenuController nenalezen – nelze otevřít Load panel.");

            GameManager.I?.EnableMenuCursor();
        }

        public void OnClick_ExitToMainMenu()
        {
            Hide(true);
            GameManager.I?.ReturnToMainMenu();
            MenuController.I?.ShowMainMenu();
        }

        // Console closer (stejné jako dřív)
        // Console closer (SAFE)
public static void TryCloseConsole()
{
    string asm = "Assembly-CSharp";
    string[] types = {
        "Obscurus.Console.DevConsole",
        "Obscurus.Console.ConsoleUI",
        "Obscurus.Console.DebugConsole",
        "Obscurus.Console.GameConsole",
        "Obscurus.Console.InGameConsole",
    };

    foreach (var tn in types)
    {
        var t = Type.GetType($"{tn}, {asm}");
        if (t == null) continue;

        // Prefer statické API konzole
        var mClose = t.GetMethod("Close", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
        if (mClose != null) { mClose.Invoke(null, null); return; }

        var mHide = t.GetMethod("Hide", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
        if (mHide != null) { mHide.Invoke(null, null); return; }

        var mSetOpen = t.GetMethod("SetOpen", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static, null, new[] { typeof(bool) }, null);
        if (mSetOpen != null) { mSetOpen.Invoke(null, new object[] { false }); return; }

        var mSetVis = t.GetMethod("SetVisible", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static, null, new[] { typeof(bool) }, null);
        if (mSetVis != null) { mSetVis.Invoke(null, new object[] { false }); return; }

        // Instance varianta
        object inst =
            t.GetProperty("Instance", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)?.GetValue(null) ??
            t.GetProperty("I",        BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)?.GetValue(null);

        if (inst != null)
        {
            var mi = t.GetMethod("Close", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
                   ?? t.GetMethod("Hide",  BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
            if (mi != null) { mi.Invoke(inst, null); return; }

            var pi =
                t.GetProperty("Open",    BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance) ??
                t.GetProperty("IsOpen",  BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance) ??
                t.GetProperty("Visible", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
            if (pi != null && pi.CanWrite && pi.PropertyType == typeof(bool))
            {
                pi.SetValue(inst, false);
                return;
            }
        }
    }

   
}
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
static void RepairConsoleIfWeBrokeIt()
{
    try
    {
        // Zapni případně vypnuté GO s "console" v názvu
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!go) continue;
            var n = go.name.ToLowerInvariant();
            if (n.Contains("console") && !go.activeSelf)
                go.SetActive(true);
        }

        // Vrať CanvasGroupy do použitelného stavu
        foreach (var cg in Resources.FindObjectsOfTypeAll<CanvasGroup>())
        {
            if (!cg) continue;
            var n = cg.gameObject.name.ToLowerInvariant();
            if (n.Contains("console"))
            {
                // nechávám alpha tak jak je; jen zajistím, že jde kliknout a psát, pokud to jejich UI čeká
                if (cg.alpha > 0f)
                {
                    cg.blocksRaycasts = true;
                    cg.interactable = true;
                }
            }
        }
    }
    catch (Exception e)
    {
        Debug.LogWarning($"[GameOverUI] Console repair skipped: {e.Message}");
    }
}


    }
}
