using UnityEngine;

[DisallowMultipleComponent]
public class PickupMagnet : MonoBehaviour
{
    public float baseRadius = 2.0f;
    public float pullSpeed  = 8.0f;
    public float checkInterval = 0.1f;
    public LayerMask pickupMask = ~0;
    public bool useRigidbodyForce = true;

    Transform _t;
    AlchemyPerks _perks;
    float _timer;

    void Awake()
    {
        _t = transform;
        _perks = GetComponent<AlchemyPerks>() ?? GetComponentInParent<AlchemyPerks>();
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = checkInterval;

        float radius = baseRadius + (_perks ? _perks.ExtraPickupRadius : 0f);

        var cols = Physics.OverlapSphere(_t.position, radius, pickupMask, QueryTriggerInteraction.Ignore);
        if (cols == null || cols.Length == 0) return;

        foreach (var c in cols)
        {
            var pickup = c.GetComponentInParent<Obscurus.Items.WorldItemPickup>();
            if (!pickup) continue;

            var rb = pickup.GetComponent<Rigidbody>();
            Vector3 dir = (_t.position - pickup.transform.position).normalized;
            float   step = pullSpeed * Time.deltaTime;

            if (rb && useRigidbodyForce) rb.AddForce(dir * pullSpeed, ForceMode.Acceleration);
            else pickup.transform.position = Vector3.MoveTowards(pickup.transform.position, _t.position, step);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var perks = Application.isPlaying ? _perks : GetComponent<AlchemyPerks>();
        float r = baseRadius + (perks ? perks.ExtraPickupRadius : 0f);
        Gizmos.color = new Color(0.3f, 0.9f, 0.7f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, r);
    }
#endif
}