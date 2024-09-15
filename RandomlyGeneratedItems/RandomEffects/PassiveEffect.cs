using System;
using System.Collections;
using System.Collections.Generic;
using R2API;
using RoR2;
using UnityEngine;

namespace RandomlyGeneratedItems.RandomEffects
{
    public readonly struct PassiveEffect
    {
        public static readonly Dictionary<string, PassiveEffect> RegisteredPassiveEffects = new();

        public readonly string Name;
        public readonly float StrengthModifier;
        public readonly Color[] SpriteColors;
        public readonly ItemTag[] ItemTags;
        public readonly Func<AbstractEffects, PassiveEffectCallback> PassiveEffectCallbackProvider;
        public readonly AbstractEffects.DescriptionDelegate DescriptionDelegate;
        public readonly int MinimumGrade;
        public readonly string[] ExclusiveConditions;

        public static IEnumerator Initialize()
        {
            RegisterPassiveEffect("AttackSpeedBoost", 10f, new[] { new Color(1.0f, 0.5f, 0.0f) }, new[] { ItemTag.Damage }, effect => (args, stacks, _) =>
                args.baseAttackSpeedAdd += effect.GetPassiveStrength(stacks), effect =>
                $"Increase <style=cIsDamage>attack speed</style> by {effect.FormatPassiveStrengthPercentage("IsDamage")}.",
                "OutOfCombat");

            RegisterPassiveEffect("SpeedBoost", 10f, new[] { new Color(0.75f, 0.75f, 1.0f) }, new[] { ItemTag.Utility }, effect => (args, stacks, _) =>
                args.moveSpeedMultAdd += effect.GetPassiveStrength(stacks), effect =>
                $"Gain {effect.FormatPassiveStrengthPercentage("IsUtility")} <style=cIsUtility>movement speed</style>.");

            RegisterPassiveEffect("HealthBoost", 5f, new[] { new Color(0.5f, 1.0f, 0.5f) }, new[] { ItemTag.Healing }, effect => (args, stacks, _) =>
                args.healthMultAdd += effect.GetPassiveStrength(stacks), effect =>
                $"Gain {effect.FormatPassiveStrengthPercentage("IsHealing")} <style=cIsHealing>maximum health</style>.",
                "HasShield", "HasBarrier", "AtFullHP");

            RegisterPassiveEffect("DamageBoost", 5f, new[] { new Color(1.0f, 0.5f, 0.5f) }, new[] { ItemTag.Damage }, effect => (args, stacks, _) =>
                args.damageMultAdd += effect.GetPassiveStrength(stacks), effect =>
                $"Increase <style=cIsDamage>base damage</style> by {effect.FormatPassiveStrengthPercentage("IsDamage")}.");

            RegisterPassiveEffect("ShieldBoost", 5f, new[] { new Color(0.0f, 0.5f, 1.0f) }, new[] { ItemTag.Healing }, effect => (args, stacks, body) =>
                args.baseShieldAdd += body.healthComponent.fullHealth * effect.GetPassiveStrength(stacks), effect =>
                $"Gain a <style=cIsHealing>shield</style> equal to {effect.FormatPassiveStrengthPercentage("IsHealing")} of your maximum health.",
                "HasBarrier", "AtFullHP");

            RegisterPassiveEffect("ArmorBoost", 10f, new[] { new Color(0.25f, 1.0f, 0.25f) }, new[] { ItemTag.Healing }, effect => (args, stacks, _) =>
                args.armorAdd += effect.GetPassiveStrength(stacks), effect =>
                $"Gain <style=cIsHealing>{effect.PassiveStrength:0.#}</style> <style=cStack>(+{effect.PassiveStrength * effect.PassiveStackScaling:0.#} per stack)</style> <style=cIsHealing>armor</style>.");

            RegisterPassiveEffect("RegenBoost", 10f, new[] { new Color(0.75f, 1.0f, 0.75f) }, new[] { ItemTag.Healing }, effect => (args, stacks, _) =>
                args.regenMultAdd += effect.GetPassiveStrength(stacks), effect =>
                $"Increase <style=cIsHealing>base health regeneration</style> by {effect.FormatPassiveStrengthPercentage("IsHealing")}.",
                "HasShield", "HasBarrier", "AtFullHP");

            RegisterPassiveEffect("CritChanceBoost", 8f, new[] { new Color(1.0f, 0.25f, 0.0f) }, new[] { ItemTag.Damage }, effect => (args, stacks, _) =>
                args.critAdd += effect.GetPassiveStrength(stacks) * 100, effect =>
                $"Gain {effect.FormatPassiveStrengthPercentage("IsDamage")} <style=cIsDamage>critical chance</style>.");

            RegisterPassiveEffect("CritDamageBoost", 8f, new[] { new Color(1.0f, 0.0f, 0.25f) }, new[] { ItemTag.Damage }, effect => (args, stacks, _) =>
                args.critDamageMultAdd += effect.GetPassiveStrength(stacks), effect =>
                $"Gain {effect.FormatPassiveStrengthPercentage("IsDamage")} <style=cIsDamage>critical damage</style>.");

            RegisterPassiveEffect("SecondaryCooldownBoost", 3f, new[] { new Color(0.25f, 0.0f, 1.0f) }, new[] { ItemTag.Utility }, effect => (args, stacks, _) =>
                args.secondaryCooldownMultAdd -= effect.GetPassiveStrength(stacks), effect =>
                $"Reduce <style=cIsUtility>secondary skill cooldown</style> by {effect.FormatPassiveStrengthPercentage("IsUtility")}.",
                2);

            RegisterPassiveEffect("UtilityCooldownBoost", 3f, new[] { new Color(0.0f, 0.25f, 1.0f) }, new[] { ItemTag.Utility }, effect => (args, stacks, _) =>
                args.utilityCooldownMultAdd -= effect.GetPassiveStrength(stacks), effect =>
                $"Reduce <style=cIsUtility>utility skill cooldown</style> by {effect.FormatPassiveStrengthPercentage("IsUtility")}.",
                2);

            RegisterPassiveEffect("SpecialCooldownBoost", 3f, new[] { new Color(0.25f, 0.25f, 1.0f) }, new[] { ItemTag.Utility }, effect => (args, stacks, _) =>
                args.specialCooldownMultAdd -= effect.GetPassiveStrength(stacks), effect =>
                $"Reduce <style=cIsUtility>special skill cooldown</style> by {effect.FormatPassiveStrengthPercentage("IsUtility")}.",
                2);

            RegisterPassiveEffect("EquipCooldownBoost", 3f, new[] { new Color(0.75f, 0.25f, 1.0f) }, new[] { ItemTag.Utility }, effect => (args, stacks, _) =>
                args.specialCooldownMultAdd -= effect.GetPassiveStrength(stacks), effect =>
                $"Reduce <style=cIsUtility>equipment cooldown</style> by {effect.FormatPassiveStrengthPercentage("IsUtility")}.",
                2, "IsEquipment");

            RegisterPassiveEffect("AllCooldownBoost", 1.5f, new[] { new Color(0.75f, 0.75f, 1.0f) }, new[] { ItemTag.Utility }, effect => (args, stacks, _) =>
                args.cooldownMultAdd -= effect.GetPassiveStrength(stacks), effect =>
                $"Reduce <style=cIsUtility>all cooldowns</style> by {effect.FormatPassiveStrengthPercentage("IsUtility")}.",
                3);

            yield break;
        }

        public static PassiveEffect RegisterPassiveEffect(string name, float strengthModifier, Color[] spriteColors, ItemTag[] itemTags, Func<AbstractEffects, PassiveEffectCallback> statEffectCallbackProvider,
            AbstractEffects.DescriptionDelegate descriptionDelegate, params string[] exclusiveConditions)
        {
            return RegisterPassiveEffect(name, strengthModifier, spriteColors, itemTags, statEffectCallbackProvider,
                descriptionDelegate, 0, exclusiveConditions);
        }

        public static PassiveEffect RegisterPassiveEffect(string name, float strengthModifier, Color[] spriteColors, ItemTag[] itemTags, Func<AbstractEffects, PassiveEffectCallback> statEffectCallbackProvider,
            AbstractEffects.DescriptionDelegate descriptionDelegate, int minimumGrade, params string[] exclusiveConditions)
        {
            PassiveEffect passiveEffect =
                new(name, strengthModifier, spriteColors, itemTags, statEffectCallbackProvider, descriptionDelegate, minimumGrade, exclusiveConditions);
            RegisteredPassiveEffects[name] = passiveEffect;
            return passiveEffect;
        }

        public PassiveEffect(string name, float strengthModifier, Color[] spriteColors, ItemTag[] itemTags, Func<AbstractEffects, PassiveEffectCallback> passiveEffectCallbackProvider, AbstractEffects.DescriptionDelegate descriptionDelegate, int minimumGrade, params string[] exclusiveConditions)
        {
            Name = name;
            StrengthModifier = strengthModifier;
            SpriteColors = spriteColors;
            ItemTags = itemTags;
            PassiveEffectCallbackProvider = passiveEffectCallbackProvider;
            DescriptionDelegate = descriptionDelegate;
            MinimumGrade = minimumGrade;
            ExclusiveConditions = exclusiveConditions;
        }

        public PassiveEffectCallback GetPassiveEffectCallback(AbstractEffects effects)
        {
            return PassiveEffectCallbackProvider(effects);
        }

        public delegate void PassiveEffectCallback(RecalculateStatsAPI.StatHookEventArgs args, int stacks, CharacterBody body);
    }
}