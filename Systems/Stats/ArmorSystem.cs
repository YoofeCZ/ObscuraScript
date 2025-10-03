#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System;
using UnityEngine;
using Obscurus.Console;           // <<
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

#if HAS_ODIN
[HideMonoScript]
[InfoBox("Armor = štít. Při zásahu absorbuje část dmg do armoru; zbytek propadne do Health. Armor se po delayi regeneruje.")]
#endif
[DisallowMultipleComponent]
public class ArmorSystem : MonoBehaviour, IConsoleProvider      // << IMPLEMENTACE IConsoleProvider
{
    
    [Header("Pool")]
    [Min(1)] public float baseMax = 50f;
    [Min(0)] public float max = 50f;
    [SerializeField, Min(0f)] float current = 50f;

    [Header("Absorpce")]
    [Range(0f, 1f)] public float absorbPercent = 0.85f;
    [Range(0f, 1f)] public float passthroughWhileArmor = 0.15f;

    [Header("Regen")]
    public float regenPerSecond = 10f;
    public float regenDelay = 3.0f;

#if HAS_ODIN
    [FoldoutGroup("Debug"), ReadOnly, ShowInInspector,
     ProgressBar(0, "max", ColorMember = "@current<=1? \"#E07A5F\" : \"#1E90FF\"")]
#endif
    public float Current => current;
    public float Normalized => Mathf.Approximately(max, 0f) ? 0f : Mathf.Clamp01(current / max);

    public event Action<float, float> OnChanged;  // (cur, max)
    public event Action OnBroken;
    public event Action OnRecharged;

    float _regenTimer;
    bool _wasBroken;

    void Awake()
    {
        current = Mathf.Clamp(current, 0f, max);
        RaiseChanged();
        _wasBroken = current <= 0f;
    }

    void Update()
    {
        if (_regenTimer > 0f) _regenTimer -= Time.unscaledDeltaTime;
        else if (current < max && regenPerSecond > 0f)
        {
            var old = current;
            current = Mathf.Min(max, current + regenPerSecond * Time.deltaTime);
            if (!Mathf.Approximately(old, current))
            {
                RaiseChanged();
                if (_wasBroken && current > 0f) { _wasBroken = false; OnRecharged?.Invoke(); }
            }
        }
    }
    public void ResetToBase()
    {
        max = baseMax;
        current = max;
        RaiseChanged();
    }

    public float Absorb(float damage)
    {
        if (damage <= 0f) return 0f;
        float leak = 0f;

        if (current > 0f && absorbPercent > 0f)
        {
            float toArmor = damage * absorbPercent;
            float toLeak  = damage * passthroughWhileArmor;

            if (current >= toArmor)
            {
                current -= toArmor;
                leak = toLeak;
            }
            else
            {
                float absorbed = current;
                current = 0f;
                float notAbsorbed = toArmor - absorbed;
                leak = toLeak + notAbsorbed + (damage * (1f - absorbPercent));
                if (!_wasBroken) { _wasBroken = true; OnBroken?.Invoke(); }
            }
            _regenTimer = regenDelay;
            RaiseChanged();
        }
        else
        {
            leak = damage;
        }

        return Mathf.Max(0f, leak);
    }

    public void Refill(float toFull = -1f)
    {
        var target = (toFull > 0f) ? toFull : max;
        var old = current;
        current = Mathf.Clamp(target, 0f, max);
        _regenTimer = 0f;
        RaiseChanged();
        if (old <= 0f && current > 0f) { _wasBroken = false; OnRecharged?.Invoke(); }
    }

    public void SetMax(float newMax, bool keepRatio = true)
    {
        newMax = Mathf.Max(0f, newMax);
        if (keepRatio)
            current = Mathf.Clamp01(max <= 0 ? 0 : current / max) * newMax;
        else
            current = Mathf.Min(current, newMax);
        max = newMax;
        RaiseChanged();
    }

    void RaiseChanged() => OnChanged?.Invoke(current, max);

    // ===== Console commands (instance) =====

    [ConsoleCommand("armor", "Armor: bez param = zobraz, +N/-N, 'set N', 'max N'.")]
    public string CmdArmor(string op = null, float amount = 0f)
    {
        if (string.IsNullOrWhiteSpace(op))
            return $"AR = {Current:0.#}/{max:0.#}";

        op = op.Trim();
        if (float.TryParse(op, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var delta))
            return Apply(delta);

        if (op.StartsWith("+") || op.StartsWith("-"))
        {
            var s = op.Replace("+", "");
            if (float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                return Apply(op[0] == '-' ? -v : v);
        }

        if (op.Equals("set", StringComparison.OrdinalIgnoreCase))
        {
            Refill(amount);
            return $"AR set -> {Current:0.#}/{max:0.#}";
        }
        if (op.Equals("max", StringComparison.OrdinalIgnoreCase))
        {
            SetMax(Mathf.Max(0f, amount));
            return $"AR max -> {max:0.#} (cur {Current:0.#})";
        }

        return "Usage: armor | armor +N | armor -N | armor set N | armor max N";

        string Apply(float d)
        {
            Refill(Mathf.Clamp(Current + d, 0f, max));
            return $"AR = {Current:0.#}/{max:0.#}";
        }
    }
}
