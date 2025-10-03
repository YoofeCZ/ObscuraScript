#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Obscurus.Items.Editor
{
    public static class ItemAssetUtility
    {
        /// <summary> Vytvoří novou definici itemu jako asset a přidá do DB. </summary>
        public static ItemDefinition CreateItemAsset(ItemDatabase db, string name, ItemType type, string targetFolder = null)
        {
            if (db == null) { Debug.LogError("[Items] ItemDatabase is null."); return null; }

            string dbPath = AssetDatabase.GetAssetPath(db);
            string dbDir  = Path.GetDirectoryName(dbPath);
            string folder = string.IsNullOrEmpty(targetFolder) ? dbDir : targetFolder;

            if (!AssetDatabase.IsValidFolder(folder))
            {
                var parent = Path.GetDirectoryName(folder);
                var leaf   = Path.GetFileName(folder);
                if (!AssetDatabase.IsValidFolder(folder) && AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder(parent, leaf);
            }

            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.name = string.IsNullOrWhiteSpace(name) ? "New Item" : name;

            // nastavení typu přes serializaci (privátní pole)
            var so = new SerializedObject(def);
            so.FindProperty("type").enumValueIndex = (int)type;
            so.ApplyModifiedPropertiesWithoutUndo();

            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, $"Item_{Sanitize(name)}.asset"));
            AssetDatabase.CreateAsset(def, uniquePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            db.Add(def);
            EditorUtility.SetDirty(db);

            Debug.Log($"[Items] Item created: {uniquePath}");
            return def;
        }

        public static ItemDefinition DuplicateItem(ItemDatabase db, ItemDefinition src)
        {
            if (!db || !src) return null;

            string srcPath = AssetDatabase.GetAssetPath(src);
            string dir = Path.GetDirectoryName(srcPath);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, src.name + "_Copy.asset"));
            AssetDatabase.CopyAsset(srcPath, newPath);
            AssetDatabase.ImportAsset(newPath);

            var copy = AssetDatabase.LoadAssetAtPath<ItemDefinition>(newPath);
            if (copy) db.Add(copy);
            EditorUtility.SetDirty(db);
            return copy;
        }

        public static void DeleteItem(ItemDatabase db, ItemDefinition def)
        {
            if (!db || !def) return;
            db.Remove(def);
            string path = AssetDatabase.GetAssetPath(def);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static string Sanitize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Item";
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s.Trim();
        }
    }
}
#endif
