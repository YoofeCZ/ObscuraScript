using UnityEngine;
using Obscurus.Items;

namespace Obscurus.Player
{
    /// <summary> Jednoduchý strom – drží počty investic do tří větví. </summary>
    public class PlayerSubstanceTree : MonoBehaviour
    {
        public int vitriolPoints;
        public int aurumPoints;
        public int mercuriusPoints;

        [Header("Refs")]
        public PlayerInventory inventory;
        public PlayerStats stats;

        public bool Invest(ItemDefinition substanceItem, int count = 1)
        {
            if (!substanceItem || substanceItem.Type != ItemType.Substance || substanceItem.substance == null) return false;
            if (!inventory || !stats) return false;
            if (inventory.CountItem(substanceItem) < count) return false;

            inventory.RemoveItem(substanceItem, count);

            for (int i = 0; i < count; i++)
            {
                switch (substanceItem.substance.branch)
                {
                    case SubstanceBranch.Vitriol:   vitriolPoints++;   stats.AddVitriolTier();   break;
                    case SubstanceBranch.Aurum:     aurumPoints++;     stats.AddAurumTier();     break;
                    case SubstanceBranch.Mercurius: mercuriusPoints++; stats.AddMercuriusTier(); break;
                }
            }
            return true;
        }
    }
}