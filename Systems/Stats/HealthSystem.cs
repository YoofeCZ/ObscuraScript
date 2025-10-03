#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System;
using UnityEngine;
using Obscurus.Console;
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

[DisallowMultipleComponent]
public class HealthSystem : MonoBehaviour, IConsoleProvider
{
    [Header("Pool")]
    [Min(1)] public float baseMax = 100f; 
    [Min(1)] public float max = 100f;
    [SerializeField, Min(0f)] float current = 100f;

    [Header("Regen (optional)")]
    public bool autoRegen = false;
    [ShowIf(nameof(autoRegen))] public float regenPerSecond = 4f;
    [ShowIf(nameof(autoRegen))] public float regenDelay = 4f;

    [Header("Hit Reactions")]
    [Tooltip("Krátká nezranitelnost po zásahu (anti-chunk).")]
    public float postHitInvuln = 0.15f;
    public bool  clampAtZero   = true;

    [Header("Links (optional)")]
    public ArmorSystem armor;

#if HAS_ODIN
    [FoldoutGroup("Debug"), ReadOnly, ShowInInspector,
     ProgressBar(0, "max", ColorMember = "@current<=1? \"#E07A5F\" : \"#2ECC71\"")]
#endif
    public float Current => current;
    public float Normalized => Mathf.Approximately(max, 0f) ? 0f : Mathf.Clamp01(current / max);

    public event Action<float, float> OnChanged;
    public event Action<float> OnDamaged;
    public event Action<float> OnHealed;
    public event Action OnDied;

    float _regenTimer;
    float _invulnTimer;
    bool _dead;

    void Awake()
    {
        current = Mathf.Clamp(current, 0f, max);
        _dead = current <= 0f;
        RaiseChanged();
    }

    void Update()
    {
        if (_invulnTimer > 0f) _invulnTimer -= Time.unscaledDeltaTime;

        if (autoRegen)
        {
            if (_regenTimer > 0f) _regenTimer -= Time.unscaledDeltaTime;
            else if (current < max && regenPerSecond > 0f && !_dead)
            {
                float old = current;
                current = Mathf.Min(max, current + regenPerSecond * Time.deltaTime);
                if (!Mathf.Approximately(old, current)) { RaiseChanged(); OnHealed?.Invoke(current - old); }
            }
        }
    }
    
    public void ResetToBase()
    {
        max = baseMax;
        current = max;
        RaiseChanged();
    }


    public void LinkArmorIfMissing() { if (!armor) armor = GetComponent<ArmorSystem>(); }

    // === UPRAVENO: heal s označením zdroje + Golden Blood + Lucky Sip ===
    public void Heal(float amount, HealSource src = HealSource.Unknown)
    {
        if (amount <= 0f || _dead) return;

        var perks = GetComponent<AlchemyPerks>();
        if (perks) amount *= perks.GoldenBloodMultiplier(src);   // Golden Blood

        float old = current;
        current = Mathf.Min(max, current + amount);
        if (!Mathf.Approximately(old, current))
        {
            RaiseChanged();
            OnHealed?.Invoke(current - old);
        }

        // Lucky Sip (regen okno po healu, kromě pasivního regenu)
        if (perks) perks.TryActivateLuckySip(this, src);
    }

    public void Damage(float amount)
    {
        if (amount <= 0f || _dead) return;
        if (_invulnTimer > 0f) return;

        LinkArmorIfMissing();

        float toHealth = amount;
        if (armor) toHealth = armor.Absorb(amount);

        if (toHealth <= 0f)
        {
            _regenTimer = regenDelay;
            _invulnTimer = postHitInvuln;
            return;
        }

        // navrhni novou hodnotu
        float proposed = Mathf.Max(0f, current - toHealth);

        // === UPRAVENO: Second Wind (clamp na 10 %, bez i-frames) ===
        bool secondWindTriggered = false;
        var perks = GetComponent<AlchemyPerks>();
        if (perks)
            secondWindTriggered = perks.TrySecondWind(current, max, ref proposed);

        float old = current;
        current = proposed;

        _regenTimer  = regenDelay;
        _invulnTimer = secondWindTriggered ? 0f : postHitInvuln; // bez i-frames při SW

        float delta = old - current;
        if (!Mathf.Approximately(delta, 0f)) { RaiseChanged(); OnDamaged?.Invoke(delta); }

        if (current <= 0f && !_dead)
        {
            _dead = true;
            if (clampAtZero) current = 0f;
            RaiseChanged();
            OnDied?.Invoke();
        }
    }

    public void Kill()
    {
        if (_dead) return;
        current = 0f;
        _dead = true;
        RaiseChanged();
        OnDied?.Invoke();
    }

    public void Refill(float toFull = -1f)
    {
        float target = (toFull > 0f) ? toFull : max;
        current = Mathf.Clamp(target, 0f, max);
        _dead = current <= 0f;
        _regenTimer = 0f; _invulnTimer = 0f;
        RaiseChanged();
    }

    public void SetMax(float newMax, bool keepRatio = true)
    {
        newMax = Mathf.Max(1f, newMax);
        if (keepRatio)
            current = Mathf.Clamp01(current / Mathf.Max(1e-4f, max)) * newMax;
        else
            current = Mathf.Min(current, newMax);
        max = newMax;
        _dead = current <= 0f;
        RaiseChanged();
    }

    void RaiseChanged() => OnChanged?.Invoke(current, max);

    // ===== Console commands (instance) =====

    [ConsoleCommand("hp", "HP: bez param = zobraz, +N/-N = přidej/uber, 'set N', 'max N'.")]
    public string CmdHp(string op = null, float amount = 0f)
    {
        if (string.IsNullOrWhiteSpace(op))
            return $"HP = {Current:0.#}/{max:0.#}";

        op = op.Trim();
        if (float.TryParse(op, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return ApplyDelta(parsed);

        if (op.StartsWith("+") || op.StartsWith("-"))
        {
            var s = op.Replace("+", "");
            if (float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                return ApplyDelta(op[0] == '-' ? -v : v);
        }

        if (op.Equals("set", StringComparison.OrdinalIgnoreCase))
        {
            float target = Mathf.Clamp(amount, 0f, max);
            float diff = target - Current;
            if (diff >= 0) Heal(diff); else Damage(-diff);
            return $"HP set -> {Current:0.#}/{max:0.#}";
        }

        if (op.Equals("max", StringComparison.OrdinalIgnoreCase))
        {
            SetMax(Mathf.Max(1f, amount));
            return $"HP max -> {max:0.#} (cur {Current:0.#})";
        }

        return "Usage: hp | hp +N | hp -N | hp set N | hp max N";

        string ApplyDelta(float d)
        {
            if (d >= 0) Heal(d); else Damage(-d);
            return $"HP = {Current:0.#}/{max:0.#}";
        }
    }

    [ConsoleCommand("heal", "heal N — přidá N HP")]
    public string CmdHeal(float amount)
    {
        Heal(Mathf.Max(0f, amount));
        return $"HP = {Current:0.#}/{max:0.#}";
    }

    [ConsoleCommand("damage", "damage N — aplikuje zranění (respektuje Armor)")]
    public string CmdDamage(float amount)
    {
        Damage(Mathf.Max(0f, amount));
        return $"HP = {Current:0.#}/{max:0.#}";
    }

    [ConsoleCommand("kill", "Okamžitě zabije hráče.")]
    public string CmdKill()
    {
        Kill();
        return "Player killed.";
    }
}
