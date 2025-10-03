#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Obscurus.Items;
using Obscurus.Items.Editor;
using System.IO;

public static class CreateCoreItemsMenu
{
    [MenuItem("Obscurus/Items/Create Core RPG Items...", priority = 10)]
    public static void Run()
    {
        var db = Selection.activeObject as ItemDatabase;
        if (!db)
        {
            var path = EditorUtility.OpenFilePanel("Select ItemDatabase", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;
            path = "Assets" + path.Replace(Application.dataPath, "");
            db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
        }
        if (!db) { EditorUtility.DisplayDialog("Items", "Select ItemDatabase (asset) first.", "OK"); return; }

        var dbPath = AssetDatabase.GetAssetPath(db);
        var dir = Path.GetDirectoryName(dbPath);
        var target = Path.Combine(dir, "Core");
        if (!AssetDatabase.IsValidFolder(target))
        {
            var parent = dir;
            if (!AssetDatabase.IsValidFolder(parent)) parent = "Assets";
            AssetDatabase.CreateFolder(parent, "Core");
        }

        // helpers
        ItemDefinition Make(string name, ItemType t)
            => ItemAssetUtility.CreateItemAsset(db, name, t, target);

        void SetSO(ItemDefinition def, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(def);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
        }

        // ===== Currency =====
        var groschen = Make("Groschen", ItemType.Currency);
        SetSO(groschen, so => {
            so.FindProperty("displayName").stringValue = "Pražské groše";
            so.FindProperty("rarity").enumValueIndex = (int)Rarity.Common;
            so.FindProperty("maxStack").intValue = 999999;
        });

        // ===== Materials =====
        var scrap = Make("Scrap", ItemType.Material);
        SetSO(scrap, so => {
            so.FindProperty("displayName").stringValue = "Šrot";
        });
        AddTag(scrap, "Scrap");

        var herb = Make("Herb", ItemType.Material);
        SetSO(herb, so => { so.FindProperty("displayName").stringValue = "Byliny"; });
        AddTag(herb, "Herb");

        var jar = Make("Jar", ItemType.Material);
        SetSO(jar, so => { so.FindProperty("displayName").stringValue = "Sklenice"; });
        AddTag(jar, "Jar");

        var nails = Make("NailsAmmo", ItemType.Material);
        SetSO(nails, so => { so.FindProperty("displayName").stringValue = "Hřebíková munice"; so.FindProperty("maxStack").intValue = 999; });
        AddTag(nails, "Ammo_Nails");

        // ===== Potions (Consumable) =====
        var potH = Make("Potion_Health", ItemType.Consumable);
        AddEffect(potH, ItemEffectType.HealHP, value: 50);

        var potS = Make("Potion_Sanity", ItemType.Consumable);
        AddEffect(potS, ItemEffectType.RestoreSanity, value: 40);

        // ===== Substances =====
        MakeSubstance("Substance_Vitriol", SubstanceBranch.Vitriol, "Větev Síly (útok, krvácení, exploze)");
        MakeSubstance("Substance_Aurum",   SubstanceBranch.Aurum,   "Větev Života (HP, resisty, sanity regen)");
        MakeSubstance("Substance_Mercurius", SubstanceBranch.Mercurius, "Větev Rychlosti (pohyb, fire-rate, cooldowny)");

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Items", "Core RPG Items created into: " + target, "OK");

        // ------ locals ------
        void AddTag(ItemDefinition def, string tag)
        {
            var so = new SerializedObject(def);
            var type = (ItemType)so.FindProperty("type").enumValueIndex;
            if (type != ItemType.Material) return;
            var mat = so.FindProperty("material");
            if (mat == null) return;
            var tags = mat.FindPropertyRelative("tags");
            int i = tags.arraySize; tags.InsertArrayElementAtIndex(i);
            tags.GetArrayElementAtIndex(i).stringValue = tag;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
        }

        void AddEffect(ItemDefinition def, ItemEffectType effect, float value=0, float duration=0, float perSec=0)
        {
            var so = new SerializedObject(def);
            so.FindProperty("displayName").stringValue = def.name.Replace('_',' ');
            so.FindProperty("maxStack").intValue = 10;
            var cons = so.FindProperty("consumable");
            var list = cons.FindPropertyRelative("effects");
            int i = list.arraySize; list.InsertArrayElementAtIndex(i);
            var el = list.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("type").enumValueIndex = (int)effect;
            el.FindPropertyRelative("value").floatValue = value;
            el.FindPropertyRelative("duration").floatValue = duration;
            el.FindPropertyRelative("perSecond").floatValue = perSec;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
        }

        void MakeSubstance(string assetName, SubstanceBranch branch, string lore)
        {
            var sub = Make(assetName, ItemType.Substance);
            var so = new SerializedObject(sub);
            so.FindProperty("displayName").stringValue = assetName.Replace('_', ' ');
            var sdata = so.FindProperty("substance");
            if (sdata != null)
            {
                sdata.FindPropertyRelative("branch").enumValueIndex = (int)branch;
                sdata.FindPropertyRelative("lore").stringValue = lore;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sub);
        }
    }
}
#endif
