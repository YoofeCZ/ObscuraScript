using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HealthPickup : MonoBehaviour
{
    [Header("Amount")]
    [Min(0f)] public float amount = 30f;
    public bool asPercentOfMax = false;   // když true, amount = procento z max (0–100)

    [Header("FX (optional)")]
    public AudioSource sfxOnPickup;
    public GameObject vfxOnPickup;
    public bool destroyOnPickup = true;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        // TIP: aby OnTriggerEnter spolehlivě fungoval i s CharacterController,
        // dej tomuhle objektu kinematický Rigidbody.
        if (!GetComponent<Rigidbody>())
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var hp = other.GetComponentInParent<HealthSystem>();
        if (!hp) return;

        // spočítej množství
        float add = asPercentOfMax ? hp.max * (amount * 0.01f) : amount;

        // HealSource.Pickup → v HealthSystem se uplatní Golden Blood + Lucky Sip
        hp.Heal(add, HealSource.Pickup);

        if (sfxOnPickup) sfxOnPickup.Play();
        if (vfxOnPickup) Instantiate(vfxOnPickup, transform.position, Quaternion.identity);

        if (destroyOnPickup) Destroy(gameObject);
    }
}