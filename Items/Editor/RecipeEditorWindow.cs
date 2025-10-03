#if UNITY_EDITOR
#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System.Linq;
using UnityEditor;
using UnityEngine;
#if HAS_ODIN
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;  
using Sirenix.Utilities.Editor;
#endif

namespace Obscurus.Items.Editor
{
#if HAS_ODIN
    /// <summary>Odin editor pro Recipe DB (podobně jako ItemEditorWindow).</summary>
    public class RecipeEditorWindow : OdinMenuEditorWindow
    {
        const string EditorPrefsKey = "Obscurus.RecipeEditor.LastDB";

        [MenuItem("Obscurus/Crafting/Recipe Editor %#r")]
        public static void Open()
        {
            var wnd = GetWindow<RecipeEditorWindow>("Recipes");
            wnd.position = GUIHelper.GetEditorWindowRect().AlignCenter(1000, 620);
            wnd.Show();
        }

        [InlineEditor(InlineEditorObjectFieldModes.Hidden)]
        [SerializeField] private RecipeDatabase database;

        [SerializeField] private string search = "";

        protected override void Initialize()
        {
            base.Initialize();
            TryRestoreLastDB();
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree(false)
            {
                { "Dashboard", this, EditorIcons.SettingsCog }
            };

            if (!database)
            {
                tree.Add("⚠ No Database Selected", null);
                return tree;
            }

            var groups = database.Recipes.GroupBy(r => r ? r.category : "Uncategorized").OrderBy(g => g.Key);
            foreach (var g in groups)
            {
                foreach (var r in g.Where(x => x && (string.IsNullOrEmpty(search) ||
                             x.name.ToLowerInvariant().Contains(search.ToLowerInvariant()) ||
                             x.recipeId.ToLowerInvariant().Contains(search.ToLowerInvariant()))).OrderBy(x => x.name))
                {
                    tree.Add($"{g.Key}/{r.name}", r);
                }
            }

            tree.EnumerateTree().AddThumbnailIcons();
            return tree;
        }

        protected override void OnBeginDrawEditors()
        {
            base.OnBeginDrawEditors();

            SirenixEditorGUI.BeginHorizontalToolbar(this.MenuTree.Config.SearchToolbarHeight);

            // DB picker
            var newDb = (RecipeDatabase)EditorGUILayout.ObjectField(database, typeof(RecipeDatabase), false, GUILayout.Width(260));
            if (newDb != database)
            {
                database = newDb;
                if (database) EditorPrefs.SetString(EditorPrefsKey, AssetDatabase.GetAssetPath(database));
                ForceMenuTreeRebuild();
            }

            GUILayout.Space(8);
            GUILayout.Label("Search", GUILayout.Width(48));
            var newSearch = GUILayout.TextField(search ?? string.Empty, EditorStyles.toolbarSearchField, GUILayout.MinWidth(200));
            if (newSearch != search) { search = newSearch; ForceMenuTreeRebuild(); }

            if (GUILayout.Button(EditorIcons.Refresh.Active, GUILayout.Width(24)))
                ForceMenuTreeRebuild();

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!database))
            {
                if (SirenixEditorGUI.ToolbarButton(new GUIContent("Create Recipe")))
                    PopupCreateRecipe();

                var selected = this.MenuTree.Selection.FirstOrDefault()?.Value as RecipeDefinition;

                if (SirenixEditorGUI.ToolbarButton(new GUIContent("Duplicate")) && selected)
                {
                    var copy = RecipeAssetUtility.DuplicateRecipe(database, selected);
                    if (copy) Selection.activeObject = copy;
                    ForceMenuTreeRebuild();
                }

                if (SirenixEditorGUI.ToolbarButton(new GUIContent("Delete")) && selected)
                {
                    if (EditorUtility.DisplayDialog("Delete Recipe", $"Delete '{selected.name}'?", "Delete", "Cancel"))
                    {
                        RecipeAssetUtility.DeleteRecipe(database, selected);
                        ForceMenuTreeRebuild();
                    }
                }
            }

            SirenixEditorGUI.EndHorizontalToolbar();
        }

        [Button(ButtonSizes.Medium), GUIColor(0.2f, 0.8f, 1f)]
        private void CreateDatabase()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Recipe Database", "RecipeDatabase", "asset", "");
            if (string.IsNullOrEmpty(path)) return;

            var db = ScriptableObject.CreateInstance<RecipeDatabase>();
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            database = db;
            EditorPrefs.SetString(EditorPrefsKey, path);
            ForceMenuTreeRebuild();
        }

        private void PopupCreateRecipe()
        {
            var window = OdinEditorWindow.InspectObject(new CreateRecipePopup(database, this));
            window.position = new Rect(GUIUtility.GUIToScreenPoint(Event.current.mousePosition), new Vector2(360, 160));
            window.ShowUtility();
        }

        private void TryRestoreLastDB()
        {
            if (database) return;
            var path = EditorPrefs.GetString(EditorPrefsKey, "");
            if (!string.IsNullOrEmpty(path))
            {
                var db = AssetDatabase.LoadAssetAtPath<RecipeDatabase>(path);
                if (db) database = db;
            }
        }

        // Dashboard
        [ShowInInspector, DisableInEditorMode]
        [PropertySpace]
        [PropertyOrder(-1)]
        [BoxGroup("Dashboard")]
        [BoxGroup("Dashboard/Database", CenterLabel = true)]
        [LabelText("Current DB"), InlineEditor(InlineEditorObjectFieldModes.CompletelyHidden)]
        private RecipeDatabase _dbPreview => database;

        [BoxGroup("Dashboard")]
        [Button("Create New Database", ButtonSizes.Large), GUIColor(0.2f, 0.8f, 1f)]
        [PropertyOrder(-1)]
        [ShowIf("@database == null")]
        private void _CreateDbBtn() => CreateDatabase();

        // Create popup model
        public class CreateRecipePopup
        {
            private readonly RecipeDatabase db;
            private readonly RecipeEditorWindow owner;
            public CreateRecipePopup(RecipeDatabase db, RecipeEditorWindow owner) { this.db = db; this.owner = owner; }

            [LabelText("Name")] public string name = "New Recipe";
            [LabelText("Category")] public string category = "General";
            [LabelText("Folder (optional)")] public DefaultAsset targetFolder;

            [Button(ButtonSizes.Large), GUIColor(0.2f, 0.8f, 0.2f)]
            private void Create()
            {
                string folder = targetFolder ? AssetDatabase.GetAssetPath(targetFolder) : null;
                var def = RecipeAssetUtility.CreateRecipeAsset(db, name, category, folder);
                if (def)
                {
                    Selection.activeObject = def;
                    owner.TrySelectMenuItemWithObject(def);
                    owner.ForceMenuTreeRebuild();
                    GUIUtility.ExitGUI();
                }
            }
        }
    }
#else
    public class RecipeEditorWindow : EditorWindow
    {
        [MenuItem("Obscurus/Crafting/Recipe Editor %#r")]
        public static void Open()
        {
            EditorUtility.DisplayDialog("Odin Inspector required",
                "Toto editor okno vyžaduje Odin Inspector.\nOtevři prosím Recipe Database přímo v Project okně.",
                "OK");
        }
    }
#endif
}
#endif
