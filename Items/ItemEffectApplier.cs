using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obscurus.Items;

namespace Obscurus.Player
{
    /// Nenápadný runner pro coroutiny efektů.
    [DisallowMultipleComponent]
    public class EffectRunner : MonoBehaviour { }

    public static class ItemEffectApplier
    {
        const float EPS = 0.01f;

        /// NEW: vrátí true, pokud by aspoň jeden efekt měl dopad (hráč není „full“)
        public static bool WouldHaveAnyEffect(ItemDefinition def, GameObject target)
        {
            if (!def || def.consumable == null || target == null) return false;

            var hp = target.GetComponent<HealthSystem>();
            var st = target.GetComponent<StaminaSystem>();
            var ar = target.GetComponent<ArmorSystem>();

            var list = def.consumable.effects;
            if (list == null || list.Count == 0) return false;

            for (int i = 0; i < list.Count; i++)
            {
                var fx = list[i];
                switch (fx.type)
                {
                    case ItemEffectType.HealHP:
                    case ItemEffectType.RegenHPOverTime:
                        if (hp && hp.Current + EPS < hp.max) return true;
                        break;

                    case ItemEffectType.RestoreStamina:
                    case ItemEffectType.RegenStaminaOverTime:
                        if (st && st.Current + EPS < st.max) return true;
                        break;

                    case ItemEffectType.AddArmorFlat:
                        if (ar && ar.Current + EPS < ar.max) return true;
                        break;

                    case ItemEffectType.RestoreSanity:
                    case ItemEffectType.RegenSanityOverTime:
                        // přidej obdobu podle svého SanitySystemu
                        break;
                }
            }
            return false;
        }

        public static void ApplyAll(ItemDefinition def, GameObject target, HealSource healSrc = HealSource.Pickup)
        {
            if (!def || def.consumable == null) return;
            var list = def.consumable.effects;
            if (list == null || list.Count == 0) return;

            var runner = target.GetComponent<EffectRunner>() ?? target.AddComponent<EffectRunner>();
            var hp     = target.GetComponent<HealthSystem>();
            var st     = target.GetComponent<StaminaSystem>();
            var ar     = target.GetComponent<ArmorSystem>();
            var perks  = target.GetComponent<AlchemyPerks>(); // Lucky Sip hook je v HealthSystem.Heal

            foreach (var fx in list)
            {
                switch (fx.type)
                {
                    case ItemEffectType.HealHP:
                        if (hp) hp.Heal(fx.value, healSrc); // GoldenBlood/LuckySip se uplatní (není Regen)
                        break;

                    case ItemEffectType.RestoreStamina:
                        if (st) st.Refill(Mathf.Min(st.Current + fx.value, st.max));
                        break;

                    case ItemEffectType.AddArmorFlat:
                        if (ar)
                        {
                            if (fx.duration <= 0f)
                            {
                                ar.Refill(Mathf.Min(ar.Current + fx.value, ar.max));
                            }
                            else
                            {
                                // Dočasný štít – po době trvání odeber jen zbytek bonusu
                                runner.StartCoroutine(CoTempArmor(ar, fx.value, fx.duration));
                            }
                        }
                        break;

                    case ItemEffectType.RegenHPOverTime:
                        if (hp && fx.duration > 0f && fx.perSecond > 0f)
                            runner.StartCoroutine(CoRegenHP(hp, fx.perSecond, fx.duration));
                        break;

                    case ItemEffectType.RegenStaminaOverTime:
                        if (st && fx.duration > 0f && fx.perSecond > 0f)
                            runner.StartCoroutine(CoRegenStamina(st, fx.perSecond, fx.duration));
                        break;

                    case ItemEffectType.RestoreSanity:
                    case ItemEffectType.RegenSanityOverTime:
                        // Pokud máš SanitySystem, přidej analogii jako u HP/ST.
                        break;
                }
            }
        }
        
        static IEnumerator CoRegenHP(HealthSystem hp, float perSec, float dur)
        {
            float t = 0f;
            while (t < dur && hp)
            {
                hp.Heal(perSec * Time.deltaTime, HealSource.Regen); // Regen → bez GoldenBlood/LuckySip
                t += Time.deltaTime;
                yield return null;
            }
        }

        static IEnumerator CoRegenStamina(StaminaSystem st, float perSec, float dur)
        {
            float t = 0f;
            while (t < dur && st)
            {
                st.Refill(Mathf.Min(st.Current + perSec * Time.deltaTime, st.max));
                t += Time.deltaTime;
                yield return null;
            }
        }

        static IEnumerator CoTempArmor(ArmorSystem ar, float add, float dur)
        {
            if (!ar || add <= 0f) yield break;

            float before = ar.Current;
            ar.Refill(Mathf.Min(ar.Current + add, ar.max));

            float t = 0f;
            while (t < dur && ar) { t += Time.deltaTime; yield return null; }
            if (!ar) yield break;

            // odeber jen to, co z bonusu zbylo (necháme zranění respektovat gameplay)
            float stillAboveBaseline = Mathf.Max(0f, ar.Current - before);
            float toRemove = Mathf.Min(stillAboveBaseline, add);
            ar.Refill(Mathf.Max(0f, ar.Current - toRemove));
        }
    }
}
