using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("Obscurus/World/Player Spawner")]
public class PlayerSpawner : MonoBehaviour
{
    [Header("Identification")]
    [Tooltip("Volitelné ID. Když GameManager nastaví pendingSpawnId, použije matching spawner.")]
    public string id = "default";
    [Tooltip("Má tento spawner přednost, pokud se neshoduje ID?")]
    public bool isDefault = true;

    [Header("Capsule Preview")]
    [Tooltip("Celková výška kapsle (např. CharacterController Height).")]
    public float height = 1.8f;
    [Tooltip("Poloměr kapsle (CharacterController Radius).")]
    public float radius = 0.35f;
    [Tooltip("Posun středu kapsle relativně k tomuto objektu.")]
    public Vector3 centerOffset = new Vector3(0, 0.9f, 0);
    [Tooltip("Barva náhledu (alfa < 1 jemně zprůhlední).")]
    public Color gizmoColor = new Color(0.2f, 1f, 0.4f, 1f);
    [Tooltip("Zvýraznit i tělo kapsle (vyplněné koncové koule).")]
    public bool filledCaps = false;

    void Reset()
    {
        name = "PlayerSpawner";
        AutoCenter();
    }

    void OnValidate()
    {
        AutoCenter();
        radius = Mathf.Max(0.01f, radius);
        height = Mathf.Max(height, radius * 2f + 0.01f);
    }

    void AutoCenter()
    {
        if (centerOffset.y < 0.001f) centerOffset.y = Mathf.Max(centerOffset.y, height * 0.5f);
    }

    Vector3 Center => transform.position + transform.rotation * centerOffset;

    void OnDrawGizmos()
    {
        DrawCapsuleGizmo();
    }

    void OnDrawGizmosSelected()
    {
        var c = gizmoColor; c.a = Mathf.Clamp01(gizmoColor.a + 0.15f);
        Gizmos.color = c;
        DrawCapsuleWires();
    }

    void DrawCapsuleGizmo()
    {
        Gizmos.color = gizmoColor;
        DrawCapsuleWires();

        // Forward šipka
        var center = Center;
        Gizmos.DrawLine(center, center + transform.forward * 0.6f);
        Gizmos.DrawSphere(center + transform.forward * 0.6f, 0.03f);

        // Label nad kapslí (jen v editoru)
        #if UNITY_EDITOR
        Handles.color = Color.white;
        var labelPos = GetHemCenter(+1f) + transform.up * 0.15f;
        Handles.Label(labelPos, "PlayerSpawner");
        #endif
    }

    void DrawCapsuleWires()
    {
        var top = GetHemCenter(+1f);
        var bottom = GetHemCenter(-1f);

        if (filledCaps)
        {
            Gizmos.DrawSphere(top, radius * 0.995f);
            Gizmos.DrawSphere(bottom, radius * 0.995f);
        }

        Gizmos.DrawWireSphere(top, radius);
        Gizmos.DrawWireSphere(bottom, radius);

        var right = transform.right * radius;
        var fwd = transform.forward * radius;
        Gizmos.DrawLine(top + right, bottom + right);
        Gizmos.DrawLine(top - right, bottom - right);
        Gizmos.DrawLine(top + fwd,   bottom + fwd);
        Gizmos.DrawLine(top - fwd,   bottom - fwd);
    }

    Vector3 GetHemCenter(float sign)
    {
        var half = Mathf.Max(0, (height * 0.5f) - radius);
        return Center + transform.up * (half * sign);
    }
}
