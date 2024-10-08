﻿using System;
using System.Linq;
using RoR2;
using UnityEngine;

namespace RandomlyGeneratedItems.RandomEffects
{
    public class EquipmentEffects : AbstractEffects
    {
        public readonly EquipmentDef Equipment;

        public override Sprite Sprite => Equipment.pickupIconSprite;

        public EquipmentEffects(EquipmentDef equipment, Xoroshiro128Plus rng) : base(equipment.name, rng)
        {
            Equipment = equipment;
        }

        public override int GetStackCount(CharacterBody body)
        {
            return body.equipmentSlot.equipmentIndex == Equipment.equipmentIndex ? 1 : 0;
        }

        public override SpriteShape Generate()
        {
            Rng = new Xoroshiro128Plus(Rng);
            Description = string.Empty;
            SpriteColors = Array.Empty<Color>();
            Grade = 2;

            float strengthModifier = 2f;

            if (Equipment.isLunar)
            {
                Grade += 2;
                strengthModifier *= 2;
            }

            if (Equipment.isBoss)
            {
                Grade += 1;
                strengthModifier *= 4;
            }
            
            bool hasPassiveEffect = Rng.nextBool;

            Chance = 100;
            ChanceStackScaling = 0;

            PassiveStrength = Rng.RangeFloat(1f, 2f) * strengthModifier;
            PassiveStackScaling = 0;
            TriggeredStrength = Rng.RangeFloat(1f, 2f) * strengthModifier;
            TriggeredStackScaling = 0;

            Equipment.cooldown = Rng.RangeFloat(4f, 8f) * TriggeredStrength;
            if (hasPassiveEffect) Equipment.cooldown /= 2;

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

                OnPassiveEffect += passiveEffect.GetPassiveEffectCallback(this);
                string passiveDesc = passiveEffect.DescriptionDelegate(this);
                Description += "Passively " + char.ToLower(passiveDesc[0]) + passiveDesc[1..];
            }

            EffectTriggerType effectTriggerType = EffectTriggerType.RegisteredTriggerTypes["Equipment"];
            TriggerType = effectTriggerType.Name;
            TriggeredStrength *= effectTriggerType.StrengthModifier;

            TriggeredEffect triggeredEffect;
            bool equipmentExclusiveEffect = Rng.nextBool;
            if (equipmentExclusiveEffect)
            {
                do
                {
                    triggeredEffect = TriggeredEffect.RegisteredEquipmentEffects.Values.ElementAt(Rng.RangeInt(0, TriggeredEffect.RegisteredEquipmentEffects.Count));
                } while (triggeredEffect.MinimumGrade > Grade);
            }
            else
            {
                do
                {
                    triggeredEffect = TriggeredEffect.RegisteredTriggeredEffects[TriggerTypeMap[TriggerType][Rng.RangeInt(0, TriggerTypeMap[TriggerType].Count)]];
                } while (triggeredEffect.MinimumGrade > Grade || triggeredEffect.ExclusiveConditions.Contains("IsEquipment"));
            }

            TriggeredStrength *= triggeredEffect.StrengthModifier;
            Equipment.cooldown *= triggeredEffect.CooldownModifier;
            Color[] triggeredColors = triggeredEffect.SpriteColors;

            OnTriggeredEffect += triggeredEffect.GetTriggeredEffectCallback(this);
            string triggerDesc = effectTriggerType.DescriptionDelegate(this) + triggeredEffect.DescriptionDelegate(this);
            if (hasPassiveEffect)
            {
                if (Description.EndsWith(".")) Description = Description[..^1];
                triggerDesc = ", and " + char.ToLower(triggerDesc[0]) + triggerDesc[1..];
            }

            Equipment.cooldown = (float) Math.Round(Equipment.cooldown, 2);

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

            return hasPassiveEffect ? equipmentExclusiveEffect ? SpriteShape.Cylinder : SpriteShape.Circle : equipmentExclusiveEffect ? SpriteShape.Diamond : SpriteShape.Rhombus;
        }
    }
}
