using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ArmorPickup : MonoBehaviour
{
    [Header("Amount")]
    [Min(0f)] public float amount = 20f;
    public bool asPercentOfMax = false;   // když true, amount = procento z max (0–100)

    [Header("FX (optional)")]
    public AudioSource sfxOnPickup;
    public GameObject vfxOnPickup;
    public bool destroyOnPickup = true;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        if (!GetComponent<Rigidbody>())
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var armor = other.GetComponentInParent<ArmorSystem>();
        var hp    = other.GetComponentInParent<HealthSystem>();   // kvůli Lucky Sip
        var perks = other.GetComponentInParent<AlchemyPerks>();
        if (!armor) return;

        // základní množství
        float add = asPercentOfMax ? armor.max * (amount * 0.01f) : amount;

        // Golden Blood platí i pro ARMOR pickupy (+5 %)
        if (perks && perks.goldenBlood)
            add *= (1f + Mathf.Max(0f, perks.goldenBloodPct));

        // přičti armor (ArmorSystem nemá "Add", použijeme Refill na novou hodnotu)
        float target = Mathf.Clamp(armor.Current + add, 0f, armor.max);
        armor.Refill(target);

        // Lucky Sip – 1 s slabý regen okno po ARMOR pickupu
        if (perks && hp) perks.TryActivateLuckySip(hp, HealSource.Pickup);

        if (sfxOnPickup) sfxOnPickup.Play();
        if (vfxOnPickup) Instantiate(vfxOnPickup, transform.position, Quaternion.identity);

        if (destroyOnPickup) Destroy(gameObject);
    }
}