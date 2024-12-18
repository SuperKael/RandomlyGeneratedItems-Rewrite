using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TextCore;

namespace RandomlyGeneratedItems.RandomEffects
{
    public readonly struct TriggeredEffect
    {
        public static readonly Dictionary<string, TriggeredEffect> RegisteredTriggeredEffects = new();
        public static readonly Dictionary<string, TriggeredEffect> RegisteredEquipmentEffects = new();

        public readonly string Name;
        public readonly float StrengthModifier;
        public readonly float CooldownModifier;
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

            RegisterTriggeredEffect("PassiveBuff", 0.5f, 1f, new[] { Color.blue }, Array.Empty<ItemTag>(), effect =>
            {
                BuffDef buff = ScriptableObject.CreateInstance<BuffDef>();
                buff.name = "BUFF_PASSIVE_" + effect.Name;
                buff.canStack = effect is not EquipmentEffects && effect.Rng.nextBool;
                buff.isHidden = false;
                buff.isDebuff = false;
                effect.OnFinalizeGeneration += () =>
                {
                    buff.iconSprite = effect.Sprite;
                };
                Buffs.RegisteredBuffs.Add(buff);
                if (buff.canStack) effect.TriggeredStrength /= 5;
                effect.ExtraText["BuffCanStack"] = buff.canStack ? ", and the effect <style=cIsDamage>can stack multiple times</style>." : ". This effect does <style=cDeath>not</style> stack.";

                if (!PassiveEffect.RegisteredPassiveEffects.Values.Any(passiveEffect => 
                        passiveEffect.MinimumGrade <= effect.Grade && (!passiveEffect.ExclusiveConditions.Contains("IsEquipment") || effect is not EquipmentEffects)))
                {
                    effect.ExtraText["PassiveEffectBuff"] = "Gain nothing, because you disabled too many effect passive effects.";
                    return (_, _, _, _, _) => { };
                }

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
                PassiveEffect.PassiveSpecialStatCallback passiveSpecialStatCallback = passiveEffect.GetPassiveSpecialStatCallback(effect);

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

                AbstractEffects.OnPassiveSpecialStatUpdated += (sender, stat, value) =>
                {
                    if (!sender || !sender.inventory || !NetworkServer.active || !sender.HasBuff(buff)) return value;
                    int stackCount = effect.GetStackCount(sender);
                    int buffCount = sender.GetBuffCount(buff);

                    passiveStrenthTemp = effect.PassiveStrength;
                    passiveStackScalingTemp = effect.PassiveStackScaling;
                    try
                    {
                        effect.PassiveStrength = effect.TriggeredStrength * passiveEffect.StrengthModifier * buffCount;
                        effect.PassiveStackScaling = effect.TriggeredStackScaling;
                        value = passiveSpecialStatCallback(stat, value, stackCount, sender);
                    }
                    finally
                    {
                        effect.PassiveStrength = passiveStrenthTemp;
                        effect.PassiveStackScaling = passiveStackScalingTemp;
                    }

                    return value;
                };

                return (character, stacks, procCoefficient, _, _) =>
                {
                    character.AddTimedBuff(buff, Mathf.Pow(effect.TriggeredStrength, 2f / 3f) * (1 + effect.TriggeredStackScaling * (stacks - 1)) * procCoefficient);
                };
            }, effect =>
                    $"temporarily {char.ToLower(effect.ExtraText["PassiveEffectBuff"][0]) + effect.ExtraText["PassiveEffectBuff"][1..]} Effect lasts for <style=cIsUtility>{Mathf.Pow(effect.TriggeredStrength, 2f / 3f):0.#} seconds</style>{(effect.TriggeredStackScaling > 0 ? $" <style=cStack>(+{Mathf.Pow(effect.TriggeredStrength, 2f / 3f) * effect.TriggeredStackScaling:0.#} per stack)</style>" : "")}{effect.ExtraText["BuffCanStack"]}"
            , 2)?.BindTriggerTypes(allTriggerTypes);

            RegisterTriggeredEffect("FireEffectPayload", 50f, 1f, new[] { new Color(1.0f, 0.5f, 0.0f) }, new[] { ItemTag.Damage }, effect =>
            {
                if (!SpawnableEffectPayload.RegisteredEffectPrefabs.Values.Any(effectPrefab => effectPrefab.MinimumGrade <= effect.Grade))
                {
                    effect.ExtraText["SpawnedEffectPayload"] = "nothing, because you disabled too many effect payloads,";
                    return (_, _, _, _, _) => { };
                }

                SpawnableEffectPayload spawnableEffectPayload;
                do
                {
                    spawnableEffectPayload = SpawnableEffectPayload.RegisteredEffectPrefabs.Values.ElementAt(effect.Rng.RangeInt(0, SpawnableEffectPayload.RegisteredEffectPrefabs.Count));
                } while (spawnableEffectPayload.MinimumGrade > effect.Grade);

                effect.ExtraText["FiredEffectPayload"] = spawnableEffectPayload.DescriptionDelegate(effect);
                effect.TriggeredStrength *= spawnableEffectPayload.StrengthModifier;
                effect.ProcType = spawnableEffectPayload.ProcType;

                if (effect is EquipmentEffects equipmentEffect)
                    equipmentEffect.Equipment.cooldown *= spawnableEffectPayload.CooldownModifier;

                return (character, stacks, procCoefficient, procChainMask, args) =>
                {
                    spawnableEffectPayload.SpawnEffect(character, effect,
                        Util.GetCorePosition(character) + new Vector3(0, 1, 0), character.equipmentSlot.GetAimRay().direction,
                        stacks, procCoefficient, procChainMask, args);
                };
            }, effect =>
                    $"fire a {effect.ExtraText["FiredEffectPayload"]} for {effect.FormatTriggeredStrengthPercentage("IsDamage")} <style=cIsDamage>base damage</style>."
            )?.BindTriggerTypes(allTriggerTypes);

            RegisterTriggeredEffect("SpawnEffectPayload", 25f, 1f, new[] { new Color(1.0f, 0.75f, 0.0f) }, new[] { ItemTag.Damage }, effect =>
            {
                if (!SpawnableEffectPayload.RegisteredEffectPrefabs.Values.Any(effectPrefab => effectPrefab.MinimumGrade <= effect.Grade))
                {
                    effect.ExtraText["SpawnedEffectPayload"] = "nothing, because you disabled too many effect payloads,";
                    return (_, _, _, _, _) => { };
                }

                SpawnableEffectPayload spawnableEffectPayload;
                do
                {
                    spawnableEffectPayload = SpawnableEffectPayload.RegisteredEffectPrefabs.Values.ElementAt(effect.Rng.RangeInt(0, SpawnableEffectPayload.RegisteredEffectPrefabs.Count));
                } while (spawnableEffectPayload.MinimumGrade > effect.Grade);

                effect.ExtraText["SpawnedEffectPayload"] = spawnableEffectPayload.DescriptionDelegate(effect);
                effect.TriggeredStrength *= spawnableEffectPayload.StrengthModifier;
                effect.ProcType = spawnableEffectPayload.ProcType;

                if (effect is EquipmentEffects equipmentEffect)
                    equipmentEffect.Equipment.cooldown *= spawnableEffectPayload.CooldownModifier;

                return (character, stacks, procCoefficient, procChainMask, args) =>
                {
                    DamageReport report = args.TryGetValue("damageReport", out object reportObj) ? reportObj as DamageReport : null;
                    CharacterBody victimBody = report?.victimBody ?? character;

                    spawnableEffectPayload.SpawnEffect(character, effect,
                        Util.GetCorePosition(victimBody), Vector3.zero,
                        stacks, procCoefficient, procChainMask, args);
                };
            }, effect =>
                $"spawn a {effect.ExtraText["SpawnedEffectPayload"]} for {effect.FormatTriggeredStrengthPercentage("IsDamage")} <style=cIsDamage>base damage</style>."
            )?.BindTriggerTypes(attackTriggerTypes);

            RegisterTriggeredEffect("InflictStatus", 1f, 1f, new[] { Color.red }, new[] { ItemTag.Damage }, effect =>
            {
                IEnumerable<EffectStatus> validStatuses =
                    EffectStatus.RegisteredEffectStatuses.Values.Where(status => !status.IsPositive);

                EffectStatus[] effectStatusEnumerable = validStatuses.ToArray();
                if (!effectStatusEnumerable.Any())
                {
                    effect.ExtraText["StatusDescription"] = "nothing, because you disabled all of the negative status effects";
                    return (_, _, _, _, _) => { };
                }

                EffectStatus status = effectStatusEnumerable.ToList()[effect.Rng.RangeInt(0, effectStatusEnumerable.Length)];
                effect.ExtraText["StatusDescription"] = status.DescriptionDelegate(effect);
                effect.TriggeredStrength *= status.StrengthModifier;
                effect.ProcType = status.ProcType;

                return (_, stacks, procCoefficient, procChainMask, args) =>
                {
                    DamageReport report = args.TryGetValue("damageReport", out object reportObj) ? reportObj as DamageReport : null;
                    if (report == null) return;

                    float duration = Mathf.Pow(effect.TriggeredStrength, 2f / 3f) *
                                     (1 + effect.TriggeredStackScaling * (stacks - 1)) * procCoefficient;
                    status.ApplyEffect(report.attackerBody, effect, report.victimBody, stacks, duration, procCoefficient, procChainMask, args);
                };
            }, effect =>
                            $"inflict {effect.ExtraText["StatusDescription"]}, lasting <style=cIsUtility>{Mathf.Pow(effect.TriggeredStrength, 2f / 3f):0.#} seconds</style>{(effect.TriggeredStackScaling > 0 ? $" <style=cStack>(+{Mathf.Pow(effect.TriggeredStrength, 2f / 3f) * effect.TriggeredStackScaling:0.#} per stack)</style>" : "")}."
            )?.BindTriggerTypes(attackTriggerTypes);

            RegisterTriggeredEffect("Heal", 1f, 1f, new[] { Color.green }, new[] { ItemTag.Healing }, effect =>
            {
                effect.ProcType = ProcType.HealOnHit;

                return (character, stacks, procCoefficient, procChainMask, _) =>
                {
                    character.healthComponent.Heal(
                        character.healthComponent.fullHealth *
                        effect.GetTriggeredStrength(stacks, procCoefficient), procChainMask);
                };
            }, effect =>
                            $"receive <style=cIsHealing>healing</style> equal to {effect.FormatTriggeredStrengthPercentage("IsHealing")} of your maximum <style=cIsHealing>health</style>."
            , "AtFullHP", "HasShield")?.BindTriggerTypes(allTriggerTypes);

            RegisterTriggeredEffect("Barrier", 3f, 1f, new[] { Color.yellow }, new[] { ItemTag.Healing }, effect =>
            {
                bool noMax = Buffs.NoMaxBarrier.BuffDef != null && effect.Rng.nextNormalizedFloat * 30 < effect.TriggeredStrength;
                if (noMax) effect.TriggeredStrength /= 3;
                effect.ExtraText["NoMaxBarrierText"] = noMax ? " and remove the maximum <style=cIsHealing>barrier</style> cap for 10 seconds." : ".";
                return (character, stacks, procCoefficient, _, _) =>
                {
                    if (noMax)
                    {
                        character.AddTimedBuff(Buffs.NoMaxBarrier.BuffDef, 10);
                        // Extremely large number, but reduced from float.MaxValue a bit to try to prevent overflow if additional modifiers are applied after this
                        character.maxBarrier = float.MaxValue / 16;
                    }
                    character.healthComponent.AddBarrier(character.healthComponent.fullHealth *
                                                         effect.GetTriggeredStrength(stacks, procCoefficient));
                };
            }, effect =>
                            $"receive <style=cIsHealing>barrier</style> equal to {effect.FormatTriggeredStrengthPercentage("IsHealing")} of your maximum <style=cIsHealing>health</style>{effect.ExtraText["NoMaxBarrierText"]}"
            )?.BindTriggerTypes(allTriggerTypes);

            RegisterEquipmentEffect("SpawnInteractable", 1f, 2f, new[] { Color.magenta }, effect =>
            {
                if (!SpawnableInteractable.RegisteredInteractables.Values.Any(interactablePrefab => interactablePrefab.MinimumGrade <= effect.Grade))
                {
                    effect.ExtraText["SpawnedInteractable"] = "nothing, because you disabled too many interactables";
                    effect.ExtraText["InteractableCost"] = "";
                    return (_, _) => { };
                }

                SpawnableInteractable spawnableInteractable;
                do
                {
                    spawnableInteractable = SpawnableInteractable.RegisteredInteractables.Values.ElementAt(effect.Rng.RangeInt(0, SpawnableInteractable.RegisteredInteractables.Count));
                } while (spawnableInteractable.MinimumGrade > effect.Grade);

                effect.ExtraText["SpawnedInteractable"] = spawnableInteractable.DescriptionDelegate(effect);
                if (spawnableInteractable.CostModifier >= 2f)
                {
                    effect.ExtraText["InteractableCost"] = $" It costs {Mathf.RoundToInt(spawnableInteractable.CostModifier)}x the normal amount.";
                }
                else if (spawnableInteractable.CostModifier > 1f)
                {
                    effect.ExtraText["InteractableCost"] = $" It costs {(spawnableInteractable.CostModifier - 1) * 100:0.##}% more than normal.";
                }
                else if (spawnableInteractable.CostModifier < 1f)
                {
                    effect.ExtraText["InteractableCost"] = $" It costs {-(spawnableInteractable.CostModifier - 1) * 100:0.##}% less than normal.";
                }
                else
                {
                    effect.ExtraText["InteractableCost"] = "";
                }
                effect.Equipment.cooldown *= spawnableInteractable.CooldownModifier;

                return (character, _) =>
                {
                    spawnableInteractable.SpawnInteractable(character, effect, Util.GetCorePosition(character));
                };
            }, effect => 
                    $"spawn {effect.ExtraText["SpawnedInteractable"]}.{effect.ExtraText["InteractableCost"]}"
            );

            RegisterEquipmentEffect("BypassConditions", 1f, 2f, new[] { Color.cyan }, effect =>
            {
                return (character, _) =>
                {
                    character.AddTimedBuff(Buffs.BypassEffectConditions.BuffDef, Mathf.Pow(effect.TriggeredStrength, 2f / 3f) * 2);
                };
            }, effect =>
                    $"bypass <style=cShrine>ALL</style> restrictive conditions on <style=cArtifact>Randomly Generated Items</style> for <style=cIsUtility>{Mathf.Pow(effect.TriggeredStrength, 2f / 3f) * 2:0.#} seconds</style>."
            );

            RegisterEquipmentEffect("ForceTriggerAll", 3.125f, 2f, new[] { Color.white }, effect =>
            {
                return (character, args) =>
                {
                    if (args.ContainsKey("forceTrigger")) return;
                    foreach (string triggerType in EffectTriggerType.RegisteredTriggerTypes.Keys)
                    {
                        AbstractEffects.TriggerEffects(triggerType, character, effect.TriggeredStrength / 100, null, new Dictionary<string, object>()
                        {
                            ["forceTrigger"] = true
                        });
                    }
                };
            }, effect =>
                    $"immediately activate the triggered effects of <style=cShrine>ALL</style> of your <style=cArtifact>Randomly Generated Items</style> at {effect.FormatTriggeredStrengthPercentage("IsUtility")} of their normal strength. <style=cStack>(Does not apply to items that only apply their effects to a hit target)</style>"
            );

            yield break;
        }

        public static TriggeredEffect? RegisterTriggeredEffect(string name, float strengthModifier, float cooldownModifier, Color[] spriteColors, ItemTag[] itemTags, Func<AbstractEffects, TriggeredEffectCallback> triggeredEffectCallbackProvider,
            AbstractEffects.DescriptionDelegate descriptionDelegate, params string[] exclusiveConditions)
        {
            return RegisterTriggeredEffect(name, strengthModifier, cooldownModifier, spriteColors, itemTags, triggeredEffectCallbackProvider,
                descriptionDelegate, 0, exclusiveConditions);
        }

        public static TriggeredEffect? RegisterTriggeredEffect(string name, float strengthModifier, float cooldownModifier, Color[] spriteColors, ItemTag[] itemTags, Func<AbstractEffects, TriggeredEffectCallback> triggeredEffectCallbackProvider,
            AbstractEffects.DescriptionDelegate descriptionDelegate, int minimumGrade, params string[] exclusiveConditions)
        {
            if (!Main.RgiConfig.Bind("Triggered Effect Toggles", name, true, $"Controls whether the triggered effect '{name}' appears on randomly generated items.").Value) return null;
            TriggeredEffect triggeredEffect =
                new(name, strengthModifier, cooldownModifier, spriteColors, itemTags, triggeredEffectCallbackProvider, descriptionDelegate, minimumGrade, exclusiveConditions);
            RegisteredTriggeredEffects[name] = triggeredEffect;
            return triggeredEffect;
        }

        public static TriggeredEffect? RegisterEquipmentEffect(string name, float strengthModifier, float cooldownModifier, Color[] spriteColors, Func<EquipmentEffects, EquipmentEffectCallback> equipmentEffectCallbackProvider,
            AbstractEffects.DescriptionDelegate descriptionDelegate, params string[] exclusiveConditions)
        {
            return RegisterEquipmentEffect(name, strengthModifier, cooldownModifier, spriteColors, equipmentEffectCallbackProvider,
                descriptionDelegate, 0, exclusiveConditions);
        }

        public static TriggeredEffect? RegisterEquipmentEffect(string name, float strengthModifier, float cooldownModifier, Color[] spriteColors, Func<EquipmentEffects, EquipmentEffectCallback> equipmentEffectCallbackProvider,
            AbstractEffects.DescriptionDelegate descriptionDelegate, int minimumGrade, params string[] exclusiveConditions)
        {
            if (!Main.RgiConfig.Bind("Equipment Effect Toggles", name, true, $"Controls whether the triggered effect '{name}' appears on randomly generated equipment.").Value) return null;
            TriggeredEffect triggeredEffect = new(name, strengthModifier, cooldownModifier, spriteColors, Array.Empty<ItemTag>(), effect =>
                {
                    EquipmentEffectCallback effectCallback = equipmentEffectCallbackProvider(effect as EquipmentEffects);
                    return (character, _, _, _, args) => effectCallback(character, args);
                }, descriptionDelegate, minimumGrade, exclusiveConditions);
            RegisteredEquipmentEffects[name] = triggeredEffect;
            return triggeredEffect;
        }

        public TriggeredEffect(string name, float strengthModifier, float cooldownModifier, Color[] spriteColors, ItemTag[] itemTags, Func<AbstractEffects, TriggeredEffectCallback> triggeredEffectCallbackProvider, AbstractEffects.DescriptionDelegate descriptionDelegate, int minimumGrade, params string[] exclusiveConditions)
        {
            Name = name;
            StrengthModifier = strengthModifier;
            CooldownModifier = cooldownModifier;
            SpriteColors = spriteColors;
            ItemTags = itemTags;
            TriggeredEffectCallbackProvider = triggeredEffectCallbackProvider;
            DescriptionDelegate = descriptionDelegate;
            MinimumGrade = minimumGrade;
            ExclusiveConditions = exclusiveConditions;
        }

        public TriggeredEffect BindTriggerTypes(params string[] triggerTypes)
        {
            foreach (string triggerType in triggerTypes) if (AbstractEffects.TriggerTypeMap.TryGetValue(triggerType, out List<string> typeList)) typeList.Add(Name);
            return this;
        }

        public TriggeredEffectCallback GetTriggeredEffectCallback(AbstractEffects effects)
        {
            return TriggeredEffectCallbackProvider(effects);
        }

        public delegate void TriggeredEffectCallback(CharacterBody character, int stackCount, float procCoefficient,
            ProcChainMask procChainMask, Dictionary<string, object> args);

        public delegate void EquipmentEffectCallback(CharacterBody character, Dictionary<string, object> args);
    }
}