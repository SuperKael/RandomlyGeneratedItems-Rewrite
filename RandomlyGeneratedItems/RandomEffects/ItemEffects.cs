using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RoR2;
using UnityEngine;

namespace RandomlyGeneratedItems.RandomEffects
{
    public class ItemEffects : AbstractEffects
    {
        public readonly ItemDef Item;

        public ItemEffects(ItemDef item, Xoroshiro128Plus rng) : base(item.name, item.pickupIconSprite, rng)
        {
            Item = item;
        }

        public override int GetStackCount(CharacterBody body)
        {
            return body && body.inventory ? body.inventory.GetItemCount(Item) : 0;
        }

        public override int Generate()
        {
            Rng = new Xoroshiro128Plus(Rng);
            Description = string.Empty;
            SpriteColors = Array.Empty<Color>();

            float strengthModifier;
            float stackScalingModifier;

            switch (Item.tier)
            {
                case ItemTier.Tier1:
                    Grade = 1;
                    strengthModifier = 1f;
                    stackScalingModifier = 1f;
                    break;
                case ItemTier.Tier2:
                    Grade = 2;
                    strengthModifier = 3.2f;
                    stackScalingModifier = 0.5f;
                    break;
                case ItemTier.Tier3:
                    Grade = 3;
                    strengthModifier = 12f;
                    stackScalingModifier = 0.15f;
                    break;
                case ItemTier.Boss:
                    Grade = 4;
                    strengthModifier = 3f;
                    stackScalingModifier = 0.25f;
                    break;
                case ItemTier.VoidTier1:
                    Grade = 1;
                    strengthModifier = 1.5f;
                    stackScalingModifier = 1f;
                    break;
                case ItemTier.VoidTier2:
                    Grade = 2;
                    strengthModifier = 2.4f;
                    stackScalingModifier = 0.75f;
                    break;
                case ItemTier.VoidTier3:
                    Grade = 3;
                    strengthModifier = 8f;
                    stackScalingModifier = 0.45f;
                    break;
                case ItemTier.VoidBoss:
                    Grade = 4;
                    strengthModifier = 2f;
                    stackScalingModifier = 0.6f;
                    break;
                case ItemTier.Lunar:
                    Grade = 5;
                    strengthModifier = 1.8f;
                    stackScalingModifier = 0.5f;
                    break;
                default:
                    Grade = 0;
                    strengthModifier = 1f;
                    stackScalingModifier = 1f;
                    break;
            }

            int conditionCount = 0;
            // More than one condition, as many as four, is possible, but reaching that many would be highly improbable (1/256 chance)
            while (conditionCount < 4 && Rng.RangeFloat(0f, 1f) < 0.25f) conditionCount++;

            bool hasPassiveEffect = Rng.nextBool;
            bool hasTriggeredEffect = !hasPassiveEffect || Rng.nextBool;

            List<EffectCondition> conditions = new();

            for (int i = 0; i < conditionCount; i++)
            {
                EffectCondition effectCondition;
                do
                {
                    effectCondition = EffectCondition.RegisteredConditions.Values.ElementAt(Rng.RangeInt(0, EffectCondition.RegisteredConditions.Count));
                } while (effectCondition.MinimumGrade > Grade ||
                         conditions.Any(existingCondition => effectCondition.Name == existingCondition.Name || 
                                                             effectCondition.ExclusiveConditions.Contains(existingCondition.Name) ||
                                                             existingCondition.ExclusiveConditions.Contains(effectCondition.Name)));
                conditions.Add(effectCondition);

                strengthModifier *= effectCondition.StrengthModifier;

                Conditions.Add(effectCondition.GetConditionCallback(this));
                string conditionDesc = effectCondition.DescriptionDelegate(this);
                if (i > 0)
                {
                    conditionDesc = char.ToLower(conditionDesc[0]) + conditionDesc[1..];
                }
                Description += conditionDesc;
            }

            Chance = Rng.RangeFloat(10f, 20f) * strengthModifier;
            ChanceStackScaling = Rng.nextBool ? Rng.nextBool ? 1 : Rng.RangeFloat(0.5f, 1f) : 0;

            PassiveStrength = Rng.RangeFloat(1f, 2f) * strengthModifier;
            PassiveStackScaling = stackScalingModifier / (1 + ChanceStackScaling);
            if (hasTriggeredEffect) PassiveStrength *= 0.5f;
            TriggeredStrength = Rng.RangeFloat(1f, 2f) * strengthModifier;
            TriggeredStackScaling = stackScalingModifier / (1 + ChanceStackScaling);
            if (hasPassiveEffect) TriggeredStrength *= 0.5f;

            if (Chance >= 100f)
            {
                Chance = 100;
            }
            else
            {
                TriggeredStrength *= 1 + MathF.Log(1 / (Chance / 100));
            }

            Color[] passiveColors = null;
            if (hasPassiveEffect)
            {
                PassiveEffect passiveEffect;
                do
                {
                    passiveEffect = PassiveEffect.RegisteredPassiveEffects.Values.ElementAt(Rng.RangeInt(0, PassiveEffect.RegisteredPassiveEffects.Count));
                } while (passiveEffect.MinimumGrade > Grade ||
                         conditions.Any(condition => passiveEffect.ExclusiveConditions.Contains(condition.Name)));

                PassiveStrength *= passiveEffect.StrengthModifier;
                passiveColors = passiveEffect.SpriteColors;
                if (passiveEffect.ItemTags?.Length > 0) Item.tags = Item.tags.AddRangeToArray(passiveEffect.ItemTags);

                OnPassiveEffect += passiveEffect.GetPassiveEffectCallback(this);
                string passiveDesc = passiveEffect.DescriptionDelegate(this);
                if (conditionCount > 0)
                {
                    passiveDesc = char.ToLower(passiveDesc[0]) + passiveDesc[1..];
                }
                Description += passiveDesc;
            }

            if (!hasTriggeredEffect)
            {
                SpriteColors = new Color[passiveColors.Length];
                Array.Copy(passiveColors, 0, SpriteColors, 0, passiveColors.Length);
                return 0;
            }
            
            EffectTriggerType effectTriggerType;
            do
            {
                effectTriggerType = EffectTriggerType.RegisteredTriggerTypes.Values.ElementAt(Rng.RangeInt(0, EffectTriggerType.RegisteredTriggerTypes.Count));
            } while (conditions.Any(condition => effectTriggerType.ExclusiveConditions.Contains(condition.Name)) || 
                     TriggerTypeMap[effectTriggerType.Name].All(triggeredEffectName => 
                         !TriggeredEffect.RegisteredTriggeredEffects.TryGetValue(triggeredEffectName, out TriggeredEffect triggeredEffect) ||
                         triggeredEffect.MinimumGrade > Grade ||
                         conditions.Any(condition => triggeredEffect.ExclusiveConditions.Contains(condition.Name))));

            TriggerType = effectTriggerType.Name;
            TriggeredStrength *= effectTriggerType.StrengthModifier;

            TriggeredEffect triggeredEffect;
            do
            {
                triggeredEffect = TriggeredEffect.RegisteredTriggeredEffects[TriggerTypeMap[TriggerType][Rng.RangeInt(0, TriggerTypeMap[TriggerType].Count)]];
            } while (triggeredEffect.MinimumGrade > Grade ||
                     conditions.Any(condition => triggeredEffect.ExclusiveConditions.Contains(condition.Name)));

            TriggeredStrength *= triggeredEffect.StrengthModifier;
            Color[] triggeredColors = triggeredEffect.SpriteColors;
            if (triggeredEffect.ItemTags?.Length > 0) Item.tags = Item.tags.AddRangeToArray(triggeredEffect.ItemTags);

            OnTriggeredEffect += triggeredEffect.GetTriggeredEffectCallback(this);
            string triggerDesc = effectTriggerType.DescriptionDelegate(this) + triggeredEffect.DescriptionDelegate(this);
            if (hasPassiveEffect)
            {
                if (Description.EndsWith(".")) Description = Description[..^1];
                triggerDesc = ", and " + char.ToLower(triggerDesc[0]) + triggerDesc[1..];
            }
            else if (conditionCount > 0)
            {
                triggerDesc = char.ToLower(triggerDesc[0]) + triggerDesc[1..];
            }

            Description += triggerDesc;


            if (SpriteColors?.Length > 0)
            {
                Color[] newSpriteColors = new Color[(passiveColors?.Length ?? 0) + triggeredColors.Length + SpriteColors.Length];
                Array.Copy(SpriteColors, 0, newSpriteColors, newSpriteColors.Length - SpriteColors.Length, SpriteColors.Length);
                SpriteColors = newSpriteColors;
            }
            else
            {
                SpriteColors = new Color[(passiveColors?.Length ?? 0) + triggeredColors.Length];
            }
            if (passiveColors != null) Array.Copy(passiveColors, 0, SpriteColors, 0, passiveColors.Length);
            Array.Copy(triggeredColors, 0, SpriteColors, SpriteColors.Length - triggeredColors.Length, triggeredColors.Length);

            return hasPassiveEffect ? 2 : 1;
        }
    }
}
