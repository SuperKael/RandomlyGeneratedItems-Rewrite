using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using R2API;
using RoR2;
using RoR2.ContentManagement;
using UnityEngine;
using UnityEngine.Networking;

namespace RandomlyGeneratedItems.RandomEffects
{
    public abstract class AbstractEffects
    {
        public static readonly Dictionary<string, AbstractEffects> RegisteredEffects = new();
        public static readonly Dictionary<string, List<string>> TriggerTypeMap = new();

        public int Grade;
        public string TriggerType;
        public string Description;
        public float PassiveStrength;
        public float PassiveStackScaling;
        public float TriggeredStrength;
        public float TriggeredStackScaling;
        public float Chance;
        public float ChanceStackScaling;
        public ProcType? ProcType;
        public Color[] SpriteColors;

        public Dictionary<string, string> ExtraText = new();

        public readonly string Name;
        public readonly Sprite Sprite;

        public List<EffectCondition.ConditionCallback> Conditions = new();
        public event PassiveEffect.PassiveEffectCallback OnPassiveEffect;
        public event TriggeredEffect.TriggeredEffectCallback OnTriggeredEffect;

        public Xoroshiro128Plus Rng;

        public static IEnumerator Initialize(ContentPack contentPack)
        {
            yield return SpawnableEffectPayload.Initialize();
            yield return EffectStatus.Initialize();
            yield return EffectCondition.Initialize();
            yield return PassiveEffect.Initialize();
            yield return EffectTriggerType.Initialize();
            yield return TriggeredEffect.Initialize();
        }

        public static AbstractEffects GetEffects(string name)
        {
            return RegisteredEffects[name];
        }

        public static void ApplyPassiveEffects(CharacterBody character, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!character || !character.inventory || !NetworkServer.active) return;

            foreach (ItemIndex index in character.inventory.itemAcquisitionOrder)
            {
                if (!RegisteredEffects.TryGetValue(ItemCatalog.GetItemDef(index).name, out AbstractEffects itemEffects)) continue;

                int stackCount = itemEffects.GetStackCount(character);
                if (stackCount <= 0 || !itemEffects.ConditionsMet(character)) continue;

                try
                {
                    itemEffects.OnPassiveEffect?.Invoke(args, stackCount, character);
                }
                catch (Exception ex)
                {
                    Main.RgiLogger.LogError($"Error invoking passive effect for item {itemEffects.Name}:");
                    Main.RgiLogger.LogError(ex);
                }
            }

            if (character.equipmentSlot == null || character.equipmentSlot.equipmentIndex == EquipmentIndex.None ||
                !RegisteredEffects.TryGetValue(EquipmentCatalog.GetEquipmentDef(character.equipmentSlot.equipmentIndex).name,
                    out AbstractEffects equipmentEffects)) return;

            try
            {
                equipmentEffects.OnPassiveEffect?.Invoke(args, 1, character);
            }
            catch (Exception ex)
            {
                Main.RgiLogger.LogError($"Error invoking passive effect for equipment {equipmentEffects.Name}:");
                Main.RgiLogger.LogError(ex);
            }
        }

        public static void TriggerEffects(string name, CharacterBody character, Dictionary<string, object> args)
        {
            TriggerEffects(name, character, 1f, null, args);
        }

        public static void TriggerEffects(string name, CharacterBody character, ProcChainMask? procChainMask, Dictionary<string, object> args)
        {
            TriggerEffects(name, character, 1f, procChainMask, args);
        }

        public static void TriggerEffects(string name, CharacterBody character, DamageReport damageReport, Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            args["damageReport"] = damageReport;
            TriggerEffects(name, character, damageReport.damageInfo, args);
        }

        public static void TriggerEffects(string name, CharacterBody character, DamageInfo damageInfo, Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            args["damageInfo"] = damageInfo;
            TriggerEffects(name, character, damageInfo.procCoefficient, damageInfo.procChainMask, args);
        }

