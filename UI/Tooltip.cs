using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Tooltip : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform root;      // RectTransform tohoto objektu (pozadí)
    public TMP_Text textLabel;      // Text (TMP) uvnitř
    public CanvasGroup group;       // CanvasGroup na rootu

    [Header("Behavior")]
    public float marginX = 14f;
    public float marginY = 10f;
    public bool stickToTarget = true;

    RectTransform canvasRect;
    Canvas canvas;
    RectTransform target;
    bool visible;
    RectTransform parentRect;

    void Awake()
    {
        if (!root)  root  = transform as RectTransform;
        if (!group) group = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        parentRect = root.parent as RectTransform;

        if (group) { group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false; }

        // anchor = střed parentu (aby seděla soustava souřadnic z ScreenPointToLocalPoint)
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        // pivot = levý střed → levý okraj tooltipu stojí na vypočítaném bodě
        root.pivot     = new Vector2(0f, 0.5f);
    }

    public void Show(RectTransform targetRt, string text)
    {
        target = targetRt;
        if (textLabel) textLabel.text = text;

        // Force layout -> aby se pozadí roztáhlo podle nového textu
        Canvas.ForceUpdateCanvases();
        if (root) LayoutRebuilder.ForceRebuildLayoutImmediate(root);

        root.SetAsLastSibling();           // nahoře nad UI
        visible = true;
        if (group) group.alpha = 1f;

        UpdatePosition();
    }

    public void Hide()
    {
        visible = false;
        if (group) group.alpha = 0f;
    }

    void LateUpdate()
    {
        if (visible && stickToTarget) UpdatePosition();
    }

    void UpdatePosition()
    {
        if (!target || !parentRect) return;

        Camera cam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        // Střed pravé/levé hrany targetu (ve world space)
        Vector3[] c = new Vector3[4];
        target.GetWorldCorners(c);              // 0 BL, 1 TL, 2 TR, 3 BR
        Vector3 rightMidW = (c[2] + c[3]) * 0.5f;
        Vector3 leftMidW  = (c[0] + c[1]) * 0.5f;

        // Do lokálních souřadnic parentRect
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, RectTransformUtility.WorldToScreenPoint(cam, rightMidW), cam, out var rightLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, RectTransformUtility.WorldToScreenPoint(cam, leftMidW),  cam, out var leftLocal);

        // Zjisti, zda by se vpravo nevešel
        var pr   = parentRect.rect;
        var size = root.rect.size;

        // Předběžná pozice vpravo (pivot vlevo)
        Vector2 posRight = rightLocal + new Vector2(marginX, marginY);
        bool wouldOverflowRight = (posRight.x + size.x) > pr.xMax;

        if (!wouldOverflowRight)
        {
            // drž vpravo
            root.pivot = new Vector2(0f, 0.5f);                // levý střed
            root.anchoredPosition = posRight;
        }
        else
        {
            // flipni doleva
            root.pivot = new Vector2(1f, 0.5f);                // pravý střed (ukotvíme pravý okraj tooltipu)
            root.anchoredPosition = leftLocal + new Vector2(-marginX, marginY);
        }

        // Clamp do parentu s ohledem na pivot
        size = root.rect.size;                                 // po změně pivotu se velikost mohla změnit
        var p = root.anchoredPosition;
        float minX = pr.xMin + size.x * root.pivot.x;
        float maxX = pr.xMax - size.x * (1f - root.pivot.x);
        float minY = pr.yMin + size.y * root.pivot.y;
        float maxY = pr.yMax - size.y * (1f - root.pivot.y);
        p.x = Mathf.Clamp(p.x, minX, maxX);
        p.y = Mathf.Clamp(p.y, minY, maxY);
        root.anchoredPosition = p;
    }



}
