using UnityEngine;
using Obscurus.Items;

namespace Obscurus.Items
{
    /// Připoj na world prefab (např. HealthPotion_Pickup).
    [DisallowMultipleComponent]
    public class ItemIdentity : MonoBehaviour
    {
        public ItemDefinition definition;
        [Min(1)] public int stack = 1;

        void OnValidate()
        {
            if (definition) gameObject.name = $"Pickup_{definition.Name}";
        }
    }
}