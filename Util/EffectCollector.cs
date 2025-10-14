using System.Collections.Generic;
using UnityEngine;
using Obscurus.AI;
using Obscurus.Effects;
using Obscurus.VFX;
using Obscurus.Items;   // DamageType
using Obscurus.Combat;  // DamageContext, TypedDamage
using System.Reflection;

[System.Serializable]
public class DebugEffectRow
{
    public string effect;
    public string type;
    public int    stacks;
    public float  tickDamage;
    public float  secondsLeft;
}

[DisallowMultipleComponent]
public class EffectCollector : MonoBehaviour
{
    [Header("Debug")]
    public bool debug = false;

    [Header("Socket (volitelné)")]
    public Transform socket;

    [Header("Globální ladění efektů")]
    [Tooltip("Globální multiplikátor pro VŠECHEN damage z efektů (DoT, AOE, statusy). 1 = bez změny.")]
    public float effectsDamageMultiplier = 1f;

    [Tooltip("Pouštět i legacy AOE dmg přímo z EffectCollectoru? (Nedoporučeno – ShockField si dmg řeší sám.)")]
    public bool applyLegacyDomeDamage = false;

    [Header("Last Hit debug")]
    [Tooltip("Jak dlouho zobrazit 'Last Hit' řádek v inspektoru.")]
    public float lastHitRowSeconds = 1.5f;

    EnemyStats _stats;

    // ===== AOE Dome tracking =====
    class DomeEntry
    {
        public ShockField dome;
        public float nextTick;
        public DamageType type = DamageType.Lightning; // čistě pro debug řádek
    }
    readonly Dictionary<ShockField, DomeEntry> _domesAffectingMe = new();

    // ===== Electrized status =====
    class ElectrizedRuntime
    {
        public ElectrizedEffectDef def;
        public int stacks;
        public float expireAt;
        public float nextTick;
        public GameObject source;
        public PooledVFX vfx;
        public Transform attach;
        public DamageType damageType = DamageType.Lightning;
        public float lastTickDamage;
    }
    ElectrizedRuntime _electrized;

    // ===== Last direct hit (jen info, bez stackování) =====
    DamageType _lastHitType = DamageType.Physical;
    float _lastHitShowUntil = -1f;

    [SerializeField] List<DebugEffectRow> activeEffects = new();

    Transform Socket
    {
        get
        {
            if (!socket)
            {
                var t = transform.Find("EffectSocket");
                socket = t ? t : transform;
            }
            return socket ? socket : transform;
        }
    }

    void Awake()
    {
        _stats = GetComponent<EnemyStats>() ??
                 GetComponentInParent<EnemyStats>() ??
                 GetComponentInChildren<EnemyStats>();

        if (!_stats)
            Debug.LogWarning($"[EffectCollector:{name}] ⚠️ EnemyStats nenalezen!", this);
    }

    void OnValidate()
    {
        if (effectsDamageMultiplier < 0f) effectsDamageMultiplier = 0f;
        if (lastHitRowSeconds < 0.1f) lastHitRowSeconds = 0.1f;
    }

    void Update()
    {
        TickDomes();
        TickElectrized();

#if UNITY_EDITOR
        if (debug) RefreshInspectorRows();
#endif
    }

    // =====================================================================
    // AOE DOMES
    // =====================================================================

    public void NotifyEnterDome(ShockField dome)
    {
        if (!dome) return;
        if (_domesAffectingMe.ContainsKey(dome)) return;

        var e = new DomeEntry
        {
            dome = dome,
            nextTick = Time.time + Mathf.Max(0.05f, dome.tickInterval),
            type = TryGetDomeDamageType(dome, DamageType.Lightning)
        };
        _domesAffectingMe.Add(dome, e);
    }

    public void NotifyExitDome(ShockField dome)
    {
        if (!dome) return;
        _domesAffectingMe.Remove(dome);
    }

