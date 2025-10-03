using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Obscurus.Save;

public class MenuController : MonoBehaviour
{
    public static MenuController I { get; private set; }

    [Header("Panels")]
    public CanvasGroup mainMenu;
    public CanvasGroup pauseMenu;
    public CanvasGroup options;
    public CanvasGroup saves;
    public CanvasGroup loads;

    [Header("Graphics UI")]
    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown fullscreenDropdown;
    public TMP_Dropdown resolutionDropdown;
    public Toggle vSyncToggle;
    public TMP_Dropdown msaaDropdown;
    public Slider renderScaleSlider;
    public Toggle hdrToggle;

    [Header("Audio UI")]
    public Slider masterVolume;
    public Slider musicVolume;
    public Slider sfxVolume;

    [Header("Controls UI")]
    public Slider mouseSensitivity;
    public Toggle invertYToggle;
    public Slider fovSlider;

    [Header("Save panel (Pause)")]
    public TextMeshProUGUI slot1Label, slot2Label, slot3Label;
    public RawImage slot1Thumb, slot2Thumb, slot3Thumb;

    [Header("Load panel (Main menu)")]
    public TextMeshProUGUI load1Label, load2Label, load3Label;
    public RawImage load1Thumb, load2Thumb, load3Thumb;

    [Header("Optional refs (auto-find)")]
    public Obscurus.UI.InventoryOverlayUI inventoryOverlay;

    bool paused;
    Resolution[] _resList;
    CanvasGroup _optionsBackTarget;
    bool _loadsBackToGameOver = false;
    private GameSaveController saveCtrl;

    public bool IsOverlayOpen =>
        (mainMenu && mainMenu.blocksRaycasts) ||
        (pauseMenu && pauseMenu.blocksRaycasts) ||
        (options && options.blocksRaycasts) ||
        (saves && saves.blocksRaycasts) ||
        (loads && loads.blocksRaycasts);

    public bool IsPaused => pauseMenu && pauseMenu.blocksRaycasts;

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;

        saveCtrl = FindObjectOfType<GameSaveController>();

        // auto-find inventáře
        if (!inventoryOverlay) inventoryOverlay = FindInventoryOverlay();

