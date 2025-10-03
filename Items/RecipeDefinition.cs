#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System;
using System.Collections.Generic;
using UnityEngine;
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

namespace Obscurus.Items
{
    [Serializable]
    public struct ItemCount
    {
        public ItemDefinition item;
        [Min(1)] public int count;
    }

#if HAS_ODIN
    [HideMonoScript]
    [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
    [InfoBox("Recept pro Crafting.")]
#endif
    [CreateAssetMenu(fileName = "Recipe_", menuName = "Obscurus/Crafting/Recipe")]
    public class RecipeDefinition : ScriptableObject
    {
#if HAS_ODIN
        [HorizontalGroup("row", 0.65f)] [LabelText("Recipe Id"), Tooltip("Unikátní klíč (stabilní).")]
#endif
        public string recipeId = "scrap_to_nails";

#if HAS_ODIN
        [HorizontalGroup("row", 0.35f)] [LabelText("Category")]
#endif
        public string category = "Ammo";

#if HAS_ODIN
        [TableList(AlwaysExpanded = true)]
#endif
        public List<ItemCount> inputs = new();

#if HAS_ODIN
        [VerticalGroup("out")] [LabelText("Output")]
#endif
        public ItemDefinition output;

#if HAS_ODIN
        [VerticalGroup("out")] [LabelText("Count"), MinValue(1)]
#endif
        public int outputCount = 1;

        public override string ToString() => $"{recipeId} -> {output?.name} x{outputCount}";
    }
}