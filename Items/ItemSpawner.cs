using UnityEngine;

namespace Obscurus.Items
{
    public class ItemSpawner : MonoBehaviour
    {
        public ItemDatabase db;
        public string itemDisplayName; // nebo si sem dej přímo Id

        [ContextMenu("Spawn Now")]
        public void Spawn()
        {
            if (!db) { Debug.LogWarning("ItemSpawner: missing DB."); return; }
            var def = db.FindByDisplayName(itemDisplayName);
            if (!def || !def.prefab) { Debug.LogWarning("ItemSpawner: item or prefab missing."); return; }
            Instantiate(def.prefab, transform.position, transform.rotation);
        }
    }
}