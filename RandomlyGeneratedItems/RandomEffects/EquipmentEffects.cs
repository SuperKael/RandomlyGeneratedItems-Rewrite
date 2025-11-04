using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RoR2;
using UnityEngine;
using UnityEngine.UI;

namespace RandomlyGeneratedItems.RandomEffects
{
    public class EquipmentEffects : AbstractEffects
    {
        public EquipmentDef Equipment;
        public EquipmentDef InactiveEquipment;

        public override Sprite Sprite => Equipment.pickupIconSprite;
        protected override string NameLanguageToken => Equipment.nameToken;
        protected override string PickupLanguageToken => Equipment.pickupToken;
        protected override string DescriptionLanguageToken => Equipment.descriptionToken;
        protected override string LoreLanguageToken => Equipment.loreToken;

        public EquipmentEffects(EquipmentDef equipment, Xoroshiro128Plus rng) : base(equipment.name, rng)
        {
            Equipment = equipment;
        }

        public override int GetStackCount(CharacterBody body)
        {
            return body.equipmentSlot.equipmentIndex == Equipment.equipmentIndex ? 1 : 0;
        }

        public override void Deactivate(CharacterBody body, int amount = -1)
        {
            CharacterMasterNotificationQueue.SendTransformNotification(body.master, Equipment.equipmentIndex, InactiveEquipment.equipmentIndex, CharacterMasterNotificationQueue.TransformationType.Default);
            body.inventory.SetEquipmentIndex(InactiveEquipment.equipmentIndex);
        }

        public override void Reactivate(CharacterBody body, int amount = -1)
        {
            CharacterMasterNotificationQueue.SendTransformNotification(body.master, InactiveEquipment.equipmentIndex, Equipment.equipmentIndex, CharacterMasterNotificationQueue.TransformationType.RegeneratingScrapRegen);
            body.inventory.SetEquipmentIndex(Equipment.equipmentIndex);
        }

