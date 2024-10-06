using System;
using System.Collections;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RandomlyGeneratedItems.RandomEffects
{
    public readonly struct SpawnableInteractable
    {
        public static readonly Dictionary<string, SpawnableInteractable> RegisteredInteractables = new();

        public readonly string Name;
        public readonly float CostModifier;
        public readonly float CooldownModifier;
        public readonly InteractableSpawnDelegate SpawnDelegate;
        public readonly AbstractEffects.DescriptionDelegate DescriptionDelegate;
        public readonly int MinimumGrade;

        public static IEnumerator Initialize()
        {
            RegisterInteractableSpawnCard("GunnerDrone", 2f, 1f, _ => "a <style=cIsDamage>Broken Gunner Drone</style>", "RoR2/Base/Drones/iscBrokenDrone1.asset");
            RegisterInteractableSpawnCard("HealingDrone", 2f, 1f, _ => "a <style=cIsDamage>Broken Healing Drone</style>", "RoR2/Base/Drones/iscBrokenDrone2.asset");
            RegisterInteractableSpawnCard("EquipmentDrone", 2f, 1f, _ => "a <style=cIsDamage>Broken Equipment Drone</style>", "RoR2/Base/Drones/iscBrokenEquipmentDrone.asset");
            RegisterInteractableSpawnCard("GunnerTurret", 2f, 1f, _ => "a <style=cIsDamage>Broken Turret</style>", "RoR2/Base/Drones/iscBrokenTurret1.asset");
            RegisterInteractableSpawnCard("Barrel", 1f, 0.5f, _ => "a <style=cIsUtility>Barrel</style>", "RoR2/Base/Barrel1/iscBarrel1.asset");
            RegisterInteractableSpawnCard("SmallChest", 4f, 2f, _ => "a <style=cIsDamage>Small Chest</style>", "RoR2/Base/Chest1/iscChest1.asset");
            RegisterInteractableSpawnCard("LargeChest", 4f, 4f, _ => "a <style=cIsHealing>Large Chest</style>", "RoR2/Base/Chest2/iscChest2.asset");
            RegisterInteractableSpawnCard("LegendaryChest", 4f, 8f, _ => "a <style=cDeath>Legendary Chest</style>", "RoR2/Base/GoldChest/iscGoldChest.asset");
            RegisterInteractableSpawnCard("EquipmentBarrel", 2f, 2f, _ => "an <style=cIsHealth>Equipment Barrel</style>", "RoR2/Base/EquipmentBarrel/iscEquipmentBarrel.asset");
            // Maybe don't spawn lunar pods while random lunar items don't exist
            //RegisterInteractableSpawnCard("LunarPod", 1f, 1f, _ => "a <style=cLunarObjective>Lunar Pod</style>", "RoR2/Base/LunarChest/iscLunarChest.asset");
            RegisterInteractableSpawnCard("AdaptiveChest", 4f, 4f, _ => "an <style=cIsUtility>Adaptive Chest</style>", "RoR2/Base/CasinoChest/iscCasinoChest.asset");
            RegisterInteractableSpawnCard("SmallDamageChest", 4f, 2f, _ => "a <style=cIsDamage>Small Damage Chest</style>", "RoR2/Base/CategoryChest/iscCategoryChestDamage.asset");
            RegisterInteractableSpawnCard("SmallHealingChest", 4f, 2f, _ => "a <style=cIsDamage>Small Healing Chest</style>", "RoR2/Base/CategoryChest/iscCategoryChestHealing.asset");
            RegisterInteractableSpawnCard("SmallUtilityChest", 4f, 2f, _ => "a <style=cIsDamage>Small Utility Chest</style>", "RoR2/Base/CategoryChest/iscCategoryChestUtility.asset");
            RegisterInteractableSpawnCard("BloodShrine", 1.5f, 1f, _ => "a <style=cDeath>Shrine of Blood</style>", "RoR2/Base/ShrineBlood/iscShrineBlood.asset");
            RegisterInteractableSpawnCard("MountainShrine", 1f, 1f, _ => "a <style=cIsUtility>Shrine of the Mountain</style>", "RoR2/Base/ShrineBoss/iscShrineBoss.asset");
            RegisterInteractableSpawnCard("ChanceShrine", 2f, 2f, _ => "a <style=cIsDamage>Shrine of Chance</style>", "RoR2/Base/ShrineChance/iscShrineChance.asset");
            RegisterInteractableSpawnCard("CombatShrine", 1f, 1f, _ => "a <style=cWorldEvent>Shrine of Combat</style>", "RoR2/Base/ShrineCombat/iscShrineCombat.asset");
            RegisterInteractableSpawnCard("HealingShrine", 2f, 4f, _ => "a <style=cIsHealing>Shrine of the Woods</style>", "RoR2/Base/ShrineHealing/iscShrineHealing.asset");
            RegisterInteractableSpawnCard("CommonMultishop", 4f, 3f, _ => "a <style=cIsDamage>Common Multishop</style>", "RoR2/Base/TripleShop/iscTripleShop.asset");
            RegisterInteractableSpawnCard("UncommonMultishop", 4f, 6f, _ => "an <style=cIsHealing>Uncommon Multishop</style>", "RoR2/Base/TripleShopEquipment/iscTripleShopEquipment.asset");
            RegisterInteractableSpawnCard("EquipmentMultishop", 2f, 3f, _ => "an <style=cIsHealth>Equipment Multishop</style>", "RoR2/Base/TripleShopLarge/iscTripleShopLarge.asset");

            RegisterInteractableSpawnCard("LargeDamageChest", 4f, 4f, _ => "a <style=cIsHealing>Large Damage Chest</style>", "RoR2/DLC1/CategoryChest2/iscCategoryChest2Damage.asset");
            RegisterInteractableSpawnCard("LargeHealingChest", 4f, 4f, _ => "a <style=cIsHealing>Large Healing Chest</style>", "RoR2/DLC1/CategoryChest2/iscCategoryChest2Healing.asset");
            RegisterInteractableSpawnCard("LargeUtilityChest", 4f, 4f, _ => "a <style=cIsHealing>Large Utility Chest</style>", "RoR2/DLC1/CategoryChest2/iscCategoryChest2Utility.asset");
            RegisterInteractableSpawnCard("VoidStalk", 1f, 0.5f, _ => "a <style=cIsVoid>Void Stalk</style>", "RoR2/DLC1/VoidCoinBarrel/iscVoidCoinBarrel.asset");
            RegisterInteractableSpawnCard("VoidCradle", 1.5f, 4f, _ => "a <style=cIsVoid>Void Cradle</style>", "RoR2/DLC1/VoidChest/iscVoidChest.asset");
            RegisterInteractableSpawnCard("VoidPotential", 4f, 4f, _ => "a <style=cIsVoid>Void Potential</style>", "RoR2/DLC1/VoidTriple/iscVoidTriple.asset");
            RegisterInteractableSpawnCard("CrashedMultishop", 1f, 8f, _ => "a <style=cIsHealing>Crashed Multishop</style>", "RoR2/DLC1/FreeChest/iscFreeChest.asset");

            yield break;
        }

        public static (SpawnableInteractable spawnableEffectPayload, InteractableSpawnCard spawnCard) RegisterInteractableSpawnCard(string name, float costModifier, float cooldownModifier, AbstractEffects.DescriptionDelegate descriptionDelegate, string key, int minimumGrade = 0)
        {
            InteractableSpawnCard spawnCard = Addressables.LoadAssetAsync<InteractableSpawnCard>(key).WaitForCompletion();
            return (RegisterInteractableSpawnDelegate(name, costModifier, cooldownModifier,
                (character, _, origin) =>
                {
                    GameObject interactableObject = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(spawnCard, new DirectorPlacementRule
                    {
                        spawnOnTarget = character.transform,
                        position = origin,
                        placementMode = DirectorPlacementRule.PlacementMode.NearestNode,
                        minDistance = 20f,
                        maxDistance = 100f,
                        preventOverhead = true
                    }, RoR2Application.rng));

                    if (Math.Abs(costModifier - 1f) > 0)
                    {
                        foreach (PurchaseInteraction purchaseInteraction in
                                 interactableObject.GetComponentsInChildren<PurchaseInteraction>())
                        {
                            purchaseInteraction.cost = Mathf.RoundToInt(purchaseInteraction.cost * costModifier);
                        }
                    }
                }, descriptionDelegate, minimumGrade), spawnCard);
        }

        public static SpawnableInteractable RegisterInteractableSpawnDelegate(string name, float costModifier, float cooldownModifier,
            InteractableSpawnDelegate spawnDelegate, AbstractEffects.DescriptionDelegate descriptionDelegate,
            int minimumGrade = 0)
        {
            SpawnableInteractable spawnableEffectPayload = new(name, costModifier, cooldownModifier, spawnDelegate
                , descriptionDelegate, minimumGrade);
            RegisteredInteractables[name] = spawnableEffectPayload;
            return spawnableEffectPayload;
        }

        public SpawnableInteractable(string name, float costModifier, float cooldownModifier, InteractableSpawnDelegate spawnDelegate, AbstractEffects.DescriptionDelegate descriptionDelegate, int minimumGrade)
        {
            Name = name;
            CostModifier = costModifier;
            CooldownModifier = cooldownModifier;
            SpawnDelegate = spawnDelegate;
            DescriptionDelegate = descriptionDelegate;
            MinimumGrade = minimumGrade;
        }

        public void SpawnInteractable(CharacterBody character, AbstractEffects effects, Vector3 origin)
        {
            SpawnDelegate(character, effects, origin);
        }

        public delegate void InteractableSpawnDelegate(CharacterBody character, AbstractEffects effects, Vector3 origin);
    }
}