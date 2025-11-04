using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using R2API;
using RoR2;
using RoR2.ContentManagement;
using UnityEngine;

namespace RandomlyGeneratedItems.RandomEffects
{
    public abstract class AbstractEffects
    {
        public static readonly Dictionary<string, AbstractEffects> RegisteredEffects = new();
        public static readonly Dictionary<string, AbstractEffects> RegisteredInactiveEffects = new();
        public static readonly Dictionary<string, List<string>> TriggerTypeMap = new();
        public static readonly Dictionary<string, List<string>> LunarTriggerTypeMap = new();

        public static event Func<CharacterBody, string, float, float> OnPassiveSpecialStatUpdated;

        public Xoroshiro128Plus Rng;

        public string Description { get => DescriptionOverrride ?? (BuiltDescription ??= BuildDescription()); }
        private string BuiltDescription;
        private string DescriptionOverrride;

        public int Grade;
        public string TriggerType;

        public float PassiveStrength;
        public float PassiveStackScaling;

        public float TriggeredStrength;
        public float TriggeredStackScaling;

        public float LunarStrength;
        public float LunarStackScaling;

        public float Chance;
        public float ChanceStackScaling;

        public bool HasInactiveForm;
        public string ReactivationTrigger;
        public ProcType? ProcType;
        public Color[] SpriteColors;

        public Dictionary<string, string> ExtraText = new();

        public readonly string Name;
        public abstract Sprite Sprite { get; }

        public List<EffectCondition.ConditionCallback> Conditions = new();
        public event Action OnFinalizeGeneration;
        public event PassiveEffect.PassiveEffectCallback OnPassiveEffect;
        public event PassiveEffect.PassiveSpecialStatCallback OnPassiveSpecialStat;
        public event TriggeredEffect.TriggeredEffectCallback OnTriggeredEffect;
        public List<EffectCondition.ConditionCallback> ReactivationConditions = new();

        protected HashSet<string> ConditionNames = new();
        protected HashSet<string> PassiveEffectNames = new();
        protected HashSet<string> TriggeredEffectNames = new();

        protected abstract string PickupLanguageToken { get; }
        protected abstract string DescriptionLanguageToken { get; }

        private LanguageAPI.LanguageOverlay nameLanguageOverlay;
        private LanguageAPI.LanguageOverlay namePluralLanguageOverlay;
        private LanguageAPI.LanguageOverlay pickupLanguageOverlay;
        private LanguageAPI.LanguageOverlay descriptionLanguageOverlay;
        private LanguageAPI.LanguageOverlay loreLanguageOverlay;

