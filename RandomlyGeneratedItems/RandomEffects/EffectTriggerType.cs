using System.Collections;
using System.Collections.Generic;

namespace RandomlyGeneratedItems.RandomEffects
{
    public readonly struct EffectTriggerType
    {
        public static readonly Dictionary<string, EffectTriggerType> RegisteredTriggerTypes = new();

        public readonly string Name;
        public readonly float StrengthModifier;
        public readonly AbstractEffects.DescriptionDelegate DescriptionDelegate;
        public readonly string[] ExclusiveConditions;

        public static IEnumerator Initialize()
        {
            RegisterTriggerType("Hit", 0.5f, effect => effect.Chance >= 100
                ? "On hit, "
                : $"Gain a {effect.FormatChancePercentage()} chance on hit to ");
            RegisterTriggerType("Crit", 1f, effect => effect.Chance >= 100
                ? "On <style=cIsDamage>critical strike</style>, "
                : $"Gain a {effect.FormatChancePercentage()} chance on <style=cIsDamage>critical strike</style> to ");
            RegisterTriggerType("Kill", 2f, effect => effect.Chance >= 100
                ? "On kill, "
                : $"Gain a {effect.FormatChancePercentage()} chance on kill to ",
                "OutOfCombat");
            RegisterTriggerType("EliteKill", 4f, effect => effect.Chance >= 100
                ? "Upon killing an <style=cIsDamage>elite</style>, "
                : $"Gain a {effect.FormatChancePercentage()} chance to, upon killing an <style=cIsDamage>elite</style>, ",
                "OutOfCombat");
            RegisterTriggerType("Heal", 1f, effect => effect.Chance >= 100
                ? "Upon getting healed, "
                : $"Gain a {effect.FormatChancePercentage()} chance upon getting healed to ");
            RegisterTriggerType("Hurt", 2f, effect => effect.Chance >= 100
                ? "Upon <style=cDeath>taking damage</style>, "
                : $"Gain a {effect.FormatChancePercentage()} chance upon <style=cDeath>taking damage</style> to ");
            RegisterTriggerType("Skill", 0.5f, effect => effect.Chance >= 100
                ? "On skill use, "
                : $"Gain a {effect.FormatChancePercentage()} chance on skill use to ");
            RegisterTriggerType("Equipment", 4f, effect => effect.Chance >= 100
                ? "On equipment use, "
                : $"Gain a {effect.FormatChancePercentage()} chance on equipment use to ");
            RegisterTriggerType("Interact", 8f, effect => effect.Chance >= 100
                ? "Upon activating an interactable, "
                : $"Gain a {effect.FormatChancePercentage()} chance upon activating an interactable to ");

            yield break;
        }

        public static EffectTriggerType RegisterTriggerType(string name, float strengthModifier,
            AbstractEffects.DescriptionDelegate descriptionDelegate, params string[] exclusiveConditions)
        {
            EffectTriggerType effectTriggerType = new(name, strengthModifier, descriptionDelegate, exclusiveConditions);
            RegisteredTriggerTypes[name] = effectTriggerType;
            AbstractEffects.TriggerTypeMap[name] = new List<string>();
            return effectTriggerType;
        }

        public EffectTriggerType(string name, float strengthModifier, AbstractEffects.DescriptionDelegate descriptionDelegate, string[] exclusiveConditions)
        {
            Name = name;
            StrengthModifier = strengthModifier;
            DescriptionDelegate = descriptionDelegate;
            ExclusiveConditions = exclusiveConditions;
        }
    }
}