    void TickDomes()
    {
        if (_domesAffectingMe.Count == 0 || _stats == null || _stats.IsDead) return;

        var toRemove = ListPool<ShockField>.Get();
        try
        {
            foreach (var kv in _domesAffectingMe)
            {
                var e = kv.Value;
                var dome = e.dome;
                if (!dome || !dome.IsAlive) { toRemove.Add(kv.Key); continue; }

                if (Time.time >= e.nextTick)
                {
                    e.nextTick += Mathf.Max(0.05f, dome.tickInterval);

                    // A) Legacy AOE dmg (většinou nechceš) – NESTACKUJE nic:
                    if (applyLegacyDomeDamage && dome.aoeDamage > 0f)
                    {
                        float amt = dome.aoeDamage * effectsDamageMultiplier;
                        if (amt > 0f)
                        {
                            GameObject src = TryGetDomeSource(dome);
                            ApplyEffectDamageTyped(amt, e.type, src);
                        }
                    }

                    // B) DoT stack z AOE pouze když to tak def chce:
                    //    WhileInsideAoe = stackovat; OnHit = NESTACKOVAT z AOE.
                    if (dome.electrizedDef && dome.stacksPerTick > 0 &&
                        dome.electrizedDef.gainMode == StackGainMode.WhileInsideAoe)
                    {
                        var src = TryGetDomeSource(dome);
                        ApplyElectrized(dome.electrizedDef, dome.stacksPerTick, src);
                    }
                }
            }
        }
        finally
        {
            for (int i = 0; i < toRemove.Count; i++)
                _domesAffectingMe.Remove(toRemove[i]);
            ListPool<ShockField>.Release(toRemove);
        }
    }

    // =====================================================================
    // ELECTRIZED (OnHit / AOE)
    // =====================================================================

    /// <summary>
    /// Aplikuj/stackuj Electrized. Voláš buď z útoku (OnHit), nebo to volá TickDomes při WhileInsideAoe.
    /// Útoky tím pádem NESTACKUJÍ, dokud je v definici gainMode=OnHit a ty je sem vědomě neposíláš.
    /// </summary>
    public void ApplyElectrized(ElectrizedEffectDef def, int addStacks, GameObject source, Transform attachSocket = null)
    {
        if (!def || !_stats || _stats.IsDead) return;

        if (_electrized == null)
        {
            _electrized = new ElectrizedRuntime
            {
                def      = def,
                stacks   = 0,
                expireAt = 0f,
                nextTick = Time.time + Mathf.Max(0.05f, def.tickInterval),
                source   = source,
                attach   = attachSocket ? attachSocket : Socket,
                damageType = def.damageType
            };

            if (def.onTargetVFX && _electrized.attach)
                _electrized.vfx = VFXPool.SpawnLoop(def.onTargetVFX, _electrized.attach.position, _electrized.attach.rotation, _electrized.attach);
        }

        _electrized.def = def;
        _electrized.source = source ? source : _electrized.source;
        _electrized.damageType = def.damageType;

        _electrized.stacks = Mathf.Clamp(_electrized.stacks + Mathf.Max(1, addStacks), 1, Mathf.Max(1, def.maxStacks));

        if (def.refreshDurationOnStack)
            _electrized.expireAt = Time.time + Mathf.Max(0.1f, def.duration);
        else if (_electrized.expireAt <= Time.time) // první nastavení
            _electrized.expireAt = Time.time + Mathf.Max(0.1f, def.duration);
    }

    void TickElectrized()
    {
        if (_electrized == null) return;

        // Expire?
        if (Time.time >= _electrized.expireAt || !_stats || _stats.IsDead)
        {
            if (_electrized.vfx) VFXPool.Release(_electrized.vfx);
            _electrized = null;
            return;
        }

        if (Time.time >= _electrized.nextTick)
        {
            _electrized.nextTick += Mathf.Max(0.05f, _electrized.def.tickInterval);

            float baseDmg = ComputeElectrizedTickDamage(_electrized.def, _electrized.stacks);
            float finalDmg = baseDmg * effectsDamageMultiplier;
            _electrized.lastTickDamage = finalDmg;

            if (finalDmg > 0f)
                ApplyEffectDamageTyped(finalDmg, _electrized.damageType, _electrized.source);
        }
    }

