using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [TextArea] public string message = "Popis tlačítka";
    public Tooltip tooltip;

    RectTransform rect;

    void Awake() => rect = transform as RectTransform;

    public void OnPointerEnter(PointerEventData e)
    {
        if (tooltip) tooltip.Show(rect, message);
    }

    public void OnPointerMove(PointerEventData e)
    {
        // jen udrží tooltip nalepený, když se hýbe myš
        if (tooltip) tooltip.Show(rect, message);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (tooltip) tooltip.Hide();
    }
}