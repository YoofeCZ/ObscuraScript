using System;
using System.Collections.Generic;
using UnityEngine;

namespace Obscurus.Save
{
    public interface ISaveable
    {
        string Id { get; }                 // SaveId (GUID)
        string SaveType { get; }           // typ komponenty (pro párování při loadu)
        string PrefabKey { get; }          // klíč v Prefab DB (pro respawn, může být prázdný)

        string CaptureAsJson();            // serializace vlastního stavu
        void ApplyFromJson(string json);   // deserializace vlastního stavu
    }

    /// <summary>Základ pro konkrétní save komponenty (zde SaveAgent).</summary>
    public abstract class SaveComponent : MonoBehaviour, ISaveable
    {
        [SerializeField] protected SaveId saveId;
        [SerializeField] protected string prefabKey; // volitelné

        protected virtual void Awake() { if (!saveId) saveId = GetComponent<SaveId>(); }

        public string Id => saveId ? saveId.Id : null;
        public virtual string SaveType => GetType().Name;
        public virtual string PrefabKey => prefabKey;

        public abstract string CaptureAsJson();
        public abstract void ApplyFromJson(string json);

        /// <summary>Jen pro SaveManager: doplní ID do nově spawnutého objektu.</summary>
        public void _ForceSetId(string v)
        {
            if (!saveId) saveId = gameObject.AddComponent<SaveId>();
            saveId.SetIdRuntime(v);
        }

        public void _ForceSetPrefabKey(string v) => prefabKey = v;
    }

    // ---------- Datové typy save souboru ----------

    [Serializable]
    public class SceneSet
    {
        public string activeScene;
        public List<string> loadedScenes = new();
    }

    [Serializable]
    public class SaveFile
    {
        public int version = 1;
        public string dateIso;
        public SceneSet scenes = new SceneSet();
        public List<SaveObject> objects = new List<SaveObject>();
    }

    [Serializable]
    public class SaveObject
    {
        public string id;          // SaveId
        public string type;        // SaveType (např. "SaveAgent")
        public string prefabKey;   // PrefabDB klíč
        public string json;        // interní JSON snapshot
    }
}
