using RoR2;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RandomlyGeneratedItems.RandomEffects
{
    public readonly struct EffectStatus
    {
        public static readonly Dictionary<string, EffectStatus> RegisteredEffectStatuses = new();

        public readonly string Name;
        public readonly float StrengthModifier;
        public readonly bool IsPositive;
        public readonly ProcType ProcType;
        public readonly StatusApplyDelegate ApplyDelegate;
        public readonly AbstractEffects.DescriptionDelegate DescriptionDelegate;
        public readonly int MinimumGrade;

        public static IEnumerator Initialize()
        {
            RegisterEffectStatus("Bleed", 1f, false, ProcType.BleedOnHit,
                (character, effects, target, stackCount, procCoefficient, procChainMask, args) =>
                {
                    DamageInfo damageInfo = args["damageInfo"] as DamageInfo;
                    effects.ExtraText["DOTIsTotalDamage"] = damageInfo != null ? "TOTAL" : "base";
                    float duration = Mathf.Pow(effects.TriggeredStrength, 2f / 3f) * (1 + effects.TriggeredStackScaling * (stackCount - 1)) * procCoefficient;
                    InflictDotInfo dotInfo = new()
                    {
                        victimObject = target.gameObject,
                        attackerObject = character.gameObject,
                        dotIndex = DotController.DotIndex.Bleed,
                        duration = duration,
                        totalDamage = damageInfo?.damage ?? character.damage
                    };

                    DotController.InflictDot(ref dotInfo);
                }, effects => $"<style=cDeath>Bleed</style> for {effects.FormatTriggeredStrengthPercentage("IsDamage")} {effects.ExtraText["DOTIsTotalDamage"]} damage");

            yield break;
        }

        public static EffectStatus RegisterEffectStatus(string name, float strengthModifier, bool isPositive,
            ProcType procType, StatusApplyDelegate applyDelegate, AbstractEffects.DescriptionDelegate descriptionDelegate,
            int minimumGrade = 0)
        {
            EffectStatus effectStatus = new(name, strengthModifier, isPositive,
                procType, applyDelegate, descriptionDelegate, minimumGrade);
            RegisteredEffectStatuses[name] = effectStatus;
            return effectStatus;
        }

        public EffectStatus(string name, float strengthModifier, bool isPositive, ProcType procType, StatusApplyDelegate applyDelegate, AbstractEffects.DescriptionDelegate descriptionDelegate, int minimumGrade)
        {
            Name = name;
            StrengthModifier = strengthModifier;
            IsPositive = isPositive;
            ProcType = procType;
            ApplyDelegate = applyDelegate;
            DescriptionDelegate = descriptionDelegate;
            MinimumGrade = minimumGrade;
        }

        public void ApplyEffect(CharacterBody character, AbstractEffects effects,
            CharacterBody target, int stackCount, float procCoefficient, ProcChainMask procChainMask, Dictionary<string, object> args)
        {
            ApplyDelegate(character, effects, target, stackCount, procCoefficient, procChainMask, args);
        }

        public delegate void StatusApplyDelegate(CharacterBody character, AbstractEffects effects,
            CharacterBody target, int stackCount, float procCoefficient, ProcChainMask procChainMask, Dictionary<string, object> args);
    }
}
