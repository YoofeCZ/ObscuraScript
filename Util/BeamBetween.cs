using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent]
public class BeamBetween : MonoBehaviour
{
    public Transform a, b;

    public enum LengthAxis { X, Y, Z }
    [Header("Geometry")]
    public LengthAxis lengthAxis = LengthAxis.Z; // Z pro mesh, Y pro Unity Quad
    public float thickness = 0.12f;              // šířka paprsku

    static readonly int _LenID = Shader.PropertyToID("_Length");
    static readonly int _ThkID = Shader.PropertyToID("_Thickness");

    Renderer _r;
    MaterialPropertyBlock _mpb;

    void Awake()
    {
        if (!_r) _r = GetComponentInChildren<Renderer>(true);
        if (_r && _mpb == null) _mpb = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        if (!a || !b)
        {
            if (Application.isPlaying) Destroy(gameObject);
            return;
        }

        Vector3 pa = a.position, pb = b.position;
        Vector3 dir = pb - pa;
        float len = dir.magnitude;
        if (len < 1e-4f) { transform.position = pa; return; }

        Vector3 up = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
        Quaternion look = Quaternion.LookRotation(dir.normalized, up);

        Quaternion axisMap =
            lengthAxis == LengthAxis.Z ? Quaternion.identity :
            lengthAxis == LengthAxis.X ? Quaternion.FromToRotation(Vector3.forward, Vector3.right) :
                                         Quaternion.FromToRotation(Vector3.forward, Vector3.up);

        transform.SetPositionAndRotation((pa + pb) * 0.5f, look * axisMap);

        Vector3 s = lengthAxis switch
        {
            LengthAxis.X => new Vector3(len, thickness, thickness),
            LengthAxis.Y => new Vector3(thickness, len, thickness),
            _            => new Vector3(thickness, thickness, len),
        };
        transform.localScale = s;

        if (_r)
        {
            _r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_LenID, len);
            _mpb.SetFloat(_ThkID, thickness);
            _r.SetPropertyBlock(_mpb);
        }
    }
}
