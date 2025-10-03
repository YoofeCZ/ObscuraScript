#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Obscurus.Save
{
    public static class SaveToolsEditor
    {
        [MenuItem("Obscurus/Save/Tag Selection As Saveable (incl. children) %#t")]
        public static void TagSelection()
        {
            foreach (var go in Selection.gameObjects)
                TagRecursive(go.transform);
            Debug.Log("[Save] SaveTag + SaveId + SaveAgent přidáno (selection).");
        }

        [MenuItem("Obscurus/Save/Tag Active Scene (all objects) %#y")]
        public static void TagActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                TagRecursive(root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Save] Active scene tagged: {scene.name}");
        }

        static void TagRecursive(Transform t)
        {
            Ensure(t.gameObject);
            foreach (Transform c in t) TagRecursive(c);
        }

        static void Ensure(GameObject go)
        {
            var tag = go.GetComponent<SaveTag>() ?? go.AddComponent<SaveTag>();
            var sid = go.GetComponent<SaveId>() ?? go.AddComponent<SaveId>();
            sid.EnsureId();
            var agent = go.GetComponent<SaveAgent>() ?? go.AddComponent<SaveAgent>();
            EditorUtility.SetDirty(go);
        }
    }
}
#endif