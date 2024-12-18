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
            RegisterEffectStatus("Bleed", 5f, false, ProcType.BleedOnHit,
                (character, _, target, _, duration, _, _, args) =>
                {
                    DamageInfo damageInfo = args["damageInfo"] as DamageInfo;
                    InflictDotInfo dotInfo = new()
                    {
                        victimObject = target.gameObject,
                        attackerObject = character.gameObject,
                        dotIndex = DotController.DotIndex.Bleed,
                        duration = duration,
                        totalDamage = damageInfo?.damage ?? character.damage
                    };

                    DotController.InflictDot(ref dotInfo);
                }, effects => $"<style=cDeath>bleed</style> for {effects.FormatTriggeredStrengthPercentage("IsDamage")} damage");

            yield break;
        }

        public static EffectStatus? RegisterEffectStatus(string name, float strengthModifier, bool isPositive,
            ProcType procType, StatusApplyDelegate applyDelegate, AbstractEffects.DescriptionDelegate descriptionDelegate,
            int minimumGrade = 0)
        {
            if (!Main.RgiConfig.Bind("Status Effect Toggles", name, true, $"Controls whether the status effect '{name}' can be applied by randomly generated items.").Value) return null;
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
            CharacterBody target, int stackCount, float duration, float procCoefficient, ProcChainMask procChainMask, Dictionary<string, object> args)
        {
            ApplyDelegate(character, effects, target, stackCount, duration, procCoefficient, procChainMask, args);
        }

        public delegate void StatusApplyDelegate(CharacterBody character, AbstractEffects effects,
            CharacterBody target, int stackCount, float duration, float procCoefficient, ProcChainMask procChainMask, Dictionary<string, object> args);
    }
}
