#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System.Collections.Generic;
using UnityEngine;
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

namespace Obscurus.Items
{
#if HAS_ODIN
    [HideMonoScript]
    [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
    [InfoBox("Databáze receptů pro Crafting.")]
#endif
    [CreateAssetMenu(fileName = "RecipeDatabase", menuName = "Obscurus/Crafting/Recipe Database")]
    public class RecipeDatabase : ScriptableObject
    {
#if HAS_ODIN
        [TableList(AlwaysExpanded = true)]
#endif
        [SerializeField] private List<RecipeDefinition> recipes = new();

        public IReadOnlyList<RecipeDefinition> Recipes => recipes;

        public void Add(RecipeDefinition def)
        {
            if (!def || recipes.Contains(def)) return;
            recipes.Add(def);
        }
        public void Remove(RecipeDefinition def)
        {
            if (!def) return;
            recipes.Remove(def);
        }

        public RecipeDefinition Find(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return null;
            for (int i = 0; i < recipes.Count; i++)
            {
                var r = recipes[i];
                if (r && r.recipeId == recipeId) return r;
            }
            return null;
        }
    }
}