        public static IEnumerator Initialize(ContentPack contentPack)
        {
            yield return SpawnableEffectPayload.Initialize();
            yield return SpawnableInteractable.Initialize();
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
            if (!character || !character.inventory) return;

            foreach (ItemIndex index in character.inventory.itemAcquisitionOrder)
            {
                string name = ItemCatalog.GetItemDef(index).name;
                if (!RegisteredEffects.TryGetValue(name, out AbstractEffects itemEffects)) continue;

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

        public static void ApplyPassiveSpecialStat(CharacterBody character, string stat, ref float value)
        {
            if (!character || !character.inventory) return;

            if (OnPassiveSpecialStatUpdated != null)
            {
                value = OnPassiveSpecialStatUpdated.GetInvocationList().Aggregate(value, (current, handler) => (float)handler.DynamicInvoke(character, stat, current));
            }

            foreach (ItemIndex index in character.inventory.itemAcquisitionOrder)
            {
                string name = ItemCatalog.GetItemDef(index).name;
                if (!RegisteredEffects.TryGetValue(name, out AbstractEffects itemEffects)) continue;

                int stackCount = itemEffects.GetStackCount(character);
                if (stackCount <= 0 || !itemEffects.ConditionsMet(character)) continue;

                try
                {
                    value = itemEffects.OnPassiveSpecialStat?.Invoke(stat, value, stackCount, character) ?? value;
                }
                catch (Exception ex)
                {
                    Main.RgiLogger.LogError($"Error invoking passive special stat {stat} for item {itemEffects.Name}:");
                    Main.RgiLogger.LogError(ex);
                }
            }

            if (character.equipmentSlot == null || character.equipmentSlot.equipmentIndex == EquipmentIndex.None ||
                !RegisteredEffects.TryGetValue(EquipmentCatalog.GetEquipmentDef(character.equipmentSlot.equipmentIndex).name,
                    out AbstractEffects equipmentEffects)) return;

            try
            {
                value = equipmentEffects.OnPassiveSpecialStat?.Invoke(stat, value, 1, character) ?? value;
            }
            catch (Exception ex)
            {
                Main.RgiLogger.LogError($"Error invoking passive special stat for equipment {equipmentEffects.Name}:");
                Main.RgiLogger.LogError(ex);
            }
        }

        public static void TriggerEffects(string triggerType, CharacterBody character, Dictionary<string, object> args)
        {
            TriggerEffects(triggerType, character, 1f, null, args);
        }

        public static void TriggerEffects(string triggerType, CharacterBody character, ProcChainMask? procChainMask, Dictionary<string, object> args)
        {
            TriggerEffects(triggerType, character, 1f, procChainMask, args);
        }

        public static void TriggerEffects(string triggerType, CharacterBody character, DamageReport damageReport, Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            args["damageReport"] = damageReport;
            TriggerEffects(triggerType, character, damageReport.damageInfo, args);
        }

        public static void TriggerEffects(string triggerType, CharacterBody character, DamageInfo damageInfo, Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            args["damageInfo"] = damageInfo;
            TriggerEffects(triggerType, character, damageInfo.procCoefficient, damageInfo.procChainMask, args);
        }

        public static void TriggerEffects(string triggerType, CharacterBody character, float procCoefficient,
            ProcChainMask? procChainMask, Dictionary<string, object> args)
        {
            if (!character || !character.inventory) return;

            args ??= new Dictionary<string, object>();
            ProcChainMask newMask = new();
            if (procChainMask.HasValue) newMask.mask = procChainMask.Value.mask;

            foreach (ItemIndex index in character.inventory.itemAcquisitionOrder.ToList())
            {
                string name = ItemCatalog.GetItemDef(index).name;

                if (RegisteredInactiveEffects.TryGetValue(name, out AbstractEffects inactiveEffects) && 
                    triggerType == inactiveEffects.ReactivationTrigger && 
                    inactiveEffects.ReactivationConditions.All(condition => condition(character)))
                {
                    name = inactiveEffects.Name;
                    inactiveEffects.Reactivate(character);
                }

                if (!RegisteredEffects.TryGetValue(name, out AbstractEffects itemEffects) || triggerType != itemEffects.TriggerType) continue;

                int stackCount = itemEffects.GetStackCount(character);
                float chance = itemEffects.GetChance(stackCount, procCoefficient);
                if (stackCount <= 0 || !args.ContainsKey("forceTrigger") && (!itemEffects.ConditionsMet(character) || chance < 100 && !Util.CheckRoll(chance, character.master))) continue;

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
            if (equipment == null) return;

            if (RegisteredInactiveEffects.TryGetValue(equipment.name, out AbstractEffects inactiveEquipmentEffects) &&
                triggerType == inactiveEquipmentEffects.ReactivationTrigger && 
                inactiveEquipmentEffects.ReactivationConditions.All(condition => condition(character)))
            {
                inactiveEquipmentEffects.Reactivate(character);
                equipment = EquipmentCatalog.GetEquipmentDef(character.equipmentSlot.equipmentIndex);
            }

            if (!RegisteredEffects.TryGetValue(equipment.name, out AbstractEffects equipmentEffects) || equipmentEffects.TriggerType != triggerType) return;

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

        protected AbstractEffects(string name, Xoroshiro128Plus rng)
        {
            Name = name;
            Rng = rng;
        }

        public void Register()
        {
            OnFinalizeGeneration?.Invoke();
            RegisteredEffects[Name] = this;
            if (HasInactiveForm) RegisteredInactiveEffects[Name + "_INACTIVE"] = this;
        }

        public bool ConditionsMet(CharacterBody body)
        {
            return Buffs.BypassEffectConditions.BuffDef != null && body.HasBuff(Buffs.BypassEffectConditions.BuffDef) || Conditions.All(condition => condition(body));
        }

        public abstract int GetStackCount(CharacterBody body);
        public abstract void Deactivate(CharacterBody body, int amount = -1);
        public abstract void Reactivate(CharacterBody body, int amount = -1);

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

        public float GetLunarStrength(int stackCount, float procCoefficient = 1)
        {
            return LunarStrength * (1 + LunarStackScaling * (stackCount - 1)) * procCoefficient * 0.01f;
        }

        public string FormatChancePercentage()
        {
            return $"<style=cIsDamage>{Chance:#0}%</style>" + (ChanceStackScaling > 0 ? $" <style=cStack>(+{Chance * ChanceStackScaling:#0}% per stack)</style>" : "");
        }

        public string FormatPassiveStrengthPercentage(string textStyle)
        {
            return $"<style=c{textStyle}>{PassiveStrength:#0}%</style>" + (PassiveStackScaling > 0 ? $" <style=cStack>(+{PassiveStrength * PassiveStackScaling:#0}% per stack)</style>" : "");
        }

        public string FormatTriggeredStrengthPercentage(string textStyle)
        {
            return $"<style=c{textStyle}>{TriggeredStrength:#0}%</style>" + (TriggeredStackScaling > 0 ? $" <style=cStack>(+{TriggeredStrength * TriggeredStackScaling:#0}% per stack)</style>" : "");
        }

        public string FormatLunarStrengthPercentage(string textStyle)
        {
            return $"<style=c{textStyle}>{LunarStrength:#0}%</style>" + (LunarStackScaling > 0 ? $" <style=cStack>(+{LunarStrength * LunarStackScaling:#0}% per stack)</style>" : "");
        }

        public void InvalidateDescription()
        {
            BuiltDescription = null;
        }

        public void RegenerateDescription()
        {
            InvalidateDescription();

            if (pickupLanguageOverlay != null)
                pickupLanguageOverlay.Remove();
            if (descriptionLanguageOverlay != null)
                descriptionLanguageOverlay.Remove();

            pickupLanguageOverlay = LanguageAPI.AddOverlay(PickupLanguageToken, Description);
            descriptionLanguageOverlay = LanguageAPI.AddOverlay(DescriptionLanguageToken, Description);
        }

        public void OverrideDescription(string description)
        {
            DescriptionOverrride = description;
        }

        public abstract SpriteShape Generate();

        protected void SetTrigger(EffectTriggerType trigger)
        {
            TriggerType = trigger.Name;
            InvalidateDescription();
        }

        protected bool AddCondition(EffectCondition condition)
        {
            if (!ConditionNames.Add(condition.Name))
                return false;

            Conditions.Add(condition.GetConditionCallback(this));
            InvalidateDescription();
            return true;
        }

        protected bool AddPassiveEffect(PassiveEffect effect)
        {
            if (!PassiveEffectNames.Add(effect.Name))
                return false;

            OnPassiveEffect      += effect.GetPassiveEffectCallback(this);
            OnPassiveSpecialStat += effect.GetPassiveSpecialStatCallback(this);
            InvalidateDescription();
            return true;
        }

        protected bool AddTriggeredEffect(TriggeredEffect effect)
        {
            if (!TriggeredEffectNames.Add(effect.Name))
                return false;

            OnTriggeredEffect += effect.GetTriggeredEffectCallback(this);
            InvalidateDescription();
            return true;
        }

        public bool HasCondition(string name)
        {
            return ConditionNames.Contains(name);
        }

        public bool HasPassiveEffect(string name)
        {
            return PassiveEffectNames.Contains(name);
        }

        public bool HasTriggeredEffect(string name)
        {
            return TriggeredEffectNames.Contains(name);
        }

        protected abstract string BuildDescription();

        public delegate string DescriptionDelegate(AbstractEffects effects);
    }
}