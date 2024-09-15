using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RoR2;

namespace RandomlyGeneratedItems.RandomEffects
{
    public readonly struct EffectCondition
    {
        public static readonly Dictionary<string, EffectCondition> RegisteredConditions = new();

        public readonly string Name;
        public readonly float StrengthModifier;
        public readonly Func<AbstractEffects, ConditionCallback> ConditionCallbackProvider;
        public readonly AbstractEffects.DescriptionDelegate DescriptionDelegate;
        public readonly int MinimumGrade;
        public readonly string[] ExclusiveConditions;

        public static IEnumerator Initialize()
        {
            RegisterCondition("HasShield", 1.5f, _ => "While you have a <style=cIsHealing>shield</style>, ", _ => body => body.healthComponent.shield > 0,
                "UnderHalfHP");
            RegisterCondition("HasBarrier", 1.5f, _ => "While you have a <style=cIsHealing>barrier</style>, ", _ => body => body.healthComponent.barrier > 0,
                "UnderHalfHP");
            RegisterCondition("OutOfDanger", 2f, _ => "While <style=cIsUtility>out of danger</style>, ", _ => body => body.outOfDanger);
            RegisterCondition("OutOfCombat", 4f, _ => "While <style=cIsUtility>out of combat</style>, ", _ => body => body.outOfCombat);
            RegisterCondition("Moving", 1.5f, _ => "While <style=cIsUtility>moving</style>, ", _ => body => !body.GetNotMoving(),
                "NotMoving");
            RegisterCondition("NotMoving", 4f, _ => "After standing still for <style=cIsHealing>1</style> second, ", _ => body => body.GetNotMoving(),
                "Moving", "Midair");
            RegisterCondition("UnderHalfHP", 4f, _ => "While below <style=cIsHealth>50% health</style>, ", _ => body => body.healthComponent.combinedHealthFraction <= 0.5f,
                "HasShield", "HasBarrier", "AtFullHP");
            RegisterCondition("AtFullHP", 1.5f, _ => "While at <style=cIsHealth>full health</style>, ", _ => body => body.healthComponent.combinedHealthFraction >= 1f,
                "UnderHalfHP");
            RegisterCondition("Midair", 2f, _ => "While <style=cIsUtility>midair</style>, ", _ => body => body.characterMotor.lastGroundedTime >= Run.FixedTimeStamp.now + 0.2f,
                "NotMoving");
            RegisterCondition("Debuffed", 2f, _ => "While <style=cIsHealth>debuffed</style>, ", _ => body => body.activeBuffsList.Any(index => BuffCatalog.GetBuffDef(index).isDebuff));
            RegisterCondition("First3Minutes", 2f, _ => "For the first <style=cIsUtility>3 minutes</style> every stage, ", _ => _ => Stage.instance && Run.instance.fixedTime - Stage.instance.entryTime.t <= 180);
            RegisterCondition("TeleporterEvent", 2f, _ => "During the <style=cIsUtility>Teleporter Event</style>, ", _ => _ => TeleporterInteraction.instance && TeleporterInteraction.instance.isCharging);

            yield break;
        }

        public static EffectCondition RegisterCondition(string name, float strengthModifier, AbstractEffects.DescriptionDelegate descriptionDelegate,
            Func<AbstractEffects, ConditionCallback> conditionCallbackProvider, params string[] exclusiveConditions)
        {
            return RegisterCondition(name, strengthModifier, descriptionDelegate, conditionCallbackProvider, 0,
                exclusiveConditions);
        }

        public static EffectCondition RegisterCondition(string name, float strengthModifier, AbstractEffects.DescriptionDelegate descriptionDelegate, Func<AbstractEffects, ConditionCallback> conditionCallbackProvider, int minimumGrade, params string[] exclusiveConditions)
        {
            EffectCondition effectCondition = new(name, strengthModifier, descriptionDelegate, conditionCallbackProvider, minimumGrade, exclusiveConditions);
            RegisteredConditions[name] = effectCondition;
            return effectCondition;
        }

        public EffectCondition(string name, float strengthModifier, AbstractEffects.DescriptionDelegate descriptionDelegate, Func<AbstractEffects, ConditionCallback> conditionCallbackProvider, int minimumGrade, params string[] exclusiveConditions)
        {
            Name = name;
            StrengthModifier = strengthModifier;
            DescriptionDelegate = descriptionDelegate;
            ConditionCallbackProvider = conditionCallbackProvider;
            MinimumGrade = minimumGrade;
            ExclusiveConditions = exclusiveConditions;
        }

        public ConditionCallback GetConditionCallback(AbstractEffects effects)
        {
            return ConditionCallbackProvider(effects);
        }

        public delegate bool ConditionCallback(CharacterBody body);
    }
}