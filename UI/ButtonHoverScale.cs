using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Target")]
    public RectTransform visual;   // <- přiřaď child "Visual"

    [Header("Effect")]
    public float scaleFactor = 1.15f;   // 1.15–1.25 je „tak akorát“
    public float speed = 14f;

    Vector3 baseScale;
    Vector3 targetScale;
    bool hovered;

    void Awake()
    {
        if (!visual) visual = transform as RectTransform; // fallback
        baseScale   = visual.localScale;
        targetScale = baseScale;
    }

    void Update()
    {
        var goal = hovered ? baseScale * scaleFactor : baseScale;
        visual.localScale = Vector3.Lerp(visual.localScale, goal, Time.unscaledDeltaTime * speed);
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit (PointerEventData e) => hovered = false;
}