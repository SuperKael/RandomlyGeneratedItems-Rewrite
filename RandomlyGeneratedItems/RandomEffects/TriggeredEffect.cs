using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RandomlyGeneratedItems.RandomEffects
{
    public readonly struct TriggeredEffect
    {
        public static readonly Dictionary<string, TriggeredEffect> RegisteredTriggeredEffects = new();

        public readonly string Name;
        public readonly float StrengthModifier;
        public readonly Color[] SpriteColors;
        public readonly ItemTag[] ItemTags;
        public readonly Func<AbstractEffects, TriggeredEffectCallback> TriggeredEffectCallbackProvider;
        public readonly AbstractEffects.DescriptionDelegate DescriptionDelegate;
        public readonly int MinimumGrade;
        public readonly string[] ExclusiveConditions;
        
        public static IEnumerator Initialize()
        {
            string[] allTriggerTypes = EffectTriggerType.RegisteredTriggerTypes.Keys.ToArray();
            string[] attackTriggerTypes = { "Hit", "Crit" };
            // string[] infrequentTriggerTypes = { "Kill", "EliteKill", "Hurt", "Equipment", "Interact" };

            RegisterTriggeredEffect("PassiveBuff", 0.5f, new[] { Color.blue }, Array.Empty<ItemTag>(), effect =>
            {
                BuffDef buff = ScriptableObject.CreateInstance<BuffDef>();
                buff.name = "BUFF_PASSIVE_" + effect.Name;
                buff.canStack = effect is not EquipmentEffects && effect.Rng.nextBool;
                buff.isDebuff = false;
                buff.iconSprite = effect.Sprite;
                Buffs.RegisteredBuffs.Add(buff);
                if (buff.canStack) effect.TriggeredStrength /= 5;
                effect.ExtraText["BuffCanStack"] = buff.canStack ? ", and the effect <style=cIsDamage>can stack multiple times</style>." : ". This effect does <style=cDeath>not</style> stack.";

                PassiveEffect passiveEffect;
                do
                {
                    passiveEffect = PassiveEffect.RegisteredPassiveEffects.Values.ElementAt(effect.Rng.RangeInt(0, PassiveEffect.RegisteredPassiveEffects.Count));
                } while (passiveEffect.MinimumGrade > effect.Grade || passiveEffect.ExclusiveConditions.Contains("IsEquipment") && effect is EquipmentEffects);

                effect.TriggeredStrength *= passiveEffect.StrengthModifier;
                effect.SpriteColors = effect.SpriteColors.AddRangeToArray(passiveEffect.SpriteColors);
                if (effect is ItemEffects itemEffects && passiveEffect.ItemTags?.Length > 0) itemEffects.Item.tags.AddRangeToArray(passiveEffect.ItemTags);

                float passiveStrenthTemp = effect.PassiveStrength;
                float passiveStackScalingTemp = effect.PassiveStackScaling;
                try
                {
                    effect.PassiveStrength = effect.TriggeredStrength * passiveEffect.StrengthModifier;
                    effect.PassiveStackScaling = effect.TriggeredStackScaling;
                    effect.ExtraText["PassiveEffectBuff"] = passiveEffect.DescriptionDelegate(effect);
                }
                finally
                {
                    effect.PassiveStrength = passiveStrenthTemp;
                    effect.PassiveStackScaling = passiveStackScalingTemp;
                }

                PassiveEffect.PassiveEffectCallback passiveEffectCallback = passiveEffect.GetPassiveEffectCallback(effect);

                RecalculateStatsAPI.GetStatCoefficients += (sender, args) =>
                {
                    if (!sender || !sender.inventory || !NetworkServer.active || !sender.HasBuff(buff)) return;
                    int stackCount = effect.GetStackCount(sender);
                    int buffCount = sender.GetBuffCount(buff);

                    passiveStrenthTemp = effect.PassiveStrength;
                    passiveStackScalingTemp = effect.PassiveStackScaling;
                    try
                    {
                        effect.PassiveStrength = effect.TriggeredStrength * passiveEffect.StrengthModifier * buffCount;
                        effect.PassiveStackScaling = effect.TriggeredStackScaling;
                        passiveEffectCallback(args, stackCount, sender);
                    }
                    finally
                    {
                        effect.PassiveStrength = passiveStrenthTemp;
                        effect.PassiveStackScaling = passiveStackScalingTemp;
                    }
                };

                return (character, stacks, procCoefficient, _, _) =>
                {
                    character.AddTimedBuff(buff, Mathf.Pow(effect.TriggeredStrength, 2f / 3f) * (1 + effect.TriggeredStackScaling * (stacks - 1)) * procCoefficient);
                };
            }, effect =>
                    $"temporarily {char.ToLower(effect.ExtraText["PassiveEffectBuff"][0]) + effect.ExtraText["PassiveEffectBuff"][1..]} Effect lasts for <style=cIsUtility>{Mathf.Pow(effect.TriggeredStrength, 2f / 3f):0.#} seconds</style>{(effect.TriggeredStackScaling > 0 ? $" <style=cStack>(+{Mathf.Pow(effect.TriggeredStrength, 2f / 3f) * effect.TriggeredStackScaling:0.#} per stack)</style>" : "")}{effect.ExtraText["BuffCanStack"]}"
            , 2).BindTriggerTypes(allTriggerTypes);

            RegisterTriggeredEffect("FireEffectPayload", 50f, new[] { new Color(1.0f, 0.5f, 0.0f) }, new[] { ItemTag.Damage }, effect =>
            {
                SpawnableEffectPayload spawnableEffectPayload;
                do
                {
                    spawnableEffectPayload = SpawnableEffectPayload.RegisteredEffectPrefabs.Values.ElementAt(effect.Rng.RangeInt(0, SpawnableEffectPayload.RegisteredEffectPrefabs.Count));
                } while (spawnableEffectPayload.MinimumGrade > effect.Grade);

                effect.ExtraText["FiredEffectPayload"] = spawnableEffectPayload.DescriptionDelegate(effect);
                effect.TriggeredStrength *= spawnableEffectPayload.StrengthModifier;
                effect.ProcType = spawnableEffectPayload.ProcType;

                return (character, stacks, procCoefficient, procChainMask, args) =>
                {
                    spawnableEffectPayload.SpawnEffect(character, effect,
                        Util.GetCorePosition(character) + new Vector3(0, 1, 0), character.equipmentSlot.GetAimRay().direction,
                        stacks, procCoefficient, procChainMask, args);
                };
            }, effect =>
                    $"fire a {effect.ExtraText["FiredEffectPayload"]} for {effect.FormatTriggeredStrengthPercentage("IsDamage")} <style=cIsDamage>base damage</style>."
            ).BindTriggerTypes(allTriggerTypes);

            RegisterTriggeredEffect("SpawnEffectPayload", 25f, new[] { new Color(1.0f, 0.75f, 0.0f) }, new[] { ItemTag.Damage }, effect =>
            {
                SpawnableEffectPayload spawnableEffectPayload;
                do
                {
                    spawnableEffectPayload = SpawnableEffectPayload.RegisteredEffectPrefabs.Values.ElementAt(effect.Rng.RangeInt(0, SpawnableEffectPayload.RegisteredEffectPrefabs.Count));
                } while (spawnableEffectPayload.MinimumGrade > effect.Grade);

                effect.ExtraText["SpawnedEffectPayload"] = spawnableEffectPayload.DescriptionDelegate(effect);
                effect.TriggeredStrength *= spawnableEffectPayload.StrengthModifier;
                effect.ProcType = spawnableEffectPayload.ProcType;

                return (character, stacks, procCoefficient, procChainMask, args) =>
                {
                    DamageReport report = (DamageReport)args["damageReport"];

                    spawnableEffectPayload.SpawnEffect(character, effect,
                        Util.GetCorePosition(report.victimBody), Vector3.zero,
                        stacks, procCoefficient, procChainMask, args);
                };
            }, effect =>
                $"spawn a {effect.ExtraText["SpawnedEffectPayload"]} for {effect.FormatTriggeredStrengthPercentage("IsDamage")} <style=cIsDamage>base damage</style>."
            ).BindTriggerTypes(attackTriggerTypes);

            RegisterTriggeredEffect("ApplyBleed", 1f, new[] { Color.red }, new[] { ItemTag.Damage }, effect =>
                    (_, stacks, procCoefficient, _, args) =>
                    {
                        DamageReport report = (DamageReport)args["damageReport"];
                        float duration = Mathf.Pow(effect.TriggeredStrength, 2f / 3f) * (1 + effect.TriggeredStackScaling * (stacks - 1)) * procCoefficient;
                        InflictDotInfo dotInfo = new()
                        {
                            victimObject = report.victim.gameObject,
                            attackerObject = report.attacker.gameObject,
                            dotIndex = DotController.DotIndex.Bleed,
                            duration = duration,
                            totalDamage = report.damageInfo.damage,
                        };

                        DotController.InflictDot(ref dotInfo);
                    }, effect =>
                            $"<style=cDeath>bleed</style> a target for <style=cIsUtility>{Mathf.Pow(effect.TriggeredStrength, 2f / 3f):0.#} seconds</style>{(effect.TriggeredStackScaling > 0 ? $" <style=cStack>(+{Mathf.Pow(effect.TriggeredStrength, 2f / 3f) * effect.TriggeredStackScaling:0.#} per stack)</style>" : "")}."
            ).BindTriggerTypes(attackTriggerTypes);

            RegisterTriggeredEffect("Heal", 1f, new[] { Color.green }, new[] { ItemTag.Healing }, effect =>
                    (character, stacks, procCoefficient, procChainMask, _) =>
                    {
                        character.healthComponent.Heal(character.healthComponent.fullHealth * effect.GetTriggeredStrength(stacks, procCoefficient), procChainMask);
                    }, effect =>
                            $"receive <style=cIsHealing>healing</style> equal to {effect.FormatTriggeredStrengthPercentage("IsHealing")} of your maximum <style=cIsHealing>health</style>."
            , "AtFullHP", "HasShield").BindTriggerTypes(allTriggerTypes);

            RegisterTriggeredEffect("Barrier", 3f, new[] { Color.yellow }, new[] { ItemTag.Healing }, effect =>
            {
                bool noMax = effect.Rng.nextNormalizedFloat * 30 < effect.TriggeredStrength;
                if (noMax) effect.TriggeredStrength /= 3;
                effect.ExtraText["NoMaxBarrierText"] = noMax ? " Remove the maximum <style=cIsHealing>barrier</style> cap for 10 seconds." : "";
                return (character, stacks, procCoefficient, _, _) =>
                {
                    if (noMax)
                    {
                        character.AddTimedBuff(Buffs.NoMaxBarrier.BuffDef, 10);
                        // Extremely large number, but reduced from float.MaxValue a bit to try to prevent overflow if additional modifiers are applied after this
                        character.maxBarrier = float.MaxValue / 16;
                        character.barrierDecayRate = (character.maxHealth + character.maxShield) / 30;
                    }
                    character.healthComponent.AddBarrier(character.healthComponent.fullHealth *
                                                         effect.GetTriggeredStrength(stacks, procCoefficient));
                };
            }, effect =>
                            $"receive <style=cIsHealing>barrier</style> equal to {effect.FormatTriggeredStrengthPercentage("IsHealing")} of your maximum <style=cIsHealing>health</style>.{effect.ExtraText["NoMaxBarrierText"]}"
            ).BindTriggerTypes(allTriggerTypes);

            yield break;
        }

        public static TriggeredEffect RegisterTriggeredEffect(string name, float strengthModifier, Color[] spriteColors, ItemTag[] itemTags, Func<AbstractEffects, TriggeredEffectCallback> triggeredEffectCallbackProvider,
            AbstractEffects.DescriptionDelegate descriptionDelegate, params string[] exclusiveConditions)
        {
            return RegisterTriggeredEffect(name, strengthModifier, spriteColors, itemTags, triggeredEffectCallbackProvider,
                descriptionDelegate, 0, exclusiveConditions);
        }

        public static TriggeredEffect RegisterTriggeredEffect(string name, float strengthModifier, Color[] spriteColors, ItemTag[] itemTags, Func<AbstractEffects, TriggeredEffectCallback> triggeredEffectCallbackProvider,
            AbstractEffects.DescriptionDelegate descriptionDelegate, int minimumGrade, params string[] exclusiveConditions)
        {
            TriggeredEffect triggeredEffect =
                new(name, strengthModifier, spriteColors, itemTags, triggeredEffectCallbackProvider, descriptionDelegate, minimumGrade, exclusiveConditions);
            RegisteredTriggeredEffects[name] = triggeredEffect;
            return triggeredEffect;
        }

        public TriggeredEffect(string name, float strengthModifier, Color[] spriteColors, ItemTag[] itemTags, Func<AbstractEffects, TriggeredEffectCallback> triggeredEffectCallbackProvider, AbstractEffects.DescriptionDelegate descriptionDelegate, int minimumGrade, params string[] exclusiveConditions)
        {
            Name = name;
            StrengthModifier = strengthModifier;
            SpriteColors = spriteColors;
            ItemTags = itemTags;
            TriggeredEffectCallbackProvider = triggeredEffectCallbackProvider;
            DescriptionDelegate = descriptionDelegate;
            MinimumGrade = minimumGrade;
            ExclusiveConditions = exclusiveConditions;
        }

        public TriggeredEffect BindTriggerTypes(params string[] triggerTypes)
        {
            foreach (string triggerType in triggerTypes) AbstractEffects.TriggerTypeMap[triggerType].Add(Name);
            return this;
        }

        public TriggeredEffectCallback GetTriggeredEffectCallback(AbstractEffects effects)
        {
            return TriggeredEffectCallbackProvider(effects);
        }

        public delegate void TriggeredEffectCallback(CharacterBody character, int stackCount, float procCoefficient,
            ProcChainMask procChainMask, Dictionary<string, object> args);
    }
}