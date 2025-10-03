#if UNITY_EDITOR
#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
#if HAS_ODIN
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;           // RectExtensions (AlignRight/AlignCenter…)
using Sirenix.Utilities.Editor;    // GUIHelper, SirenixEditorGUI, EditorIcons
#endif

namespace Obscurus.Items.Editor
{
#if HAS_ODIN
    /// <summary>
    /// Odin menu editor okno – levý strom (skupiny dle ItemType), vpravo inspector vybraného assetu.
    /// Toolbar: výběr DB, Search, Create/Duplicate/Delete.
    /// </summary>
    public class ItemEditorWindow : OdinMenuEditorWindow
    {
        const string EditorPrefsKey = "Obscurus.ItemEditor.LastDB";

        [MenuItem("Obscurus/Items/Editor %#i")]
        public static void Open()
        {
            var wnd = GetWindow<ItemEditorWindow>("Items");
            wnd.position = GUIHelper.GetEditorWindowRect().AlignCenter(1100, 650);
            wnd.Show();
        }

        [InlineEditor(InlineEditorObjectFieldModes.Hidden)]
        [SerializeField] private ItemDatabase database;

        [SerializeField] private string search = "";

        protected override void Initialize()
        {
            base.Initialize();
            TryRestoreLastDB();
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree(supportsMultiSelect: false)
            {
                { "Dashboard", this, EditorIcons.SettingsCog }
            };

            if (!database)
            {
                tree.Add("⚠ No Database Selected", null);
                return tree;
            }

            var groups = new Dictionary<ItemType, List<ItemDefinition>>();
            foreach (ItemType t in System.Enum.GetValues(typeof(ItemType)))
                groups[t] = new List<ItemDefinition>();

            foreach (var it in database.Items)
                if (it) groups[it.Type].Add(it);

            foreach (var kv in groups)
            {
                var type = kv.Key;
                var list = kv.Value;

                IEnumerable<ItemDefinition> filtered = list;
                if (!string.IsNullOrWhiteSpace(search))
                    filtered = filtered.Where(i =>
                        i.name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        i.ToString().IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0);

                foreach (var it in filtered.OrderBy(i => i.name))
                {
                    // Odin umí jako ikonu Texture / GUIContent. Když item nemá ikonku, dáme fallback.
                    Texture iconTex = it.icon ? it.icon.texture : GetFallbackIconTexture();
                    tree.Add($"{type}/{it.name}", it, iconTex);
                }
            }

            tree.EnumerateTree().AddThumbnailIcons();
            return tree;
        }

        protected override void OnBeginDrawEditors()
        {
            base.OnBeginDrawEditors();

            var toolbarH = this.MenuTree.Config.SearchToolbarHeight;
            SirenixEditorGUI.BeginHorizontalToolbar(toolbarH);

            // DB picker
            var newDb = (ItemDatabase)EditorGUILayout.ObjectField(database, typeof(ItemDatabase), false, GUILayout.Width(240));
            if (newDb != database)
            {
                database = newDb;
                if (database)
                    EditorPrefs.SetString(EditorPrefsKey, AssetDatabase.GetAssetPath(database));
                ForceMenuTreeRebuild();
            }

            // Search
            GUILayout.Space(8);
            GUILayout.Label("Search", GUILayout.Width(48));

            var searchStyle = GetToolbarSearchStyle();
            var newSearch = GUILayout.TextField(search ?? string.Empty, searchStyle, GUILayout.MinWidth(180));

            // Clear button (X)
            if (GUILayout.Button(GUIContent.none, GetToolbarSearchCancelButtonStyle()))
            {
                newSearch = string.Empty;
                GUI.FocusControl(null);
            }

            if (newSearch != search)
            {
                search = newSearch;
                ForceMenuTreeRebuild();
            }

            if (GUILayout.Button(EditorIcons.Refresh.Active, GUILayout.Width(24)))
                ForceMenuTreeRebuild();

            GUILayout.FlexibleSpace();

            // Actions
            using (new EditorGUI.DisabledScope(!database))
            {
                if (SirenixEditorGUI.ToolbarButton(new GUIContent("Create Item")))
                    PopupCreateItem();

                var selected = this.MenuTree.Selection.FirstOrDefault()?.Value as ItemDefinition;

                if (SirenixEditorGUI.ToolbarButton(new GUIContent("Duplicate")) && selected)
                {
                    var copy = ItemAssetUtility.DuplicateItem(database, selected);
                    if (copy) Selection.activeObject = copy;
                    ForceMenuTreeRebuild();
                }

                if (SirenixEditorGUI.ToolbarButton(new GUIContent("Delete")) && selected)
                {
                    if (EditorUtility.DisplayDialog("Delete Item", $"Delete '{selected.name}'?", "Delete", "Cancel"))
                    {
                        ItemAssetUtility.DeleteItem(database, selected);
                        ForceMenuTreeRebuild();
                    }
                }
            }

            SirenixEditorGUI.EndHorizontalToolbar();
        }