        public override SpriteShape Generate()
        {
            Rng = new Xoroshiro128Plus(Rng);
            SpriteColors = Array.Empty<Color>();

            Grade = 2;
            float strengthModifier = 2f;

            if (Equipment.isLunar)
            {
                Grade += 4;
                strengthModifier *= 2;
            }

            if (Equipment.isBoss)
            {
                Grade += 2;
                strengthModifier *= 4;
            }
            
            bool hasPassiveEffect = Rng.nextBool && PassiveEffect.RegisteredPassiveEffects.Any(effect => effect.Value.MinimumGrade <= Grade); ;

            Chance = 100;
            ChanceStackScaling = 0;

            PassiveStrength = Rng.RangeFloat(1f, 2f) * strengthModifier;
            PassiveStackScaling = 0;
            TriggeredStrength = Rng.RangeFloat(1f, 2f) * strengthModifier;
            TriggeredStackScaling = 0;
            LunarStrength = Rng.RangeFloat(1f, 2f) * strengthModifier;
            LunarStackScaling = 0;

            Equipment.cooldown = Rng.RangeFloat(4f, 8f) * TriggeredStrength;
            if (hasPassiveEffect) Equipment.cooldown *= 2;

            Color[] passiveColors = null;
            if (hasPassiveEffect)
            {
                PassiveEffect passiveEffect;
                do
                {
                    passiveEffect = PassiveEffect.RegisteredPassiveEffects.Values.ElementAt(Rng.RangeInt(0, PassiveEffect.RegisteredPassiveEffects.Count));
                } while (passiveEffect.MinimumGrade > Grade || passiveEffect.ExclusiveConditions.Contains("IsEquipment"));

                PassiveStrength *= passiveEffect.StrengthModifier;
                passiveColors = passiveEffect.SpriteColors;

                AddPassiveEffect(passiveEffect);
            }

            bool triggerTypeRegistered;
            if (EffectTriggerType.RegisteredTriggerTypes.TryGetValue("Equipment", out EffectTriggerType triggerType))
            {
                triggerTypeRegistered = true;
                SetTrigger(triggerType);
                TriggeredStrength *= triggerType.StrengthModifier;
            }
            else
            {
                triggerTypeRegistered = false;
                TriggerType = "Equipment";
                TriggeredStrength *= 4f;
            }

            TriggeredEffect equipmentEffect;
            bool equipmentExclusiveEffect = (Rng.nextBool 
                                             || !triggerTypeRegistered 
                                             || !TriggeredEffect.RegisteredTriggeredEffects.Values.Any(effect => effect.MinimumGrade <= Grade && !effect.ExclusiveConditions.Contains("Equipment"))) 
                                            && TriggeredEffect.RegisteredEquipmentEffects.Any(effect => effect.Value.MinimumGrade <= Grade);
            if (equipmentExclusiveEffect)
            {
                do
                {
                    equipmentEffect = TriggeredEffect.RegisteredEquipmentEffects.Values.ElementAt(Rng.RangeInt(0, TriggeredEffect.RegisteredEquipmentEffects.Count));
                } while (equipmentEffect.MinimumGrade > Grade);
            }
            else
            {
                if (!triggerTypeRegistered || !TriggeredEffect.RegisteredTriggeredEffects.Values.Any(effect => effect.MinimumGrade <= Grade && !effect.ExclusiveConditions.Contains("Equipment")))
                {
                    OverrideDescription("You disabled every possible triggered and equipment effect... what did you think would happen?");
                    return SpriteShape.Circle;
                }

                do
                {
                    equipmentEffect = TriggeredEffect.RegisteredTriggeredEffects[TriggerTypeMap[TriggerType][Rng.RangeInt(0, TriggerTypeMap[TriggerType].Count)]];
                } while (equipmentEffect.MinimumGrade > Grade || equipmentEffect.ExclusiveConditions.Contains("IsEquipment"));
            }

            TriggeredStrength *= equipmentEffect.StrengthModifier;
            Equipment.cooldown *= equipmentEffect.CooldownModifier;
            Color[] triggeredColors = equipmentEffect.SpriteColors;

            AddTriggeredEffect(equipmentEffect);

            Equipment.cooldown = (float) Math.Round(Equipment.cooldown, 2);

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

            SpriteShape spriteShape = hasPassiveEffect ? equipmentExclusiveEffect ? SpriteShape.Cylinder : SpriteShape.Circle : equipmentExclusiveEffect ? SpriteShape.Diamond : SpriteShape.Rhombus;

            if (!Equipment.isLunar) return spriteShape;

            bool lunarEffectIsTriggered = Rng.nextBool;

            bool noPassiveLunarEffects = false;
            if (!PassiveEffect.RegisteredPassiveLunarEffects.Values.Any(effect =>
                    effect.MinimumGrade <= Grade))
            {
                noPassiveLunarEffects = true;
                lunarEffectIsTriggered = true;
            }

            if (lunarEffectIsTriggered && !TriggeredEffect.RegisteredTriggeredLunarEffects.Values.Any(effect =>
                    effect.MinimumGrade <= Grade &&
                    LunarTriggerTypeMap.TryGetValue(TriggerType, out List<string> typeList) && typeList.Contains(effect.Name)))
            {
                lunarEffectIsTriggered = false;
            }

            if (noPassiveLunarEffects && !lunarEffectIsTriggered) return spriteShape;

            if (lunarEffectIsTriggered)
            {
                TriggeredEffect[] validTriggeredLunarEffects = TriggeredEffect.RegisteredTriggeredLunarEffects.Values.Where(effect =>
                    effect.MinimumGrade <= Grade &&
                    LunarTriggerTypeMap.TryGetValue(TriggerType, out List<string> typeList) && typeList.Contains(effect.Name)).ToArray();

                TriggeredEffect triggeredEffect = validTriggeredLunarEffects.ElementAt(Rng.RangeInt(0, validTriggeredLunarEffects.Length));

                PassiveStrength *= triggeredEffect.StrengthModifier;
                TriggeredStrength *= triggeredEffect.StrengthModifier;
                LunarStrength *= triggeredEffect.StrengthModifier * EffectTriggerType.RegisteredTriggerTypes[TriggerType].StrengthModifier;
                if (Chance < 100f) LunarStrength *= 1 + MathF.Log(1 / (Chance / 100));

                AddTriggeredEffect(triggeredEffect);

                SpriteColors = SpriteColors.AddRangeToArray(triggeredEffect.SpriteColors);
            }
            else
            {
                PassiveEffect[] validPassiveLunarEffects = PassiveEffect.RegisteredPassiveLunarEffects.Values.Where(effect =>
                    effect.MinimumGrade <= Grade).ToArray();

                PassiveEffect passiveEffect = validPassiveLunarEffects.ElementAt(Rng.RangeInt(0, validPassiveLunarEffects.Length));

                PassiveStrength *= passiveEffect.StrengthModifier;
                TriggeredStrength *= passiveEffect.StrengthModifier;
                LunarStrength *= passiveEffect.StrengthModifier;

                AddPassiveEffect(passiveEffect);

                SpriteColors = SpriteColors.AddRangeToArray(passiveEffect.SpriteColors);
            }

            return spriteShape;
        }

        protected override string BuildDescription()
        {
            StringBuilder sb = new();

            // Passive Effects
            if (PassiveEffectNames.Count > 0)
            {
                sb.Append("<b>PASSIVE:</b> ");
                sb.Append("Applies passively just from holding this equipment.");

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
                    if (TriggeredEffect.RegisteredEquipmentEffects.TryGetValue(effectName, out TriggeredEffect effect))
                    {
                        sb.Append("\n<style=cIsUtility><b> => </b></style>");
                        sb.Append(effect.DescriptionDelegate(this));
                    }
                    else if (TriggeredEffect.RegisteredTriggeredEffects.TryGetValue(effectName, out effect))
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

            // Equipment Cooldown
            sb.Append($"\n<b>COOLDOWN:</b> <style=cIsUtility>{Equipment.cooldown:0.#} seconds</style>");

            return sb.ToString();
        }
    }
}
