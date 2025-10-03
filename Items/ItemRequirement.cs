#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System;
using UnityEngine;
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

namespace Obscurus.Items
{
    /// <summary>
    /// Polymorfní podmínky pro použití itemu – držíme je přes SerializeReference.
    /// Odin pak nabídne neat picker v inspektoru / editor okně.
    /// </summary>
    [Serializable]
    public abstract class ItemRequirement
    {
#if HAS_ODIN
        [GUIColor(0.8f,0.9f,1f)]
        [ShowInInspector, ReadOnly, LabelText("Req")]
#endif
        public virtual string Summary => GetType().Name;

        /// <summary> Sem si tvoje hra dosadí implementaci checku. </summary>
        public abstract bool IsSatisfied(IRequirementContext ctx);
    }

    public interface IRequirementContext
    {
        int GetLevel();
        float GetStat(string key);
        bool HasFlag(string flag);
    }

    [Serializable]
    public sealed class LevelRequirement : ItemRequirement
    {
        public int minLevel = 1;

        public override string Summary => $"Level ≥ {minLevel}";
        public override bool IsSatisfied(IRequirementContext ctx) => ctx != null && ctx.GetLevel() >= minLevel;
    }

    [Serializable]
    public sealed class StatRequirement : ItemRequirement
    {
        public string statKey = "Strength";
        public float minValue = 10f;

        public override string Summary => $"{statKey} ≥ {minValue}";
        public override bool IsSatisfied(IRequirementContext ctx) => ctx != null && ctx.GetStat(statKey) >= minValue;
    }

    [Serializable]
    public sealed class FlagRequirement : ItemRequirement
    {
        public string flag = "Quest/Chapter2/Started";
        public override string Summary => $"Has '{flag}'";
        public override bool IsSatisfied(IRequirementContext ctx) => ctx != null && ctx.HasFlag(flag);
    }
}