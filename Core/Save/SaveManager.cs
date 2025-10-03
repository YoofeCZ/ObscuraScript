#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

namespace Obscurus.Save
{
#if HAS_ODIN
    [HideMonoScript]
    [InfoBox("Centrální Save/Load – ukládá všechny aktuálně nahrané scény a všechny objekty se SaveAgentem/SaveComponentem.", InfoMessageType.Info)]
#endif
    [DefaultExecutionOrder(-9000)]
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager I { get; private set; }

#if HAS_ODIN
        [BoxGroup("Database"), InlineEditor(InlineEditorObjectFieldModes.Foldout)]
        [Tooltip("DB pro respawn chybějících objektů při loadu (podle PrefabKey).")]
#endif
        [SerializeField] SavePrefabDB prefabDB;

#if HAS_ODIN
        [BoxGroup("Options")]
#endif
        [SerializeField] bool compressSaves = false;
        
        // === Flag pro rozlišení nové hry vs. načtený save ===
        public static bool IsNewGame { get; set; } = false;
        public static event Action OnBeforeSave;
        public static event Action OnAfterSave;
        public static event Action OnBeforeLoad;
        public static event Action OnAfterLoad;

        // aktuální mapa Id → GameObject (pro resolve referencí uvnitř Apply)
        static Dictionary<string, GameObject> _currentIdMap;

        void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
        }

        // ---------- PUBLIC API ----------
#if HAS_ODIN
        [BoxGroup("Quick")]
        [HorizontalGroup("Quick/row")]
        [Button(ButtonSizes.Medium), GUIColor(0.2f,0.8f,0.2f)] public void Save1() => SaveSlot(1);
        [HorizontalGroup("Quick/row")]
        [Button(ButtonSizes.Medium), GUIColor(0.2f,0.8f,0.2f)] public void Save2() => SaveSlot(2);
        [HorizontalGroup("Quick/row")]
        [Button(ButtonSizes.Medium), GUIColor(0.2f,0.8f,0.2f)] public void Save3() => SaveSlot(3);

        [HorizontalGroup("Quick/row2")]
        [Button(ButtonSizes.Medium), GUIColor(0.2f,0.6f,1f)] public void Load1() => LoadSlot(1);
        [HorizontalGroup("Quick/row2")]
        [Button(ButtonSizes.Medium), GUIColor(0.2f,0.6f,1f)] public void Load2() => LoadSlot(2);
        [HorizontalGroup("Quick/row2")]
        [Button(ButtonSizes.Medium), GUIColor(0.2f,0.6f,1f)] public void Load3() => LoadSlot(3);