        public static void TriggerEffects(string name, CharacterBody character, float procCoefficient,
            ProcChainMask? procChainMask, Dictionary<string, object> args)
        {
            if (!character || !character.inventory || !NetworkServer.active) return;

            args ??= new Dictionary<string, object>();
            ProcChainMask newMask = new();
            if (procChainMask.HasValue) newMask.mask = procChainMask.Value.mask;

            foreach (ItemIndex index in character.inventory.itemAcquisitionOrder)
            {
                if (!RegisteredEffects.TryGetValue(ItemCatalog.GetItemDef(index).name, out AbstractEffects itemEffects) || itemEffects.TriggerType != name) continue;

                int stackCount = itemEffects.GetStackCount(character);
                float chance = itemEffects.GetChance(stackCount, procCoefficient);
                if (stackCount <= 0 || !itemEffects.ConditionsMet(character) || chance < 100 && !Util.CheckRoll(chance, character.master)) continue;

                if (procChainMask.HasValue && itemEffects.ProcType.HasValue)
                {
                    if (procChainMask.Value.HasProc(itemEffects.ProcType.Value)) continue;
                    newMask.AddProc(itemEffects.ProcType.Value);
                }

                try
                {
                    itemEffects.OnTriggeredEffect?.Invoke(character, stackCount, procCoefficient, newMask, args);
                }
                catch (Exception ex)
                {
                    Main.RgiLogger.LogError($"Error invoking triggered effect for item {itemEffects.Name}:");
                    Main.RgiLogger.LogError(ex);
                }
            }

            if (character.equipmentSlot == null || character.equipmentSlot.equipmentIndex == EquipmentIndex.None) return;
            EquipmentDef equipment = EquipmentCatalog.GetEquipmentDef(character.equipmentSlot.equipmentIndex);
            if (equipment == null || !RegisteredEffects.TryGetValue(equipment.name, out AbstractEffects equipmentEffects) || equipmentEffects.TriggerType != name) return;

            if (procChainMask.HasValue && equipmentEffects.ProcType.HasValue)
            {
                if (procChainMask.Value.HasProc(equipmentEffects.ProcType.Value)) return;
                newMask.AddProc(equipmentEffects.ProcType.Value);
            }

            try
            {
                equipmentEffects.OnTriggeredEffect?.Invoke(character, 1, procCoefficient, newMask, args);
            }
            catch (Exception ex)
            {
                Main.RgiLogger.LogError($"Error invoking triggered effect for equipment {equipmentEffects.Name}:");
                Main.RgiLogger.LogError(ex);
            }
        }

        protected AbstractEffects(string name, Sprite sprite, Xoroshiro128Plus rng)
        {
            Name = name;
            Sprite = sprite;
            Rng = rng;
        }

        public void Register()
        {
            RegisteredEffects[Name] = this;
        }

        public bool ConditionsMet(CharacterBody body)
        {
            return Conditions.All(condition => condition(body));
        }

        public abstract int GetStackCount(CharacterBody body);

        public float GetChance(int stackCount, float procCoefficient)
        {
            return Chance >= 100 ? Chance : Chance * (1 + ChanceStackScaling * (stackCount - 1)) * procCoefficient;
        }

        public float GetPassiveStrength(int stackCount)
        {
            return PassiveStrength * (1 + PassiveStackScaling * (stackCount - 1)) * 0.01f;
        }

        public float GetTriggeredStrength(int stackCount, float procCoefficient)
        {
            return TriggeredStrength * (1 + TriggeredStackScaling * (stackCount - 1)) * procCoefficient * 0.01f;
        }

        public string FormatChancePercentage()
        {
            return $"<style=cIsDamage>{Chance:0.##}%</style>" + (ChanceStackScaling > 0 ? $" <style=cStack>(+{Chance * ChanceStackScaling:0.##}% per stack)</style>" : "");
        }

        public string FormatPassiveStrengthPercentage(string textStyle)
        {
            return $"<style=c{textStyle}>{PassiveStrength:0.##}%</style>" + (PassiveStackScaling > 0 ? $" <style=cStack>(+{PassiveStrength * PassiveStackScaling:0.##}% per stack)</style>" : "");
        }

        public string FormatTriggeredStrengthPercentage(string textStyle)
        {
            return $"<style=c{textStyle}>{TriggeredStrength:0.##}%</style>" + (TriggeredStackScaling > 0 ? $" <style=cStack>(+{TriggeredStrength * TriggeredStackScaling:0.##}% per stack)</style>" : "");
        }

        public abstract int Generate();

        public delegate string DescriptionDelegate(AbstractEffects effects);
    }
}