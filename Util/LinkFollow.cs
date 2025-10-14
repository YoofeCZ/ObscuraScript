using UnityEngine;

[DisallowMultipleComponent]
public class LinkFollow : MonoBehaviour
{
    public Transform a;
    public Transform b;

    private LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        if (lr)
        {
            // pokud máš materiál s HDR barvou, můžeš tady nastavit
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.TransformZ;
        }
    }

    void LateUpdate()
    {
        if (!a || !b)
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            return;
        }

        if (lr)
        {
            lr.positionCount = 2;
            lr.SetPosition(0, a.position);
            lr.SetPosition(1, b.position);
        }
    }
}