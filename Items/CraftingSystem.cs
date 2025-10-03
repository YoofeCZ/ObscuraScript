using UnityEngine;
using Obscurus.Player;

namespace Obscurus.Items
{
    public class CraftingSystem : MonoBehaviour
    {
        public RecipeDatabase database;

        public bool TryCraft(PlayerInventory inv, string recipeId)
        {
            if (!database || !inv) return false;
            var r = database.Find(recipeId);
            if (!r || !r.output) return false;

            // check
            foreach (var ing in r.inputs)
                if (!ing.item || inv.CountItem(ing.item) < Mathf.Max(1, ing.count)) return false;

            // consume
            foreach (var ing in r.inputs)
                inv.RemoveItem(ing.item, Mathf.Max(1, ing.count));

            // produce
            return inv.AddItem(r.output, Mathf.Max(1, r.outputCount));
        }
    }
}