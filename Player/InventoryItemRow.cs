using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Obscurus.Items;

namespace Obscurus.UI
{
    public class InventoryItemRow : MonoBehaviour
    {
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text countText;
        public TMP_Text detailText;

        public void Bind(ItemDefinition def, int count)
        {
            if (!def)
            {
                if (nameText) nameText.text = "(null)";
                if (countText) countText.text = "x0";
                if (detailText) detailText.text = "";
                if (icon) icon.enabled = false;
                return;
            }

            if (icon) { icon.enabled = true; icon.sprite = def.icon; }
            if (nameText) nameText.text = def.name;
            if (countText) countText.text = $"x{count}";

            if (detailText)
            {
                if (def.Type == ItemType.Ammunition && def.ammo != null) detailText.text = def.ammo.ammoKey;
                else if (def.Type == ItemType.Currency && def.currency != null) detailText.text = def.currency.currencyKey;
                else if (def.Type == ItemType.Substance && def.substance != null) detailText.text = def.substance.branch.ToString();
                else detailText.text = "";
            }
        }
    }
}