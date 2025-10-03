using UnityEngine;
using UnityEngine.UI;

public class PerkTickOnly : MonoBehaviour
{
    public AlchemyTreeShop shop;
    public PerkId perk;
    public GameObject ownedTick;

    void OnEnable()
    {
        // najdi si shop/tick
        if (!shop) shop = GetComponentInParent<AlchemyTreeShop>();

        if (!ownedTick)
        {
            var t = transform.Find("Tick");
            if (!t)
            {
                // fallback: vezmi první child Image, co má v názvu "tick"
                var imgs = GetComponentsInChildren<Image>(true);
                foreach (var img in imgs)
                    if (img && img.name.ToLower().Contains("tick")) { t = img.transform; break; }
            }
            if (t) ownedTick = t.gameObject;
        }

        // DEFAULT: dokud nevíme, že je odemčeno, fajfku skryj
        if (ownedTick) ownedTick.SetActive(false);
    }

    void Update()
    {
        if (!ownedTick) return;
        if (!shop) shop = GetComponentInParent<AlchemyTreeShop>();

        bool unlocked = shop && shop.perks && shop.perks.IsUnlocked(perk);
        if (ownedTick.activeSelf != unlocked)
            ownedTick.SetActive(unlocked);
    }
}