        // === Graphics UI bootstrap ===
        if (qualityDropdown)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            qualityDropdown.value = QualitySettings.GetQualityLevel();
        }
        if (fullscreenDropdown)
        {
            fullscreenDropdown.ClearOptions();
            fullscreenDropdown.AddOptions(new List<string>(new []{"Fullscreen","Exclusive Fullscreen","Maximized Window","Windowed"}));
            fullscreenDropdown.value = FullscreenToIndex(Screen.fullScreenMode);
        }
        _resList = Screen.resolutions
            .Where(r => r.refreshRateRatio.value >= 59)
            .OrderBy(r => r.width).ThenBy(r => r.height).ThenBy(r => r.refreshRateRatio.value).ToArray();
        if (resolutionDropdown)
        {
            resolutionDropdown.ClearOptions();
            var opts = _resList.Select(r => $"{r.width}×{r.height} @ {Mathf.RoundToInt((float)r.refreshRateRatio.value)}").ToList();
            resolutionDropdown.AddOptions(opts);
            var cur = Screen.currentResolution;
            int idx = Array.FindIndex(_resList, r => r.width == cur.width && r.height == cur.height);
            resolutionDropdown.value = Mathf.Max(0, idx);
        }

        var urp = GetURP();
        if (urp)
        {
            if (msaaDropdown)      msaaDropdown.value      = MsaaToIndex(urp.msaaSampleCount);
            if (renderScaleSlider) renderScaleSlider.value = Mathf.Clamp(urp.renderScale, 0.5f, 1.5f);
            if (hdrToggle)         hdrToggle.isOn          = urp.supportsHDR;
        }
        if (vSyncToggle) vSyncToggle.isOn = QualitySettings.vSyncCount > 0;

        // Audio
        if (masterVolume) { masterVolume.value = PlayerPrefs.GetFloat("vol_master", 1f); AudioListener.volume = masterVolume.value; }
        if (musicVolume)  musicVolume.value = PlayerPrefs.GetFloat("vol_music", 0.8f);
        if (sfxVolume)    sfxVolume.value   = PlayerPrefs.GetFloat("vol_sfx",   0.8f);

        // Controls
        if (mouseSensitivity) mouseSensitivity.value = PlayerPrefs.GetFloat("mouse_sens", 1f);
        if (invertYToggle)    invertYToggle.isOn     = PlayerPrefs.GetInt("invert_y", 0) == 1;
        if (fovSlider)        fovSlider.value        = PlayerPrefs.GetFloat("fov", 90f);

        // UI default
        ShowOnly(mainMenu);
        Set(options, false); Set(saves, false); Set(loads, false);

        RefreshSavesPanel();
        RefreshLoadsPanel();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        bool inAnyLevel = IsAnyNonBootstrapSceneLoaded();

        if (kb.escapeKey.wasPressedThisFrame && inAnyLevel)
        {
            // ESC zavírá Options/Saves
            if (options && options.alpha > 0) { Btn_CloseOptions(); return; }
            if (saves   && saves.alpha   > 0) { Btn_CloseSaves();   return; }

            // zavři inventář → pauza
            if (inventoryOverlay != null && inventoryOverlay.IsOpen)
            {
                inventoryOverlay.Close();
                ShowOnly(pauseMenu);
                Obscurus.GameOverUI.TryCloseConsole();
                return;
            }

            TogglePause();
        }
    }

    static bool IsAnyNonBootstrapSceneLoaded()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != "_Bootstrap") return true;
        }
        return false;
    }

    void ShowOnly(CanvasGroup g)
    {
        Set(mainMenu, g == mainMenu);
        Set(pauseMenu, g == pauseMenu);
        Set(options,  g == options);
        Set(saves,    g == saves);
        Set(loads,    g == loads);

        var gm = GameManager.I;

        if (g == pauseMenu)
        {
            Time.timeScale = 0f;
            Obscurus.GameOverUI.TryCloseConsole();
            if (gm != null) gm.EnableMenuCursor(); else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            return;
        }

        if (g == mainMenu || g == options || g == loads)
        {
            if (gm != null) gm.EnableMenuCursor(); else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        if (g == null || g == mainMenu) Time.timeScale = 1f;
    }

    static void Set(CanvasGroup g, bool v)
    {
        if (!g) return;
        g.alpha = v ? 1 : 0;
        g.blocksRaycasts = v;
        g.interactable = v;
    }

    void CloseAllMenusForGameplay()
    {
        ShowOnly(null);
        Set(mainMenu,  false);
        Set(loads,     false);
        Set(pauseMenu, false);
        Set(options,   false);
        Set(saves,     false);
        GameManager.I?.EnableGameplayCursor();
    }

    public void TogglePause()
    {
        var gm = GameManager.I;
        var opening = !(pauseMenu && pauseMenu.alpha > 0f);
        if (opening)
        {
            ShowOnly(pauseMenu);
            Obscurus.GameOverUI.TryCloseConsole();
        }
        else
        {
            Time.timeScale = 1f; Set(pauseMenu,false); gm?.EnableGameplayCursor();
        }
    }

    // ===== Main menu buttons =====
    // ===== Main menu buttons =====
    public void Btn_Play()
    {
        GameSaveController.IsNewGame = true;
        ShowOnly(null);
        Set(mainMenu, false);
        GameManager.I?.EnableGameplayCursor();
        GameManager.I?.LoadDefault();  // ← volá nyní existující veřejnou metodu
    }

    public void Btn_PlayDev()
    {
        GameSaveController.IsNewGame = true;
        ShowOnly(null);
        Set(mainMenu, false);
        GameManager.I?.EnableGameplayCursor();
        GameManager.I?.LoadDev();      // ← volá nyní existující veřejnou metodu
    }

    public void Btn_PlayExample()
    {
        GameSaveController.IsNewGame = true;
        ShowOnly(null);
        Set(mainMenu, false);
        GameManager.I?.EnableGameplayCursor();
        GameManager.I?.LoadExample();  // ← volá nyní existující veřejnou metodu
    }



    public void Btn_OpenOptions()
    {
        _optionsBackTarget =
            (pauseMenu && pauseMenu.blocksRaycasts) ? pauseMenu :
            (mainMenu  && mainMenu.blocksRaycasts)  ? mainMenu  :
            (loads     && loads.blocksRaycasts)     ? loads     :
            (saves     && saves.blocksRaycasts)     ? saves     :
            null;

        if (_optionsBackTarget == null)
            _optionsBackTarget = IsAnyNonBootstrapSceneLoaded() ? pauseMenu : mainMenu;

        RefreshOptionsPreview();
        ShowOnly(options);
    }

    public void Btn_CloseOptions()
    {
        var target = _optionsBackTarget ? _optionsBackTarget : mainMenu;
        ShowOnly(target);
        _optionsBackTarget = null;
    }

    public void Btn_OpenLoads() { _loadsBackToGameOver = false; RefreshLoadsPanel(); ShowOnly(loads); }
    public void Btn_CloseLoads()
    {
        if (_loadsBackToGameOver)
        {
            _loadsBackToGameOver = false;
            var go = FindFirstObjectByType<Obscurus.GameOverUI>(FindObjectsInactive.Include);
            if (go) { go.RestoreAfterLoads(); return; }
            ShowOnly(mainMenu);
            return;
        }
        ShowOnly(mainMenu);
    }

    public void Btn_Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ===== Pause buttons =====
    public void Btn_Resume() => TogglePause();
    public void Btn_RestartLevel()
    {
        if (GameManager.I == null || !IsAnyNonBootstrapSceneLoaded()) return;
        var lvl = GameManager.I.CurrentLevel;
        TogglePause();
        if (!string.IsNullOrEmpty(lvl)) GameManager.I.LoadLevel(lvl);
    }
    public void Btn_MainMenu()
    {
        TogglePause();
        ShowOnly(mainMenu);
        GameManager.I?.ReturnToMainMenu();
    }
    public void Btn_OpenSaves() { RefreshSavesPanel(); ShowOnly(saves); }
    public void Btn_CloseSaves(){ ShowOnly(pauseMenu); }

    // ===== Graphics, Audio, Controls handlers =====
    public void OnQualityChanged(int idx) { QualitySettings.SetQualityLevel(idx, true); PlayerPrefs.SetInt("opt_quality", idx); }
    public void OnFullscreenChanged(int idx) { var mode = IndexToFullscreen(idx); Screen.fullScreenMode = mode; PlayerPrefs.SetInt("opt_fullscreen", idx); }
    public void OnResolutionChanged(int idx)
    {
        if (_resList == null || _resList.Length == 0) return;
        idx = Mathf.Clamp(idx, 0, _resList.Length-1);
        var r = _resList[idx];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode, r.refreshRateRatio);
        PlayerPrefs.SetInt("opt_res_idx", idx);
    }
    public void OnVSyncChanged(bool on) { QualitySettings.vSyncCount = on ? 1 : 0; PlayerPrefs.SetInt("opt_vsync", on ? 1 : 0); }
    public void OnMSAAChanged(int idx) { var urp = GetURP(); if (!urp) return; urp.msaaSampleCount = IndexToMsaa(idx); PlayerPrefs.SetInt("opt_msaa", urp.msaaSampleCount); }
    public void OnRenderScaleChanged(float v) { var urp = GetURP(); if (!urp) return; urp.renderScale = Mathf.Clamp(v, 0.5f, 1.5f); PlayerPrefs.SetFloat("opt_renderscale", urp.renderScale); }
    public void OnHDRChanged(bool on) { var urp = GetURP(); if (!urp) return; urp.supportsHDR = on; PlayerPrefs.SetInt("opt_hdr", on ? 1 : 0); }

    public void OnMasterVolume(float v) { AudioListener.volume = Mathf.Clamp01(v); PlayerPrefs.SetFloat("vol_master", AudioListener.volume); }
    public void OnMusicVolume (float v) { PlayerPrefs.SetFloat("vol_music", Mathf.Clamp01(v)); }
    public void OnSfxVolume   (float v) { PlayerPrefs.SetFloat("vol_sfx",   Mathf.Clamp01(v)); }

    public void OnSensitivityChanged(float v) { PlayerPrefs.SetFloat("mouse_sens", v); }
    public void OnInvertYChanged(bool on)     { PlayerPrefs.SetInt("invert_y", on ? 1 : 0); }
    public void OnFovChanged(float v)         { PlayerPrefs.SetFloat("fov", v); }

    // ===== Slot‑based save/load pomocí GameSaveController =====
    public void Btn_SaveSlot1()
    {
        if (saveCtrl == null) saveCtrl = FindObjectOfType<GameSaveController>();
        saveCtrl.SaveSlot(1);
        RefreshSavesPanel();
    }
    public void Btn_SaveSlot2()
    {
        if (saveCtrl == null) saveCtrl = FindObjectOfType<GameSaveController>();
        saveCtrl.SaveSlot(2);
        RefreshSavesPanel();
    }
    public void Btn_SaveSlot3()
    {
        if (saveCtrl == null) saveCtrl = FindObjectOfType<GameSaveController>();
        saveCtrl.SaveSlot(3);
        RefreshSavesPanel();
    }

    public void Btn_LoadSlot1()
    {
        if (saveCtrl == null) saveCtrl = FindObjectOfType<GameSaveController>();
        GameSaveController.IsNewGame = false;
        CloseAllMenusForGameplay();
        saveCtrl.LoadSlot(1);
    }
    public void Btn_LoadSlot2()
    {
        if (saveCtrl == null) saveCtrl = FindObjectOfType<GameSaveController>();
        GameSaveController.IsNewGame = false;
        CloseAllMenusForGameplay();
        saveCtrl.LoadSlot(2);
    }
    public void Btn_LoadSlot3()
    {
        if (saveCtrl == null) saveCtrl = FindObjectOfType<GameSaveController>();
        GameSaveController.IsNewGame = false;
        CloseAllMenusForGameplay();
        saveCtrl.LoadSlot(3);
    }

    public void Btn_DeleteSlot1()
    {
        if (saveCtrl == null) saveCtrl = FindObjectOfType<GameSaveController>();
        saveCtrl.DeleteSlot(1);
        RefreshSavesPanel();
        RefreshLoadsPanel();
    }
    public void Btn_DeleteSlot2()
    {
        if (saveCtrl == null) saveCtrl = FindObjectOfType<GameSaveController>();
        saveCtrl.DeleteSlot(2);
        RefreshSavesPanel();
        RefreshLoadsPanel();
    }
    public void Btn_DeleteSlot3()
    {
        if (saveCtrl == null) saveCtrl = FindObjectOfType<GameSaveController>();
        saveCtrl.DeleteSlot(3);
        RefreshSavesPanel();
        RefreshLoadsPanel();
    }

    void RefreshSavesPanel()
    {
        if (saveCtrl == null) saveCtrl = FindObjectOfType<GameSaveController>();
        if (slot1Label) slot1Label.text = saveCtrl.SlotSummary(1);
        if (slot2Label) slot2Label.text = saveCtrl.SlotSummary(2);
        if (slot3Label) slot3Label.text = saveCtrl.SlotSummary(3);
        if (slot1Thumb) ApplyThumb(slot1Thumb, null);
        if (slot2Thumb) ApplyThumb(slot2Thumb, null);
        if (slot3Thumb) ApplyThumb(slot3Thumb, null);
    }

    void RefreshLoadsPanel()
    {
        if (saveCtrl == null) saveCtrl = FindObjectOfType<GameSaveController>();
        if (load1Label) load1Label.text = saveCtrl.SlotSummary(1);
        if (load2Label) load2Label.text = saveCtrl.SlotSummary(2);
        if (load3Label) load3Label.text = saveCtrl.SlotSummary(3);
        if (load1Thumb) ApplyThumb(load1Thumb, null);
        if (load2Thumb) ApplyThumb(load2Thumb, null);
        if (load3Thumb) ApplyThumb(load3Thumb, null);
    }

    static void ApplyThumb(RawImage img, Texture2D tex)
    {
        if (!img) return;
        img.texture = tex;
        img.color = tex ? Color.white : new Color(1,1,1,0.15f);
    }

    static int MsaaToIndex(int samples) => samples switch { 2=>1, 4=>2, 8=>3, _=>0 };
    static int IndexToMsaa(int idx) => idx switch { 1=>2, 2=>4, 3=>8, _=>1 };
    static int FullscreenToIndex(FullScreenMode m) => m switch {
        FullScreenMode.FullScreenWindow    => 0,
        FullScreenMode.ExclusiveFullScreen => 1,
        FullScreenMode.MaximizedWindow     => 2,
        _ => 3
    };
    static FullScreenMode IndexToFullscreen(int i) => i switch {
        0 => FullScreenMode.FullScreenWindow,
        1 => FullScreenMode.ExclusiveFullScreen,
        2 => FullScreenMode.MaximizedWindow,
        _ => FullScreenMode.Windowed
    };
    static UniversalRenderPipelineAsset GetURP()
    {
        var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (!rp) rp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        return rp;
    }

    void RefreshOptionsPreview() { }

    public void ShowMainMenu()          { _loadsBackToGameOver = false; ShowOnly(mainMenu); }
    public void ShowLoadsMenu()         { _loadsBackToGameOver = false; RefreshLoadsPanel(); ShowOnly(loads); }
    public void ShowLoadsFromGameOver() { _loadsBackToGameOver = true;  RefreshLoadsPanel(); ShowOnly(loads); GameManager.I?.EnableMenuCursor(); }

    static Obscurus.UI.InventoryOverlayUI FindInventoryOverlay()
    {
#if UNITY_2022_2_OR_NEWER
        var x = FindFirstObjectByType<Obscurus.UI.InventoryOverlayUI>(FindObjectsInactive.Include);
        if (x) return x;
#endif
        var all = Resources.FindObjectsOfTypeAll<Obscurus.UI.InventoryOverlayUI>();
        foreach (var c in all)
        {
            if (!c) continue;
            var go = c.gameObject;
            if (go && go.scene.IsValid() && (go.hideFlags & HideFlags.HideInHierarchy) == 0) return c;
        }
        return null;
    }
}
