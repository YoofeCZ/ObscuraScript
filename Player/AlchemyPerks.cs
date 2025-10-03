using System.Collections;
using UnityEngine;

/// Zdroje healu – kvůli podmínkám (Golden Blood, Lucky Sip atd.)
public enum HealSource { Unknown = 0, Pickup, Consumable, Scripted, Regen }

public class AlchemyPerks : MonoBehaviour
{
    // ===== AURUM =====
    [Header("AURUM — Sanctum Vitae")]
    public bool goldenBlood = false;
    [Range(0f, 1f)] public float goldenBloodPct = 0.05f;   // +5 %

    public bool calmNerves = false;
    [Range(0f, 1f)] public float calmNervesReduce = 0.10f;  // −10 % duration (hook jen jako funkce níž)

    public bool luckySip = false;
    public float luckySipDuration = 1.0f;     // s
    public float luckySipRegenPerSec = 1.0f;  // HP/s
    public float luckySipCooldown = 45f;      // s
    float luckySipTimer = 0f;

    public bool secondWind = false;
    [Range(0f, 0.5f)] public float secondWindThreshold = 0.10f; // 10 % max HP
    public float secondWindCooldown = 120f;
    float secondWindTimer = 0f;

    // ===== VITRIOL (hooky – dopojíš později ve zbraních/effects) =====
    [Header("VITRIOL — Signum Decay (hooks)")]
    public bool corrosionTag = false;
    public bool weakSpotGlow = false;
    public bool pinprickDot  = false;
    public bool lingeringAcid = false;

    // ===== MERCURIUS =====
    [Header("MERCURIUS — Flux")]
    public bool quickHands = false;
    [Range(0.5f, 1.0f)] public float quickHandsTimeMult = 0.95f; // −5 %

    public bool sidewind = false;
    [Range(1f, 1.2f)] public float sidewindStrafeMult = 1.03f;   // +3 %

    public bool coyoteStep = false;
    [Range(0f, 0.3f)] public float extraCoyoteSeconds = 0.08f;   // +80 ms

    public bool vacuumReach = false;
    [Range(0f, 2f)] public float extraPickupRadius = 0.3f;       // +0.3 m

    // ===== Public API pro ostatní systémy =====

    // Golden Blood – multiplikátor na healy, kromě pasivního regenu
    public float GoldenBloodMultiplier(HealSource src)
    {
        if (!goldenBlood) return 1f;
        if (src == HealSource.Regen) return 1f; // neovlivňuj pasivní regen
        return 1f + Mathf.Max(0f, goldenBloodPct);
    }

    // Calm Nerves – zkrátí trvání negativního statusu
    public float ModifyStatusDuration(float baseSeconds)
        => calmNerves ? baseSeconds * (1f - calmNervesReduce) : baseSeconds;

    // Lucky Sip – spustí krátký regen, když přijde heal (mimo regen)
    public void TryActivateLuckySip(HealthSystem hp, HealSource src)
    {
        if (!luckySip || hp == null) return;
        if (src == HealSource.Regen) return;
        if (luckySipTimer > 0f) return;

        StartCoroutine(CoLuckySipRegen(hp, luckySipDuration, luckySipRegenPerSec));
        luckySipTimer = luckySipCooldown;
    }

    IEnumerator CoLuckySipRegen(HealthSystem hp, float dur, float perSec)
    {
        float t = 0f;
        while (t < dur && hp != null)
        {
            hp.Heal(perSec * Time.deltaTime, HealSource.Regen); // označíme jako regen
            t += Time.deltaTime;
            yield return null;
        }
    }

    // Second Wind – pokusí se clampnout aktuální HP na práh (bez i-frames)
    public bool TrySecondWind(float beforeCurrent, float max, ref float proposedCurrent)
    {
        if (!secondWind) return false;
        if (secondWindTimer > 0f) return false;

        float threshold = Mathf.Max(1f, max * secondWindThreshold);
        if (beforeCurrent > threshold && proposedCurrent < threshold && proposedCurrent > 0f)
        {
            proposedCurrent = threshold;          // zůstaneš na prahu
            secondWindTimer = secondWindCooldown; // start CD
            return true;
        }
        return false;
    }
    
    // === DOPLNĚNÉ API: dotaz + odemknutí podle PerkId ===
    public bool IsUnlocked(PerkId id)
    {
        switch (id)
        {
            case PerkId.Aurum_GoldenBlood:   return goldenBlood;
            case PerkId.Aurum_CalmNerves:    return calmNerves;
            case PerkId.Aurum_LuckySip:      return luckySip;
            case PerkId.Aurum_SecondWind:    return secondWind;

            case PerkId.Vitriol_CorrosionTag:return corrosionTag;
            case PerkId.Vitriol_WeakSpotGlow:return weakSpotGlow;
            case PerkId.Vitriol_PinprickDot: return pinprickDot;
            case PerkId.Vitriol_LingeringAcid:return lingeringAcid;

            case PerkId.Mercurius_QuickHands:return quickHands;
            case PerkId.Mercurius_Sidewind:  return sidewind;
            case PerkId.Mercurius_CoyoteStep:return coyoteStep;
            case PerkId.Mercurius_VacuumReach:return vacuumReach;
        }
        return false;
    }

    public void SetUnlocked(PerkId id, bool on)
    {
        switch (id)
        {
            case PerkId.Aurum_GoldenBlood:   goldenBlood = on; break;
            case PerkId.Aurum_CalmNerves:    calmNerves  = on; break;
            case PerkId.Aurum_LuckySip:      luckySip    = on; break;
            case PerkId.Aurum_SecondWind:    secondWind  = on; break;

            case PerkId.Vitriol_CorrosionTag: corrosionTag = on; break;
            case PerkId.Vitriol_WeakSpotGlow: weakSpotGlow = on; break;
            case PerkId.Vitriol_PinprickDot:  pinprickDot  = on; break;
            case PerkId.Vitriol_LingeringAcid:lingeringAcid= on; break;

            case PerkId.Mercurius_QuickHands: quickHands   = on; break;
            case PerkId.Mercurius_Sidewind:   sidewind     = on; break;
            case PerkId.Mercurius_CoyoteStep: coyoteStep   = on; break;
            case PerkId.Mercurius_VacuumReach:vacuumReach  = on; break;
        }
        // případně sem můžeš přidat OnChanged event pro UI
    }


    // Quick Hands – multiplikátor pro časy reload/swap/oil (použij kde potřebuješ)
    public float TimeMult_QuickHands => quickHands ? quickHandsTimeMult : 1f;

    // Sidewind – multiplikátor na strafe, pokud hráč střílí
    public float StrafeMult_WhileShooting(bool isShooting) => (sidewind && isShooting) ? sidewindStrafeMult : 1f;

    // Coyote – +tolerance
    public float ExtraCoyoteSeconds => coyoteStep ? extraCoyoteSeconds : 0f;

    // Vacuum – +radius pro sběr (pokud máš magnet script)
    public float ExtraPickupRadius => vacuumReach ? extraPickupRadius : 0f;

    void Update()
    {
        if (luckySipTimer   > 0f) luckySipTimer   -= Time.deltaTime;
        if (secondWindTimer > 0f) secondWindTimer -= Time.deltaTime;
    }
}
