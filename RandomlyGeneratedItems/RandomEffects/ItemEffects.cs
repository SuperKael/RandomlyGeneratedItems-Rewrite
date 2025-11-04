using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using R2API;
using RoR2;
using UnityEngine;

namespace RandomlyGeneratedItems.RandomEffects
{
    public class ItemEffects : AbstractEffects
    {
        public ItemDef Item;
        public ItemDef InactiveItem;

        public string VoidCorruptsItemNameToken;

        public override Sprite Sprite => Item.pickupIconSprite;
        protected override string NameLanguageToken => Item.nameToken;
        protected override string PickupLanguageToken => Item.pickupToken;
        protected override string DescriptionLanguageToken => Item.descriptionToken;
        protected override string LoreLanguageToken => Item.loreToken;

        public ItemEffects(ItemDef item, Xoroshiro128Plus rng) : base(item.name, rng)
        {
            Item = item;
        }

        public override int GetStackCount(CharacterBody body)
        {
            return body && body.inventory ? body.inventory.GetItemCount(Item) : 0;
        }

        public override void Deactivate(CharacterBody body, int amount = -1)
        {
            int stackCount = body.inventory.GetItemCount(InactiveItem);
            if (stackCount < 1) return;
            if (amount != -1 && amount < stackCount) stackCount = amount;
            body.inventory.RemoveItem(Item, stackCount);
            body.inventory.GiveItem(InactiveItem, stackCount);
            CharacterMasterNotificationQueue.SendTransformNotification(body.master, InactiveItem.itemIndex, Item.itemIndex, CharacterMasterNotificationQueue.TransformationType.Default);
        }

        public override void Reactivate(CharacterBody body, int amount = -1)
        {
            int stackCount = body.inventory.GetItemCount(InactiveItem);
            if (stackCount < 1) return;
            if (amount != -1 && amount < stackCount) stackCount = amount;
            body.inventory.RemoveItem(InactiveItem, stackCount);
            body.inventory.GiveItem(Item, stackCount);
            CharacterMasterNotificationQueue.SendTransformNotification(body.master, InactiveItem.itemIndex, Item.itemIndex, CharacterMasterNotificationQueue.TransformationType.RegeneratingScrapRegen);
        }