    static float ComputeElectrizedTickDamage(ElectrizedEffectDef def, int stacks)
    {
        stacks = Mathf.Max(0, stacks);
        switch (def.damageMode)
        {
            case StackDamageMode.AddPerStack:
                return stacks * Mathf.Max(0f, def.damagePerStack);
            case StackDamageMode.MultiplyFromBase:
                return Mathf.Max(0f, def.baseTickDamage) * Mathf.Max(0f, 1f + stacks * def.multPerStack);
        }
        return 0f;
    }

    // =====================================================================
    // Last Hit (jen info o typu – bez stacků)
    // =====================================================================

    /// <summary>Volat z místa, kde se aplikuje přímý hit (útok). Jen si uloží typ pro debug.</summary>
    public void NotifyDirectHit(in DamageContext ctx)
    {
        _lastHitType = ctx.primary;
        _lastHitShowUntil = Time.time + lastHitRowSeconds;
    }

    // =====================================================================
    // Helpers: typed apply + reflexe z ShockFieldu
    // =====================================================================

    void ApplyEffectDamageTyped(float amount, DamageType type, GameObject source)
    {
        if (amount <= 0f || _stats == null || _stats.IsDead) return;

        var anyCol = GetComponentInChildren<Collider>();
        var ctx = new DamageContext
        {
            amount  = amount,
            primary = type,
            source  = source
        };

        if (anyCol)
            TypedDamage.Apply(anyCol, in ctx, Socket.position, Vector3.up, false);
        else
            _stats.ApplyDamage(amount, Socket.position, Vector3.up, source);
    }

    static GameObject TryGetDomeSource(ShockField dome)
    {
        if (!dome) return null;

        var prop = dome.GetType().GetProperty("sourceOwner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && typeof(GameObject).IsAssignableFrom(prop.PropertyType))
        { try { return (GameObject)prop.GetValue(dome); } catch { } }

        var fld = dome.GetType().GetField("sourceOwner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fld != null && typeof(GameObject).IsAssignableFrom(fld.FieldType))
        { try { return (GameObject)fld.GetValue(dome); } catch { } }

        return null;
    }

    static DamageType TryGetDomeDamageType(ShockField dome, DamageType fallback)
    {
        if (!dome) return fallback;
        var fld = dome.GetType().GetField("_baseCtx", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fld != null && fld.FieldType == typeof(DamageContext))
        {
            try { var ctx = (DamageContext)fld.GetValue(dome); return ctx.primary; }
            catch { }
        }
        return fallback;
    }

    void OnDisable()
    {
        _domesAffectingMe.Clear();

        if (_electrized != null && _electrized.vfx)
            VFXPool.Release(_electrized.vfx);
        _electrized = null;
    }

#if UNITY_EDITOR
    void RefreshInspectorRows()
    {
        if (activeEffects == null) return;
        activeEffects.Clear();

        // AOE Dome (jen info)
        foreach (var kv in _domesAffectingMe)
        {
            if (kv.Key)
            {
                activeEffects.Add(new DebugEffectRow
                {
                    effect = "AOE Dome",
                    type = kv.Value.type.ToString(),
                    stacks = 0,
                    tickDamage = 0f,
                    secondsLeft = kv.Key.SecondsLeft
                });
            }
        }

        // Electrized DoT
        if (_electrized != null)
        {
            float secs = Mathf.Max(0f, _electrized.expireAt - Time.time);
            activeEffects.Add(new DebugEffectRow
            {
                effect = "Electrized",
                type = _electrized.damageType.ToString(),
                stacks = _electrized.stacks,
                tickDamage = _electrized.lastTickDamage,
                secondsLeft = secs
            });
        }

        // Last direct hit (jen typ, bez stacků)
        if (Time.time < _lastHitShowUntil)
        {
            activeEffects.Add(new DebugEffectRow
            {
                effect = "Last Hit",
                type = _lastHitType.ToString(),
                stacks = 0,
                tickDamage = 0f,
                secondsLeft = Mathf.Max(0f, _lastHitShowUntil - Time.time)
            });
        }
    }
#else
    void RefreshInspectorRows() { }
#endif
}

static class ListPool<T>
{
    static readonly Stack<List<T>> pool = new Stack<List<T>>(8);
    public static List<T> Get() => pool.Count > 0 ? pool.Pop() : new List<T>(8);
    public static void Release(List<T> list) { list.Clear(); pool.Push(list); }
}
