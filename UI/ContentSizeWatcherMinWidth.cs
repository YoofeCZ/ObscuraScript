using UnityEngine;

namespace Obscurus.UI
{
    /// <summary>
    /// Drží minimální šířku RectTransformu (aby se layout nerozbil).
    /// </summary>
    public class ContentSizeWatcherMinWidth : MonoBehaviour
    {
        public float minRequiredWidth = 1048f;

        private void OnRectTransformDimensionsChange()
        {
            var rt = GetComponent<RectTransform>();
            if (!rt) return;
            if (rt.rect.width < minRequiredWidth)
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, minRequiredWidth);
        }
    }
}