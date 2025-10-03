#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System;
using UnityEngine;
using Obscurus.Console;            // << přidáno kvůli [ConsoleCommand] + IConsoleProvider
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

#if HAS_ODIN
[HideMonoScript]
[InfoBox("Stamina: průběžná drenáž při sprintu, jednorázové náklady pro akce. Po utrácení čeká regenDelay, pak regeneruje.")]
#endif
[DisallowMultipleComponent]
public class StaminaSystem : MonoBehaviour, IConsoleProvider   // << IMPLEMENTACE IConsoleProvider
{
    [Header("Pool")]
    [Min(1)] public float baseMax = 100f;
    [Min(1f)] public float max = 100f;
    [SerializeField, Min(0f)] float current = 100f;

    [Header("Regen")]
    [Tooltip("Rychlost regenerace za sekundu (po uplynutí regenDelay od poslední spotřeby).")]
    public float regenPerSecond = 18f;
    [Tooltip("Prodleva po poslední spotřebě, než se spustí regenerace.")]
    public float regenDelay = 0.9f;

    [Header("Sprint")]
    [Tooltip("Kolik stojí sprint za sekundu.")]
    public float sprintCostPerSecond = 20f;
    [Tooltip("Minimální stamina, aby šel sprint ZAHÁJIT (pod ní se sprint ani nezačne).")]
    public float minSprintToStart = 10f;

    [Header("Akce: jednorázové náklady")]
    public float jumpCost        = 12f;
    public float lightAttackCost = 12f;
    public float heavyAttackCost = 22f;

#if HAS_ODIN
    [FoldoutGroup("Debug"), ReadOnly, ShowInInspector,
     ProgressBar(0, "max", ColorMember = "@current<=minSprintToStart? \"#E07A5F\": \"#3CB371\"")]
#endif
    public float Current => current;
    public float Normalized => Mathf.Approximately(max, 0f) ? 0f : Mathf.Clamp01(current / max);

    public event Action<float, float> OnChanged;  // (cur, max)
    public event Action OnDepleted;

    float _regenTimer;

    void Awake()
    {
        current = Mathf.Clamp(current, 0f, max);
        RaiseChanged();
    }

    void Update()
    {
        if (_regenTimer > 0f) _regenTimer -= Time.unscaledDeltaTime;
        else if (current < max && regenPerSecond > 0f)
        {
            var old = current;
            current = Mathf.Min(max, current + regenPerSecond * Time.deltaTime);
            if (!Mathf.Approximately(old, current)) RaiseChanged();
        }
    }
    
    public void ResetToBase()
    {
        max = baseMax;
        current = max;
        RaiseChanged();
    }


    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (current + 1e-4f < amount) return false;
        Spend(amount);
        return true;
    }

    public void Spend(float amount)
    {
        if (amount <= 0f) return;
        var old = current;
        current = Mathf.Max(0f, current - amount);
        _regenTimer = regenDelay;
        if (!Mathf.Approximately(old, current)) RaiseChanged();
        if (current <= 0f) OnDepleted?.Invoke();
    }

    public bool ConsumeForSprint(float deltaTime, bool startingNow)
    {
        if (deltaTime <= 0f || sprintCostPerSecond <= 0f)
            return true;

        // Pokud sprint právě ZAHÁJÍM, vyžaduji aspoň minSprintToStart
        if (startingNow && current < minSprintToStart)
            return false;

        // Pokud stamina došla, sprint skončí
        if (current <= 0f)
            return false;

        // Jinak stamina klesá až na nulu
        float cost = sprintCostPerSecond * deltaTime;
        Spend(cost);

        return current > 0f;
    }


    public void Refill(float toFull = -1f)
    {
        var target = (toFull > 0f) ? toFull : max;
        var old = current;
        current = Mathf.Clamp(target, 0f, max);
        RaiseChanged();
        if (!Mathf.Approximately(old, current)) _regenTimer = 0f;
    }

    public void SetMax(float newMax, bool keepRatio = true)
    {
        newMax = Mathf.Max(1f, newMax);
        if (keepRatio)
            current = Mathf.Clamp01(current / Mathf.Max(1e-4f, max)) * newMax;
        else
            current = Mathf.Min(current, newMax);
        max = newMax;
        RaiseChanged();
    }

    void RaiseChanged() => OnChanged?.Invoke(current, max);

    // ===== Console commands (instance) =====

    [ConsoleCommand("stam", "Stamina: bez param = zobraz, +N/-N, 'set N', 'max N'.")]
    public string CmdStamina(string op = null, float amount = 0f)
    {
        if (string.IsNullOrWhiteSpace(op))
            return $"ST = {Current:0.#}/{max:0.#}";

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
            return $"ST set -> {Current:0.#}/{max:0.#}";
        }
        if (op.Equals("max", StringComparison.OrdinalIgnoreCase))
        {
            SetMax(Mathf.Max(1f, amount));
            return $"ST max -> {max:0.#} (cur {Current:0.#})";
        }

        return "Usage: stam | stam +N | stam -N | stam set N | stam max N";

        string Apply(float d)
        {
            if (d >= 0) Refill(Current + d);
            else        Spend(-d);
            return $"ST = {Current:0.#}/{max:0.#}";
        }
    }
}
