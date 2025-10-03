using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Obscurus.UI
{
    [DisallowMultipleComponent]
    public class KeyValueRow : MonoBehaviour
    {
        [Header("UI Refs")]
        public Image icon;
        public TextMeshProUGUI label;
        public TextMeshProUGUI value;

        [Tooltip("Volitelně tlačítko (např. '+') pro daný řádek.")]
        public Button extraButton;

        // --- Helpery, ať to můžeš rychle napojit ---
        public void SetLabel(string text) { if (label) label.text = text; }
        public void SetValue(string text) { if (value) value.text = text; }

        public void SetPair(float current, float max)
        {
            if (value) value.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";
        }

        public void SetIcon(Sprite s)
        {
            if (icon)
            {
                icon.sprite = s;
                icon.enabled = s != null;
            }
        }

        public void SetPlusVisible(bool v)
        {
            if (extraButton) extraButton.gameObject.SetActive(v);
        }
    }
}