        [Button(ButtonSizes.Medium), GUIColor(0.2f, 0.8f, 1f)]
        private void CreateDatabase()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Item Database", "ItemDatabase", "asset", "");
            if (string.IsNullOrEmpty(path)) return;

            var db = ScriptableObject.CreateInstance<ItemDatabase>();
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            database = db;
            EditorPrefs.SetString(EditorPrefsKey, path);
            ForceMenuTreeRebuild();
        }

        private void PopupCreateItem()
        {
            // Pozice/rozměry utility okna – Align* extensiony jsou v Sirenix.Utilities.RectExtensions
            var _ = GUIHelper.GetCurrentLayoutRect().AlignRight(300).AlignCenterY(0);
            var window = OdinEditorWindow.InspectObject(new CreateItemPopup(database, this));
            window.position = new Rect(GUIUtility.GUIToScreenPoint(Event.current.mousePosition), new Vector2(360, 190));
            window.ShowUtility();
        }

        private void TryRestoreLastDB()
        {
            if (database) return;
            var path = EditorPrefs.GetString(EditorPrefsKey, "");
            if (!string.IsNullOrEmpty(path))
            {
                var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
                if (db) database = db;
            }
        }

        // ==== Dashboard (root + podskupina) ====
        [ShowInInspector, DisableInEditorMode]
        [PropertySpace]
        [PropertyOrder(-1)]
        [BoxGroup("Dashboard")]                                      // <- vytvoří kořen
        [BoxGroup("Dashboard/Database", CenterLabel = true)]         // <- podskupina
        [LabelText("Current DB"), InlineEditor(InlineEditorObjectFieldModes.CompletelyHidden)]
        private ItemDatabase _dbPreview => database;

        [BoxGroup("Dashboard")]
        [Button("Create New Database", ButtonSizes.Large), GUIColor(0.2f, 0.8f, 1f)]
        [PropertyOrder(-1)]
        [ShowIf("@database == null")]
        private void _CreateDbBtn() => CreateDatabase();

        // ==== Create popup model ====
        public class CreateItemPopup
        {
            private readonly ItemDatabase db;
            private readonly ItemEditorWindow owner;
            public CreateItemPopup(ItemDatabase db, ItemEditorWindow owner) { this.db = db; this.owner = owner; }

            [LabelText("Name")] public string name = "New Item";
            [LabelText("Type")] public ItemType type = ItemType.Misc;
            [LabelText("Folder (optional)")] public DefaultAsset targetFolder;

            [Button(ButtonSizes.Large), GUIColor(0.2f, 0.8f, 0.2f)]
            private void Create()
            {
                string folder = targetFolder ? AssetDatabase.GetAssetPath(targetFolder) : null;
                var def = ItemAssetUtility.CreateItemAsset(db, name, type, folder);
                if (def)
                {
                    Selection.activeObject = def;
                    owner.TrySelectMenuItemWithObject(def);
                    owner.ForceMenuTreeRebuild();
                    GUIUtility.ExitGUI();
                }
            }
        }

        // ---- helpers ----
        private static Texture2D GetFallbackIconTexture()
        {
            var c = EditorGUIUtility.IconContent("d_Prefab Icon");
            return c != null ? c.image as Texture2D : null;
        }

        private static GUIStyle GetToolbarSearchStyle()
        {
            // Různé Unity verze = různé názvy stylů, zajistíme fallbacky
            return GUI.skin?.FindStyle("ToolbarSearchTextField")
                   ?? GUI.skin?.FindStyle("ToolbarSeachTextField")   // starší překlep ve skinu
#if UNITY_2019_1_OR_NEWER
                   ?? EditorStyles.toolbarSearchField
#else
                   ?? EditorStyles.textField
#endif
                   ;
        }

        private static GUIStyle GetToolbarSearchCancelButtonStyle()
        {
            return GUI.skin?.FindStyle("ToolbarSearchCancelButton")
                   ?? GUI.skin?.FindStyle("ToolbarSeachCancelButton")
                   ?? GUIStyle.none;
        }
    }
#else
    // Fallback, když není Odin: nabídneme jen otevření DB assetu.
    public class ItemEditorWindow : EditorWindow
    {
        [MenuItem("Obscurus/Items/Editor %#i")]
        public static void Open()
        {
            EditorUtility.DisplayDialog("Odin Inspector required",
                "Toto editor okno vyžaduje Odin Inspector.\nOtevři prosím Item Database přímo v Project okně.",
                "OK");
        }
    }
#endif
}
#endif
