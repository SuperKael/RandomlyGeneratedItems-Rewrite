using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using R2API;
using RoR2;
using UnityEngine;

namespace RandomlyGeneratedItems.RandomEffects
{
    public readonly struct PassiveEffect
    {
        public static readonly Dictionary<string, PassiveEffect> RegisteredPassiveEffects = new();
        public static readonly Dictionary<string, PassiveEffect> RegisteredPassiveLunarEffects = new();

        public readonly string Name;
        public readonly float StrengthModifier;
        public readonly Color[] SpriteColors;
        public readonly ItemTag[] ItemTags;
        public readonly Func<AbstractEffects, PassiveEffectCallback> PassiveEffectCallbackProvider;
        public readonly Func<AbstractEffects, PassiveSpecialStatCallback> PassiveSpecialStatCallbackProvider;
        public readonly AbstractEffects.DescriptionDelegate DescriptionDelegate;
        public readonly int MinimumGrade;
        public readonly bool IsLunar;
        public readonly string[] ExclusiveConditions;

        public static IEnumerator Initialize()
        {
            RegisterPassiveEffect("AttackSpeedBoost", 10f, new[] { new Color(1.0f, 0.5f, 0.0f) }, new[] { ItemTag.Damage }, effect => (args, stacks, _) => 
                    args.baseAttackSpeedAdd = Math.Max(args.baseAttackSpeedAdd + effect.GetPassiveStrength(stacks), Math.Min(args.baseAttackSpeedAdd, 0)), effect => 
                    $"Increases <style=cIsDamage>attack speed</style> by {effect.FormatPassiveStrengthPercentage("IsDamage")}.",
                0, false, "OutOfCombat");

            RegisterPassiveEffect("SpeedBoost", 10f, new[] { new Color(0.75f, 0.75f, 0.75f) }, new[] { ItemTag.Utility }, effect => (args, stacks, _) => 
                    args.moveSpeedMultAdd = Math.Max(args.moveSpeedMultAdd + effect.GetPassiveStrength(stacks), Math.Min(args.moveSpeedMultAdd, 0)), effect => 
                    $"Gain {effect.FormatPassiveStrengthPercentage("IsUtility")} <style=cIsUtility>movement speed</style>.",
                0, false, "NotMoving");

            RegisterPassiveEffect("JumpBoost", 5f, new[] { new Color(0.75f, 0.75f, 0.5f) }, new[] { ItemTag.Utility }, effect => (args, stacks, _) => 
                    args.jumpPowerMultAdd += effect.GetPassiveStrength(stacks), effect => 
                    $"Gain {effect.FormatPassiveStrengthPercentage("IsUtility")} <style=cIsUtility>jump height</style>.",
                0, false, "NotMoving", "Midair");

            RegisterPassiveEffect("MaxJumps", 0.25f, new[] { new Color(0.5f, 0.5f, 1.0f) }, new[] { ItemTag.Utility }, _ => NoEffectCallback, effect => 
                    (stat, value, stacks, _) => stat == "MaxJumpCount" ? value + Math.Sign(effect.PassiveStrength) * (int)Math.Ceiling(Math.Abs(effect.PassiveStrength)) + Math.Sign(effect.PassiveStrength * effect.PassiveStackScaling) * (int)Math.Ceiling(Math.Abs(effect.PassiveStrength * effect.PassiveStackScaling)) * (stacks - 1) : value, effect => 
                    $"Gain <style=cIsUtility>{(int)Math.Ceiling(effect.PassiveStrength):+0;-#}</style>" + (effect.PassiveStackScaling > 0 ? $"<style=cStack>({(int)Math.Ceiling(effect.PassiveStrength * effect.PassiveStackScaling):+0;-#} per stack)</style>" : "") + " maximum <style=cIsUtility>jump count</style>.",
                2, false, "NotMoving", "Midair");

            RegisterPassiveEffect("HealthBoost", 5f, new[] { new Color(0.5f, 1.0f, 0.5f) }, new[] { ItemTag.Healing }, effect => (args, stacks, _) => 
                    args.healthMultAdd += effect.GetPassiveStrength(stacks), effect => 
                    $"Gain {effect.FormatPassiveStrengthPercentage("IsHealing")} <style=cIsHealing>maximum health</style>.",
                0, false, "HasShield", "HasBarrier", "AtFullHP");

            RegisterPassiveEffect("DamageBoost", 5f, new[] { new Color(1.0f, 0.5f, 0.5f) }, new[] { ItemTag.Damage }, effect => (args, stacks, _) => 
                    args.damageMultAdd += effect.GetPassiveStrength(stacks), effect => 
                    $"Increases <style=cIsDamage>base damage</style> by {effect.FormatPassiveStrengthPercentage("IsDamage")}."
                );

            RegisterPassiveEffect("ShieldBoost", 5f, new[] { new Color(0.0f, 0.5f, 1.0f) }, new[] { ItemTag.Healing }, effect => (args, stacks, body) => 
                    args.baseShieldAdd += body.healthComponent.fullHealth * effect.GetPassiveStrength(stacks), effect => 
                    $"Gain <style=cIsHealing>shield</style> equal to {effect.FormatPassiveStrengthPercentage("IsHealing")} of your maximum health.",
                0, false, "HasBarrier", "AtFullHP");

            RegisterPassiveEffect("ArmorBoost", 10f, new[] { new Color(0.25f, 1.0f, 0.25f) }, new[] { ItemTag.Healing }, effect => (args, stacks, _) => 
                    args.armorAdd += effect.GetPassiveStrength(stacks) * 100, effect => 
                    $"Gain <style=cIsHealing>{effect.PassiveStrength:0}</style> <style=cStack>(+{effect.PassiveStrength * effect.PassiveStackScaling:0} per stack)</style> <style=cIsHealing>armor</style>."
                );

            RegisterPassiveEffect("RegenBoost", 10f, new[] { new Color(0.75f, 1.0f, 0.75f) }, new[] { ItemTag.Healing }, effect => (args, stacks, _) => 
                    args.regenMultAdd += effect.GetPassiveStrength(stacks), effect => 
                    $"Increases <style=cIsHealing>base health regeneration</style> by {effect.FormatPassiveStrengthPercentage("IsHealing")}.",
                0, false, "HasShield", "HasBarrier", "AtFullHP");

            RegisterPassiveEffect("CritChanceBoost", 5f, new[] { new Color(1.0f, 0.25f, 0.0f) }, new[] { ItemTag.Damage }, effect => (args, stacks, _) => 
                    args.critAdd += effect.GetPassiveStrength(stacks) * 100, effect => 
                    $"Gain {effect.FormatPassiveStrengthPercentage("IsDamage")} <style=cIsDamage>critical chance</style>."
                );

            RegisterPassiveEffect("CritDamageBoost", 8f, new[] { new Color(1.0f, 0.0f, 0.25f) }, new[] { ItemTag.Damage }, effect => (args, stacks, _) => 
                    args.critDamageMultAdd += effect.GetPassiveStrength(stacks), effect => 
                    $"Gain {effect.FormatPassiveStrengthPercentage("IsDamage")} <style=cIsDamage>critical damage</style>."
                );

            RegisterPassiveEffect("SecondaryCooldownBoost", 3f, new[] { new Color(0.25f, 0.0f, 1.0f) }, new[] { ItemTag.Utility }, effect => (args, stacks, _) => 
                    args.secondaryCooldownMultAdd -= effect.GetPassiveStrength(stacks), effect => 
                    $"Reduces <style=cIsUtility>secondary skill cooldown</style> by {effect.FormatPassiveStrengthPercentage("IsUtility")}.",
                2);

            RegisterPassiveEffect("UtilityCooldownBoost", 3f, new[] { new Color(0.0f, 0.25f, 1.0f) }, new[] { ItemTag.Utility }, effect => (args, stacks, _) => 
                    args.utilityCooldownMultAdd -= effect.GetPassiveStrength(stacks), effect => 
                    $"Reduces <style=cIsUtility>utility skill cooldown</style> by {effect.FormatPassiveStrengthPercentage("IsUtility")}.",
                2);

            RegisterPassiveEffect("SpecialCooldownBoost", 3f, new[] { new Color(0.25f, 0.25f, 1.0f) }, new[] { ItemTag.Utility }, effect => (args, stacks, _) => 
                    args.specialCooldownMultAdd -= effect.GetPassiveStrength(stacks), effect => 
                    $"Reduces <style=cIsUtility>special skill cooldown</style> by {effect.FormatPassiveStrengthPercentage("IsUtility")}.",
                2);

            RegisterPassiveEffect("EquipCooldownBoost", 3f, new[] { new Color(0.25f, 0.75f, 1.0f) }, new[] { ItemTag.Utility }, _ => NoEffectCallback, effect => 
                    (stat, value, stacks, _) => stat == "EquipmentCooldownScale" ? value * Math.Max(1 - effect.GetPassiveStrength(stacks), 0) : value, effect => 
                    $"Reduces <style=cIsUtility>equipment cooldown</style> by {effect.FormatPassiveStrengthPercentage("IsUtility")}.",
                2, false, "IsEquipment");

            RegisterPassiveEffect("AllCooldownBoost", 1.5f, new[] { new Color(0.75f, 0.75f, 1.0f) }, new[] { ItemTag.Utility }, effect => (args, stacks, _) => 
                    args.cooldownMultAdd -= effect.GetPassiveStrength(stacks), effect => 
                    $"Reduces <style=cIsUtility>all skill cooldowns</style> by {effect.FormatPassiveStrengthPercentage("IsUtility")}.", 
                3);

            // Negative passive lunar effects

            RegisterPassiveEffect("NegativePassive", 2f, new[] { RandomContentPackProvider.TierColors[ItemTier.Lunar] }, new ItemTag[] { }, effect =>
                {
                    if (!RegisteredPassiveEffects.Values.Any(passiveEffect =>
                            passiveEffect.MinimumGrade <= effect.Grade && (!passiveEffect.ExclusiveConditions.Contains("IsEquipment") || effect is not EquipmentEffects)))
                    {
                        effect.ExtraText["NegativePassiveEffectDesc"] = "Lose nothing, because you disabled too many passive effects.";
                        return (_, _, _) => { };
                    }

                    PassiveEffect passiveEffect;
                    do
                    {
                        passiveEffect = RegisteredPassiveEffects.Values.ElementAt(effect.Rng.RangeInt(0, RegisteredPassiveEffects.Count));
                    } while (passiveEffect.MinimumGrade > effect.Grade || passiveEffect.ExclusiveConditions.Contains("IsEquipment") && effect is EquipmentEffects);

                    effect.ExtraText["NegativePassiveEffect"] = passiveEffect.Name;
                    effect.LunarStrength *= passiveEffect.StrengthModifier / 4f;
                    effect.SpriteColors = effect.SpriteColors.AddRangeToArray(passiveEffect.SpriteColors);
                    if (effect is ItemEffects itemEffects && passiveEffect.ItemTags?.Length > 0) itemEffects.Item.tags.AddRangeToArray(passiveEffect.ItemTags);

                    PassiveEffectCallback passiveEffectCallback;

                    float passiveStrengthTemp = effect.PassiveStrength;
                    float passiveStackScalingTemp = effect.PassiveStackScaling;
                    try
                    {
                        effect.PassiveStrength = effect.LunarStrength;
                        effect.PassiveStackScaling = effect.LunarStackScaling;
                        passiveEffectCallback = passiveEffect.GetPassiveEffectCallback(effect);
                        string passiveEffectDesc = passiveEffect.DescriptionDelegate(effect);
                        effect.ExtraText["NegativePassiveEffectDesc"] = passiveEffectDesc.Replace("Gain ", "Lose ").Replace("Increases ", "Decreases ").Replace("Reduces ", "Increases ");
                    }
                    finally
                    {
                        effect.PassiveStrength = passiveStrengthTemp;
                        effect.PassiveStackScaling = passiveStackScalingTemp;
                    }

                    return (args, stacks, body) =>
                    {
                        passiveStrengthTemp = effect.PassiveStrength;
                        passiveStackScalingTemp = effect.PassiveStackScaling;
                        try
                        {
                            effect.PassiveStrength = -effect.LunarStrength;
                            effect.PassiveStackScaling = effect.LunarStackScaling;
                            passiveEffectCallback(args, stacks, body);
                        }
                        finally
                        {
                            effect.PassiveStrength = passiveStrengthTemp;
                            effect.PassiveStackScaling = passiveStackScalingTemp;
                        }
                    };
                }, effect => {
                    PassiveSpecialStatCallback passiveSpecialStatCallback = RegisteredPassiveEffects[effect.ExtraText["NegativePassiveEffect"]].GetPassiveSpecialStatCallback(effect);

                    return (stat, value, stacks, body) =>
                    {
                        float passiveStrengthTemp = effect.PassiveStrength;
                        float passiveStackScalingTemp = effect.PassiveStackScaling;
                        try
                        {
                            effect.PassiveStrength = -effect.LunarStrength;
                            effect.PassiveStackScaling = effect.LunarStackScaling;
                            value = passiveSpecialStatCallback(stat, value, stacks, body);
                        }
                        finally
                        {
                            effect.PassiveStrength = passiveStrengthTemp;
                            effect.PassiveStackScaling = passiveStackScalingTemp;
                        }
                        return value;
                    };
                }, effect => 
                    effect.ExtraText["NegativePassiveEffectDesc"],
                0, true);

            RegisterPassiveEffect("FluctuatingStats", 4f, new[] { RandomContentPackProvider.TierColors[ItemTier.Lunar] }, new ItemTag[] { ItemTag.BrotherBlacklist }, effect =>
                {
                    effect.LunarStrength /= 8f;

                    Xoroshiro128Plus rng = new Xoroshiro128Plus(effect.Rng);

                    return (args, stacks, body) =>
                    {
                        float strength = effect.GetLunarStrength(stacks);

                        args.healthMultAdd            = (1f + args.healthMultAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1f;
                        args.regenMultAdd             = (1f + args.regenMultAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1f;
                        args.moveSpeedMultAdd         = (1f + args.moveSpeedMultAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1f;
                        args.jumpPowerMultAdd         = (1f + args.jumpPowerMultAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1f;
                        args.damageMultAdd            = (1f + args.damageMultAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1f;
                        args.attackSpeedMultAdd       = (1f + args.attackSpeedMultAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1f;
                        args.critAdd                  = (body.baseCrit + args.critAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - body.baseCrit;
                        args.armorAdd                 = (body.baseArmor + args.armorAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - body.baseArmor;
                        args.cooldownMultAdd          = (1f + args.cooldownMultAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1f;
                        args.primaryCooldownMultAdd   = (1f + args.primaryCooldownMultAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1f;
                        args.secondaryCooldownMultAdd = (1f + args.secondaryCooldownMultAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1f;
                        args.utilityCooldownMultAdd   = (1f + args.utilityCooldownMultAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1f;
                        args.specialCooldownMultAdd   = (1f + args.specialCooldownMultAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1f;
                        args.shieldMultAdd            = (1f + args.shieldMultAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1f;
                        args.levelMultAdd             = (1f + args.levelMultAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1f;
                        args.critDamageMultAdd        = (1f + args.critDamageMultAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1f;
                        args.sprintSpeedAdd           = (1.45f + args.sprintSpeedAdd) * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f) - 1.45f;
                    };
                }, effect => {
                    Xoroshiro128Plus rng = new Xoroshiro128Plus(effect.Rng);

                    return (stat, value, stacks, body) =>
                    {
                        float strength = effect.GetLunarStrength(stacks);

                        return value * Mathf.Max(rng.RangeFloat(1f - strength, 1f + strength), 0.2f);
                    };
                }, effect =>
                    $"<style=cDeath>ALL</style> of your stats constantly <style=cIsVoid>fluctuate</style> by up to {effect.FormatPassiveStrengthPercentage("IsUtility")} above or below what they should be " +
                    $"<style=cStack>(stats will never be reduced to less than 20%)</style>.",
                0, true);

            yield break;
        }

        public static PassiveEffect? RegisterPassiveEffect(string name, float strengthModifier, Color[] spriteColors, ItemTag[] itemTags, Func<AbstractEffects, PassiveEffectCallback> passiveEffectCallbackProvider,
            AbstractEffects.DescriptionDelegate descriptionDelegate, int minimumGrade = 0, bool isLunar = false, params string[] exclusiveConditions)
        {
            return RegisterPassiveEffect(name, strengthModifier, spriteColors, itemTags, passiveEffectCallbackProvider, _ => NoSpecialStatCallback, descriptionDelegate, minimumGrade, isLunar, exclusiveConditions);
        }

        public static PassiveEffect? RegisterPassiveEffect(string name, float strengthModifier, Color[] spriteColors, ItemTag[] itemTags, Func<AbstractEffects, PassiveEffectCallback> passiveEffectCallbackProvider, Func<AbstractEffects, PassiveSpecialStatCallback> passiveSpecialStatCallbackProvider,
            AbstractEffects.DescriptionDelegate descriptionDelegate, int minimumGrade = 0, bool isLunar = false, params string[] exclusiveConditions)
        {
            if (!Main.RgiConfig.Bind(isLunar ? "Passive Lunar Effect Toggles" : "Passive Effect Toggles", name, true, $"Controls whether the {(isLunar ? "passive lunar" : "passive")} effect '{name}' appears on randomly generated items.").Value) return null;
            PassiveEffect passiveEffect =
                new(name, strengthModifier, spriteColors, itemTags, passiveEffectCallbackProvider, passiveSpecialStatCallbackProvider, descriptionDelegate, minimumGrade, isLunar, exclusiveConditions);
            if (isLunar)
                RegisteredPassiveLunarEffects[name] = passiveEffect;
            else
                RegisteredPassiveEffects[name] = passiveEffect;
            return passiveEffect;
        }

        public PassiveEffect(string name, float strengthModifier, Color[] spriteColors, ItemTag[] itemTags, Func<AbstractEffects, PassiveEffectCallback> passiveEffectCallbackProvider, Func<AbstractEffects, PassiveSpecialStatCallback> passiveSpecialStatCallbackProvider, AbstractEffects.DescriptionDelegate descriptionDelegate, int minimumGrade, bool isLunar, params string[] exclusiveConditions)
        {
            Name = name;
            StrengthModifier = strengthModifier;
            SpriteColors = spriteColors;
            ItemTags = itemTags;
            PassiveEffectCallbackProvider = passiveEffectCallbackProvider;
            PassiveSpecialStatCallbackProvider = passiveSpecialStatCallbackProvider;
            DescriptionDelegate = descriptionDelegate;
            MinimumGrade = minimumGrade;
            IsLunar = isLunar;
            ExclusiveConditions = exclusiveConditions;
        }

        public PassiveEffectCallback GetPassiveEffectCallback(AbstractEffects effects)
        {
            return PassiveEffectCallbackProvider?.Invoke(effects);
        }

        public PassiveSpecialStatCallback GetPassiveSpecialStatCallback(AbstractEffects effects)
        {
            return PassiveSpecialStatCallbackProvider?.Invoke(effects);
        }

        private static void NoEffectCallback(RecalculateStatsAPI.StatHookEventArgs args, int stacks, CharacterBody body) {}

        private static float NoSpecialStatCallback(string stat, float value, int stacks, CharacterBody body) => value;

        public delegate void PassiveEffectCallback(RecalculateStatsAPI.StatHookEventArgs args, int stacks, CharacterBody body);

        public delegate float PassiveSpecialStatCallback(string stat, float value, int stacks, CharacterBody body);
    }
}