#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Obscurus.Items.Editor
{
    public static class RecipeAssetUtility
    {
        public static RecipeDefinition CreateRecipeAsset(RecipeDatabase db, string name, string category, string targetFolder = null)
        {
            if (!db) { Debug.LogError("[Crafting] RecipeDatabase is null."); return null; }

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

            var def = ScriptableObject.CreateInstance<RecipeDefinition>();
            def.name = string.IsNullOrWhiteSpace(name) ? "New Recipe" : name;
            def.recipeId = Sanitize(def.name).ToLowerInvariant();
            def.category = category ?? "General";

            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, $"Recipe_{Sanitize(name)}.asset"));
            AssetDatabase.CreateAsset(def, uniquePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            db.Add(def);
            EditorUtility.SetDirty(db);

            Debug.Log($"[Crafting] Recipe created: {uniquePath}");
            return def;
        }

        public static RecipeDefinition DuplicateRecipe(RecipeDatabase db, RecipeDefinition src)
        {
            if (!db || !src) return null;

            string srcPath = AssetDatabase.GetAssetPath(src);
            string dir = Path.GetDirectoryName(srcPath);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, src.name + "_Copy.asset"));
            AssetDatabase.CopyAsset(srcPath, newPath);
            AssetDatabase.ImportAsset(newPath);

            var copy = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(newPath);
            if (copy) db.Add(copy);
            EditorUtility.SetDirty(db);
            return copy;
        }

        public static void DeleteRecipe(RecipeDatabase db, RecipeDefinition def)
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
            if (string.IsNullOrWhiteSpace(s)) return "Recipe";
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s.Trim();
        }
    }
}
#endif
