using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

public class DebugOverlay : MonoBehaviour
{
    public enum Detail { Basic, Full }

    [Header("UI")]
    [SerializeField] TextMeshProUGUI text;
    [SerializeField] CanvasGroup canvasGroup;          // <- nově
    [SerializeField] Detail detail = Detail.Full;
    [SerializeField] float updateInterval = 0.2f;

    [Header("Vizibilita")]
    [SerializeField] bool startVisible = true;
    bool visible;                                      // stav panelu (nezávislý na activeSelf)

    // Recordery
    ProfilerRecorder recDrawCalls, recBatches, recSetPass, recTris, recVerts;
    ProfilerRecorder recCpuMain, recRenderThread, recGpu;

    // FPS okno + min/max
    readonly Queue<float> _fpsWindow = new Queue<float>(240);
    const int FpsWindowSize = 240;
    float fpsMinSeen = float.PositiveInfinity, fpsMaxSeen = 0f;

    float _t;

    void Awake()
    {
        if (!canvasGroup) { canvasGroup = GetComponent<CanvasGroup>(); if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>(); }
        if (text == null) Debug.LogWarning("[DebugOverlay] Chybí reference na TextMeshProUGUI.");

        recDrawCalls   = StartRec(ProfilerCategory.Render,  "Draw Calls Count");
        recBatches     = StartRec(ProfilerCategory.Render,  "Batches Count");
        recSetPass     = StartRec(ProfilerCategory.Render,  "SetPass Calls Count");
        recTris        = StartRec(ProfilerCategory.Render,  "Triangles Count");
        recVerts       = StartRec(ProfilerCategory.Render,  "Vertices Count");
        recCpuMain     = StartRec(ProfilerCategory.Internal,"Main Thread");
        recRenderThread= StartRec(ProfilerCategory.Internal,"Render Thread");
        if (!recRenderThread.Valid) recRenderThread = StartRec(ProfilerCategory.Render, "Render Thread");
        recGpu         = StartRec(ProfilerCategory.Render,  "GPU Time");

        visible = startVisible;
        ApplyVisible();
    }

    void OnEnable()
    {
        _fpsWindow.Clear();
        fpsMinSeen = float.PositiveInfinity;
        fpsMaxSeen = 0f;
    }

    void OnDestroy()
    {
        Dispose(ref recDrawCalls); Dispose(ref recBatches); Dispose(ref recSetPass);
        Dispose(ref recTris); Dispose(ref recVerts); Dispose(ref recCpuMain);
        Dispose(ref recRenderThread); Dispose(ref recGpu);
    }

    void Update()
    {
        // ovládání (nový Input System)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.f1Key.wasPressedThisFrame) { visible = !visible; ApplyVisible(); }
            if (Keyboard.current.f2Key.wasPressedThisFrame) ResetStats();
            if (Keyboard.current.f3Key.wasPressedThisFrame) detail = detail == Detail.Full ? Detail.Basic : Detail.Full;
        }

        // běžíme i skrytí (ať se min/max stále aktualizují)
        float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.00001f);
        if (_fpsWindow.Count >= FpsWindowSize) _fpsWindow.Dequeue();
        _fpsWindow.Enqueue(fps);
        if (fps < fpsMinSeen) fpsMinSeen = fps;
        if (fps > fpsMaxSeen) fpsMaxSeen = fps;

        _t += Time.unscaledDeltaTime;
        if (_t < updateInterval || !visible || text == null) return;
        _t = 0f;

        // výpočty
        float ms = Time.unscaledDeltaTime * 1000f;
        float fpsAvg = 0f; foreach (var f in _fpsWindow) fpsAvg += f; fpsAvg /= _fpsWindow.Count;
        long drawCalls = Read(recDrawCalls), batches = Read(recBatches), setPass = Read(recSetPass), tris = Read(recTris), verts = Read(recVerts);
        float cpuMs = ReadMs(recCpuMain), rtMs = ReadMs(recRenderThread), gpuMs = ReadMs(recGpu);
        double mb = 1.0 / (1024.0 * 1024.0);
        double allocMB = Profiler.GetTotalAllocatedMemoryLong() * mb, reservedMB = Profiler.GetTotalReservedMemoryLong() * mb, managedMB = Profiler.GetMonoUsedSizeLong() * mb;

        if (detail == Detail.Basic)
        {
            text.text =
                $"<b>FPS</b> {fps:0.0}  (<i>{ms:0.0} ms</i>)  avg {fpsAvg:0.0}  min {fpsMinSeen:0.0}  max {fpsMaxSeen:0.0}\n" +
                $"DrawCalls {drawCalls}  Batches {batches}  Tris {(tris/1_000_000.0):0.00}M";
        }
        else
        {
            text.text =
                $"<b>FPS</b> {fps:0.0}  (<i>{ms:0.0} ms</i>)  avg {fpsAvg:0.0}  min {fpsMinSeen:0.0}  max {fpsMaxSeen:0.0}\n" +
                $"CPU {cpuMs:0.0} ms   GPU {(gpuMs>0?gpuMs:0):0.0} ms   RT {(rtMs>0?rtMs:0):0.0} ms\n" +
                $"Draw {drawCalls}   Batches {batches}   SetPass {setPass}\n" +
                $"Tris {(tris/1_000_000.0):0.00}M   Verts {(verts/1_000_000.0):0.00}M\n" +
                $"Mem  Alloc {allocMB:0.0} MB   Reserved {reservedMB:0.0} MB   Managed {managedMB:0.0} MB\n" +
                $"F1: Show/Hide  |  F2: Reset min/max  |  F3: Detail";
        }
    }

    // ===== helpers =====
    void ApplyVisible()
    {
        if (!canvasGroup) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    void ResetStats()
    {
        _fpsWindow.Clear();
        fpsMinSeen = float.PositiveInfinity;
        fpsMaxSeen = 0f;
    }

    static ProfilerRecorder StartRec(ProfilerCategory cat, string name, int cap = 15)
    {
        try { return ProfilerRecorder.StartNew(cat, name, cap); }
        catch { return default; } // když stat není na platformě k dispozici
    }

    static long Read(ProfilerRecorder r) => (!r.Valid || r.Count == 0) ? 0 : r.LastValue;
    static float ReadMs(ProfilerRecorder r) => (!r.Valid || r.Count == 0) ? 0f : r.LastValue * 1e-6f;
    static void Dispose(ref ProfilerRecorder r) { if (r.Valid) r.Dispose(); r = default; }
}