#endif

        public static void SaveSlot(int slot)
        {
            if (!I) AutoCreateInstance();

            OnBeforeSave?.Invoke();

            var file = new SaveFile
            {
                dateIso = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            // Scény
            file.scenes = CaptureSceneSet();

            // Objekty
            var saveables = UnityEngine.Object.FindObjectsByType<SaveComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var s in saveables)
            {
                var rec = new SaveObject
                {
                    id        = s.Id,
                    type      = s.SaveType,
                    prefabKey = s.PrefabKey,
                    json      = s.CaptureAsJson()
                };
                if (!string.IsNullOrEmpty(rec.id))
                    file.objects.Add(rec);
            }

            var dir = SlotDir(slot);
            Directory.CreateDirectory(dir);

            var json = SaveCodec.ToJson(file, pretty: true);
            var path = Path.Combine(dir, "save.json");

            WriteAtomic(path, json, I.compressSaves);
            CaptureScreenshot(Path.Combine(dir, "thumb.png"), 640, 360);

            Debug.Log($"[SaveManager] Saved slot {slot} → {dir}");
            OnAfterSave?.Invoke();
        }

        public static void LoadSlot(int slot)
        {
            if (!I) AutoCreateInstance();

            var dir = SlotDir(slot);
            var path = Path.Combine(dir, "save.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SaveManager] Slot {slot} empty.");
                return;
            }

            var json = ReadMaybeCompressed(path);
            var file = SaveCodec.FromJson<SaveFile>(json);
            if (file == null || file.scenes == null || file.scenes.loadedScenes == null || file.scenes.loadedScenes.Count == 0)
            {
                Debug.LogWarning($"[SaveManager] Slot {slot} corrupted.");
                return;
            }

            I.StartCoroutine(I.LoadRoutine(file));
        }

        public static void DeleteSlot(int slot)
        {
            var dir = SlotDir(slot);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Debug.Log($"[SaveManager] Deleted slot {slot}");
        }

        public static string SlotSummary(int slot)
        {
            try
            {
                var path = Path.Combine(SlotDir(slot), "save.json");
                if (!File.Exists(path)) return "Empty";
                var json = ReadMaybeCompressed(path);
                var f = SaveCodec.FromJson<SaveFile>(json);
                var first = f?.scenes?.loadedScenes?.FirstOrDefault() ?? "?";
                return f == null ? "Corrupted" : $"{first} — {f.dateIso}";
            }
            catch { return "Corrupted"; }
        }

        public static Texture2D LoadThumbnail(int slot)
        {
            var p = Path.Combine(SlotDir(slot), "thumb.png");
            if (!File.Exists(p)) return null;
            try
            {
                var bytes = File.ReadAllBytes(p);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(bytes);
                return tex;
            }
            catch { return null; }
        }

        // ====== NOVĚ: „Load Last Save“ a utilitky ======

        public static void LoadLastSave()
        {
            int slot = FindLastSavedSlot();
            if (slot <= 0)
            {
                Debug.LogWarning("[SaveManager] LoadLastSave: žádný uložený slot nenalezen.");
                return;
            }
            Debug.Log($"[SaveManager] LoadLastSave → slot {slot} ({SlotSummary(slot)})");
            LoadSlot(slot);
        }

        public static int FindLastSavedSlot()
        {
            DateTime best = DateTime.MinValue;
            int bestSlot = -1;

            foreach (var s in GetExistingSlots())
            {
                if (TryGetSlotDate(s, out var when) && when > best)
                {
                    best = when;
                    bestSlot = s;
                }
            }
            return bestSlot;
        }

        public static int[] GetExistingSlots()
        {
            try
            {
                var baseDir = Path.Combine(Application.persistentDataPath, "Obscurus", "Saves");
                if (!Directory.Exists(baseDir)) return Array.Empty<int>();

                return Directory.GetDirectories(baseDir, "slot*")
                    .Select(d =>
                    {
                        var name = Path.GetFileName(d);
                        return (name.StartsWith("slot", StringComparison.OrdinalIgnoreCase) &&
                                int.TryParse(name.Substring(4), out var n)) ? n : -1;
                    })
                    .Where(n => n > 0)
                    .OrderBy(n => n)
                    .ToArray();
            }
            catch { return Array.Empty<int>(); }
        }

        public static bool TryGetSlotDate(int slot, out DateTime when)
        {
            when = DateTime.MinValue;
            try
            {
                var path = Path.Combine(SlotDir(slot), "save.json");
                if (!File.Exists(path)) return false;

                var json = ReadMaybeCompressed(path);
                var f = SaveCodec.FromJson<SaveFile>(json);
                if (f != null && !string.IsNullOrWhiteSpace(f.dateIso) &&
                    DateTime.TryParseExact(f.dateIso, "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
                {
                    when = parsed;
                    return true;
                }

                var wt = File.GetLastWriteTime(path);
                if (wt > DateTime.MinValue) { when = wt; return true; }
            }
            catch { /* ignore */ }
            return false;
        }

        // ---------- Interní ----------

        static string SlotDir(int slot) =>
            Path.Combine(Application.persistentDataPath, "Obscurus", "Saves", $"slot{slot}");

        static void AutoCreateInstance()
        {
            var go = new GameObject("~SaveManager");
            var m  = go.AddComponent<SaveManager>();
            m.hideFlags = HideFlags.DontSaveInEditor;
            Debug.Log("[SaveManager] Auto-created instance.");
        }

        static SaveFile CurrentFile;

        System.Collections.IEnumerator LoadRoutine(SaveFile file)
        {
            OnBeforeLoad?.Invoke();

            // 1) Načti cílové scény (single + additive)
            yield return LoadSceneSet(file.scenes);
            // Nech proběhnout Awake/OnEnable/OnSceneLoaded (+ případné spawny)
            yield return null;
            yield return null;

            // 2) Build Id mapa
            _currentIdMap = BuildIdMap();

            // 3) Respawn chybějících
            foreach (var o in file.objects)
            {
                if (string.IsNullOrEmpty(o.id)) continue;
                if (_currentIdMap.ContainsKey(o.id)) continue;

                if (!string.IsNullOrEmpty(o.prefabKey) && prefabDB)
                {
                    var prefab = prefabDB.Find(o.prefabKey);
                    if (prefab)
                    {
                        var spawned = UnityEngine.Object.Instantiate(prefab);
                        var sav = spawned.GetComponent<SaveComponent>();
                        var sid = spawned.GetComponent<SaveId>();

                        if (sav) sav._ForceSetId(o.id);
                        else if (sid) sid.SetIdRuntime(o.id);
                        else { sid = spawned.AddComponent<SaveId>(); sid.SetIdRuntime(o.id); }

                        _currentIdMap[o.id] = spawned;
                    }
                    else Debug.LogWarning($"[SaveManager] PrefabDB key not found: {o.prefabKey}");
                }
            }

            // 4) Aplikace stavů
            foreach (var o in file.objects)
            {
                if (!_currentIdMap.TryGetValue(o.id, out var go) || go == null) continue;

                var candidates = go.GetComponents<SaveComponent>();
                if (candidates == null || candidates.Length == 0)
                {
                    Debug.LogWarning($"[SaveManager] {o.id} has no SaveComponent.");
                    continue;
                }

                var s = candidates.FirstOrDefault(c => c.SaveType == o.type) ?? candidates.FirstOrDefault();
                if (s == null)
                {
                    Debug.LogWarning($"[SaveManager] {o.id} no component matches SaveType '{o.type}'.");
                    continue;
                }

                s.ApplyFromJson(o.json);
            }

            OnAfterLoad?.Invoke();
        }

        static SceneSet CaptureSceneSet()
        {
            var ss = new SceneSet();
            var active = SceneManager.GetActiveScene().name;
            ss.activeScene = active;

            // vezmi všechny nahrané scény kromě _Bootstrap
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name == "_Bootstrap") continue;
                if (s.isLoaded) ss.loadedScenes.Add(s.name);
            }

            // fallback
            if (ss.loadedScenes.Count == 0 && active != "_Bootstrap")
                ss.loadedScenes.Add(active);

            return ss;
        }

        System.Collections.IEnumerator LoadSceneSet(SceneSet ss)
        {
            // 1) Odpojit staré scény kromě _Bootstrap
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name != "_Bootstrap")
                {
                    var u = SceneManager.UnloadSceneAsync(s);
                    if (u != null) while (!u.isDone) yield return null;
                }
            }

            // 2) Načíst nové scény additivně
            foreach (var name in ss.loadedScenes)
            {
                var op = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
                while (!op.isDone) yield return null;
            }

            // 3) Nastavit active scene
            if (!string.IsNullOrEmpty(ss.activeScene))
            {
                var a = SceneManager.GetSceneByName(ss.activeScene);
                if (a.IsValid()) SceneManager.SetActiveScene(a);
            }
        }

        static Dictionary<string, GameObject> BuildIdMap()
        {
            var dict = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            var ids = UnityEngine.Object.FindObjectsByType<SaveId>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var sid in ids)
            {
                var key = sid.Id;
                if (string.IsNullOrEmpty(key)) continue;
                if (!dict.ContainsKey(key)) dict.Add(key, sid.gameObject);
            }
            return dict;
        }

        // ---------- Resolve pro SaveAgent ----------
        public static bool TryResolveSceneObject(string saveId, Type componentType, out object obj)
        {
            obj = null;
            if (string.IsNullOrEmpty(saveId)) return false;
            if (_currentIdMap == null || !_currentIdMap.TryGetValue(saveId, out var go) || go == null)
                return false;

            if (componentType == null || componentType == typeof(GameObject))
            {
                obj = go;
                return true;
            }

            var c = go.GetComponent(componentType);
            if (c != null) { obj = c; return true; }
            return false;
        }

        // ---------- IO helpers ----------
        static void WriteAtomic(string path, string json, bool gzip)
        {
            var tmp = path + ".tmp";
            if (gzip)
            {
                var gzPath = path + ".gz";
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var sw = new StreamWriter(fs)) { sw.Write(json); }
                GZipCompress(tmp, gzPath);
                if (File.Exists(path)) File.Delete(path);
                File.Delete(tmp);
                File.Move(gzPath, path);
            }
            else
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    fs.Write(bytes, 0, bytes.Length);

                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
        }

        static string ReadMaybeCompressed(string path)
        {
            try
            {
                var txt = File.ReadAllText(path);
                return txt;
            }
            catch { return ""; }
        }

        static void GZipCompress(string src, string dst)
        {
            using (var input = new FileStream(src, FileMode.Open, FileAccess.Read))
            using (var output = new FileStream(dst, FileMode.Create, FileAccess.Write))
            using (var gz = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Optimal))
                input.CopyTo(gz);
        }

        static void CaptureScreenshot(string path, int w, int h)
        {
            var cam = Camera.main;
            if (!cam) { Debug.LogWarning("[SaveManager] No Camera.main for screenshot."); return; }

            var rt  = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            var prev = cam.targetTexture;
            var old  = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = prev;
            RenderTexture.active = old;
            UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(tex);
        }
    }
}