        public override SpriteShape Generate()
        {
            Rng = new Xoroshiro128Plus(Rng);
            SpriteColors = Array.Empty<Color>();

            float strengthModifier;
            float stackScalingModifier;
            bool isLunar = false;

            switch (Item.tier)
            {
                case ItemTier.Tier1:
                    Grade = 1;
                    strengthModifier = 1f;
                    stackScalingModifier = 1f;
                    break;
                case ItemTier.Tier2:
                    Grade = 2;
                    strengthModifier = 3f;
                    stackScalingModifier = 0.5f;
                    break;
                case ItemTier.Tier3:
                    Grade = 3;
                    strengthModifier = 12f;
                    stackScalingModifier = 0.5f;
                    break;
                case ItemTier.Boss:
                    Grade = 4;
                    strengthModifier = 6f;
                    stackScalingModifier = 0.25f;
                    break;
                case ItemTier.VoidTier1:
                    Grade = 2;
                    strengthModifier = 1.5f;
                    stackScalingModifier = 1f;
                    break;
                case ItemTier.VoidTier2:
                    Grade = 3;
                    strengthModifier = 2f;
                    stackScalingModifier = 0.75f;
                    break;
                case ItemTier.VoidTier3:
                    Grade = 4;
                    strengthModifier = 8f;
                    stackScalingModifier = 0.75f;
                    break;
                case ItemTier.VoidBoss:
                    Grade = 5;
                    strengthModifier = 4f;
                    stackScalingModifier = 0.5f;
                    break;
                case ItemTier.Lunar:
                    Grade = 6;
                    strengthModifier = 8f;
                    stackScalingModifier = 1f;
                    isLunar = true;
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

            if (conditionCount > EffectCondition.RegisteredConditions.Count)
                conditionCount = EffectCondition.RegisteredConditions.Count;

            bool hasPassiveEffect = Rng.nextBool;
            bool hasTriggeredEffect = !hasPassiveEffect || Rng.nextBool;

            List<EffectCondition> conditions = new();

            for (int i = 0; i < conditionCount; i++)
            {
                if (EffectCondition.RegisteredConditions.Values.All(effectCondition =>
                        effectCondition.MinimumGrade > Grade ||
                        conditions.Any(existingCondition => effectCondition.Name == existingCondition.Name ||
                                                            effectCondition.ExclusiveConditions.Contains(
                                                                existingCondition.Name) ||
                                                            existingCondition.ExclusiveConditions.Contains(
                                                                effectCondition.Name)))) break;

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

                AddCondition(effectCondition);
            }

            bool noPassiveEffects = false;
            if (!PassiveEffect.RegisteredPassiveEffects.Values.Any(effect =>
                    effect.MinimumGrade <= Grade &&
                    conditions.All(condition => !effect.ExclusiveConditions.Contains(condition.Name))))
            {
                noPassiveEffects = true;
                hasPassiveEffect = false;
                hasTriggeredEffect = true;
            }
            
            if (!EffectTriggerType.RegisteredTriggerTypes.Values.Any(triggerType => 
                    conditions.All(typeCondition => !triggerType.ExclusiveConditions.Contains(typeCondition.Name)) && 
                    TriggerTypeMap.TryGetValue(triggerType.Name, out List<string> typeList) && 
                    TriggeredEffect.RegisteredTriggeredEffects.Values.Any(effect => 
                        typeList.Contains(effect.Name) && effect.MinimumGrade <= Grade && conditions.All(effectCondition => !effect.ExclusiveConditions.Contains(effectCondition.Name)))))
            {
                hasTriggeredEffect = false;
                if (!noPassiveEffects) hasPassiveEffect = true;
            }

            Chance = Rng.RangeFloat(10f, 20f) * strengthModifier;
            ChanceStackScaling = Rng.nextBool ? Rng.nextBool ? 1 : Rng.RangeFloat(0.5f, 1f) : 0;

            PassiveStrength = Rng.RangeFloat(1f, 2f) * strengthModifier;
            PassiveStackScaling = stackScalingModifier / (1 + ChanceStackScaling);
            if (hasTriggeredEffect) PassiveStrength *= 0.5f;
            TriggeredStrength = Rng.RangeFloat(1f, 2f) * strengthModifier;
            TriggeredStackScaling = stackScalingModifier / (1 + ChanceStackScaling);
            if (hasPassiveEffect) TriggeredStrength *= 0.5f;
            LunarStrength = Rng.RangeFloat(1f, 2f) * strengthModifier;
            LunarStackScaling = stackScalingModifier / (1 + ChanceStackScaling);

            if (Chance >= 100f)
            {
                Chance = 100;
            }
            else
            {
                TriggeredStrength *= 1 + MathF.Log(1 / (Chance / 100));
            }
            
            if (hasPassiveEffect)
            {
                PassiveEffect passiveEffect;
                do
                {
                    passiveEffect = PassiveEffect.RegisteredPassiveEffects.Values.ElementAt(Rng.RangeInt(0, PassiveEffect.RegisteredPassiveEffects.Count));
                } while (passiveEffect.MinimumGrade > Grade ||
                         conditions.Any(condition => passiveEffect.ExclusiveConditions.Contains(condition.Name)));

                if (passiveEffect.MinimumGrade >= 6) isLunar = false;

                PassiveStrength *= passiveEffect.StrengthModifier;
                Item.tags = Item.tags.AddRangeToArray(passiveEffect.ItemTags);

                AddPassiveEffect(passiveEffect);

                SpriteColors = SpriteColors.AddRangeToArray(passiveEffect.SpriteColors);
            }
            else if (!hasTriggeredEffect)
            {
                OverrideDescription("You disabled every possible passive and triggered effect... what did you think would happen?");
                return SpriteShape.Circle;
            }

            if (hasTriggeredEffect)
            {
                EffectTriggerType effectTriggerType;
                do
                {
                    effectTriggerType = EffectTriggerType.RegisteredTriggerTypes.Values.ElementAt(Rng.RangeInt(0, EffectTriggerType.RegisteredTriggerTypes.Count));
                } while (conditions.Any(condition => effectTriggerType.ExclusiveConditions.Contains(condition.Name)) ||
                         TriggerTypeMap[effectTriggerType.Name].All(triggeredEffectName =>
                             !TriggeredEffect.RegisteredTriggeredEffects.TryGetValue(triggeredEffectName, out TriggeredEffect triggeredEffect) ||
                             triggeredEffect.MinimumGrade > Grade ||
                             conditions.Any(condition => triggeredEffect.ExclusiveConditions.Contains(condition.Name))));

                SetTrigger(effectTriggerType);
                TriggeredStrength *= effectTriggerType.StrengthModifier;

                TriggeredEffect triggeredEffect;
                do
                {
                    triggeredEffect = TriggeredEffect.RegisteredTriggeredEffects[TriggerTypeMap[TriggerType][Rng.RangeInt(0, TriggerTypeMap[TriggerType].Count)]];
                } while (triggeredEffect.MinimumGrade > Grade ||
                         conditions.Any(condition => triggeredEffect.ExclusiveConditions.Contains(condition.Name)));

                if (triggeredEffect.MinimumGrade >= 6) isLunar = false;

                TriggeredStrength *= triggeredEffect.StrengthModifier;
                Item.tags = Item.tags.AddRangeToArray(triggeredEffect.ItemTags);

                AddTriggeredEffect(triggeredEffect);

                SpriteColors = SpriteColors.AddRangeToArray(triggeredEffect.SpriteColors);
            }

            SpriteShape spriteShape = hasTriggeredEffect ? hasPassiveEffect ? SpriteShape.Circle : SpriteShape.Rhombus : SpriteShape.Square;

            if (!isLunar) return spriteShape;
            
            bool lunarEffectIsTriggered = hasTriggeredEffect && Rng.nextBool;

            bool noPassiveLunarEffects = false;
            if (!PassiveEffect.RegisteredPassiveLunarEffects.Values.Any(effect =>
                    effect.MinimumGrade <= Grade &&
                    conditions.All(condition => !effect.ExclusiveConditions.Contains(condition.Name))))
            {
                noPassiveLunarEffects = true;
                lunarEffectIsTriggered = hasTriggeredEffect;
            }

            if (lunarEffectIsTriggered && !TriggeredEffect.RegisteredTriggeredLunarEffects.Values.Any(effect =>
                    effect.MinimumGrade <= Grade &&
                    conditions.All(effectCondition => !effect.ExclusiveConditions.Contains(effectCondition.Name)) &&
                    LunarTriggerTypeMap.TryGetValue(TriggerType, out List<string> typeList) && typeList.Contains(effect.Name)))
            {
                lunarEffectIsTriggered = false;
            }

            if (noPassiveLunarEffects && !lunarEffectIsTriggered) return spriteShape;

            if (lunarEffectIsTriggered)
            {
                TriggeredEffect[] validTriggeredLunarEffects = TriggeredEffect.RegisteredTriggeredLunarEffects.Values.Where(effect =>
                    effect.MinimumGrade <= Grade &&
                    conditions.All(effectCondition => !effect.ExclusiveConditions.Contains(effectCondition.Name)) &&
                    LunarTriggerTypeMap.TryGetValue(TriggerType, out List<string> typeList) && typeList.Contains(effect.Name)).ToArray();

                TriggeredEffect triggeredEffect = validTriggeredLunarEffects.ElementAt(Rng.RangeInt(0, validTriggeredLunarEffects.Length));

                PassiveStrength *= triggeredEffect.StrengthModifier;
                TriggeredStrength *= triggeredEffect.StrengthModifier;
                LunarStrength *= triggeredEffect.StrengthModifier * EffectTriggerType.RegisteredTriggerTypes[TriggerType].StrengthModifier;
                if (Chance < 100f) LunarStrength *= 1 + MathF.Log(1 / (Chance / 100));

                Item.tags = Item.tags.AddRangeToArray(triggeredEffect.ItemTags);

                AddTriggeredEffect(triggeredEffect);

                SpriteColors = SpriteColors.AddRangeToArray(triggeredEffect.SpriteColors);
            }
            else
            {
                PassiveEffect[] validPassiveLunarEffects = PassiveEffect.RegisteredPassiveLunarEffects.Values.Where(effect =>
                    effect.MinimumGrade <= Grade &&
                    conditions.All(effectCondition => !effect.ExclusiveConditions.Contains(effectCondition.Name))).ToArray();

                PassiveEffect passiveEffect = validPassiveLunarEffects.ElementAt(Rng.RangeInt(0, validPassiveLunarEffects.Length));

                PassiveStrength *= passiveEffect.StrengthModifier;
                TriggeredStrength *= passiveEffect.StrengthModifier;
                LunarStrength *= passiveEffect.StrengthModifier;

                Item.tags = Item.tags.AddRangeToArray(passiveEffect.ItemTags);

                AddPassiveEffect(passiveEffect);

                SpriteColors = SpriteColors.AddRangeToArray(passiveEffect.SpriteColors);
            }

            return spriteShape;
        }

        protected override string BuildDescription()
        {
            StringBuilder sb = new();

            // Conditions
            if (ConditionNames.Count > 0)
            {
                sb.Append(ConditionNames.Count > 1 ? "<b>CONDITIONS:</b> " : "<b>CONDITION:</b> ");

                bool first = true;
                foreach (string conditionName in ConditionNames)
                {
                    if (first)
                        first = false;
                    else
                        sb.Append(" AND ");

                    if (EffectCondition.RegisteredConditions.TryGetValue(conditionName, out EffectCondition condition))
                        sb.Append(condition.DescriptionDelegate(this));
                    else
                        sb.Append($"<style=cDeath><b>ERROR INVALID CONDITION</b> '{conditionName}'</style>");
                }
            }

            // Passive Effects
            if (PassiveEffectNames.Count > 0)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("<b>PASSIVE:</b> ");
                if (ConditionNames.Count > 1)
                    sb.Append("Applies passively when the conditions are met.");
                else if (ConditionNames.Count > 0)
                    sb.Append("Applies passively when the condition is met.");
                else
                    sb.Append("Applies passively.");

                // We hold onto the descriptions of lunar effects to ensure that they always appear after normal effects
                List<string> lunarEffectDescriptions = new();

                foreach (string effectName in PassiveEffectNames)
                {
                    if (PassiveEffect.RegisteredPassiveEffects.TryGetValue(effectName, out PassiveEffect effect))
                    {
                        sb.Append("\n<style=cIsUtility><b> => </b></style>");
                        sb.Append(effect.DescriptionDelegate(this));
                    }
                    else if (PassiveEffect.RegisteredPassiveLunarEffects.TryGetValue(effectName, out effect))
                    {
                        lunarEffectDescriptions.Add(effect.DescriptionDelegate(this));
                    }
                    else
                    {
                        sb.Append("\n<style=cIsUtility><b> => </b></style>");
                        sb.Append($"<style=cDeath><b>ERROR INVALID PASSIVE EFFECT</b> '{effectName}'</style>");
                    }
                }

                foreach (string effectDescription in lunarEffectDescriptions)
                {
                    sb.Append("\n<style=cDeath><b> => </b></style>");
                    sb.Append(effectDescription);
                }
            }

            // Triggered Effects
            if (TriggeredEffectNames.Count > 0)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("<b>TRIGGER:</b> ");

                if (EffectTriggerType.RegisteredTriggerTypes.TryGetValue(TriggerType, out EffectTriggerType triggerType))
                    sb.Append(triggerType.DescriptionDelegate(this));
                else
                    sb.Append($"<style=cDeath><b>ERROR INVALID TRIGGER</b> '{TriggerType}'</style>");

                // We hold onto the descriptions of lunar effects to ensure that they always appear after normal effects
                List<string> lunarEffectDescriptions = new();

                foreach (string effectName in TriggeredEffectNames)
                {
                    if (TriggeredEffect.RegisteredTriggeredEffects.TryGetValue(effectName, out TriggeredEffect effect))
                    {
                        sb.Append("\n<style=cIsUtility><b> => </b></style>");
                        sb.Append(effect.DescriptionDelegate(this));
                    }
                    else if (TriggeredEffect.RegisteredTriggeredLunarEffects.TryGetValue(effectName, out effect))
                    {
                        lunarEffectDescriptions.Add(effect.DescriptionDelegate(this));
                    }
                    else
                    {
                        sb.Append("\n<style=cIsUtility><b> => </b></style>");
                        sb.Append($"<style=cDeath><b>ERROR INVALID TRIGGERED EFFECT</b> '{effectName}'</style>");
                    }
                }

                foreach (string effectDescription in lunarEffectDescriptions)
                {
                    sb.Append("\n<style=cDeath><b> => </b></style>");
                    sb.Append(effectDescription);
                }
            }

            // Void item corruption line
            if (VoidCorruptsItemNameToken != null)
            {
                sb.Append($"\n<style=cIsVoid>Corrupts all {Language.currentLanguage.GetLocalizedStringByToken(VoidCorruptsItemNameToken)}</style>.");
            }

            return sb.ToString();
        }
    }
}
