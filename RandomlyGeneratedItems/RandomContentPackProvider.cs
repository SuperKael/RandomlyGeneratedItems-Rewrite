using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using R2API;
using RandomlyGeneratedItems.RandomEffects;
using RoR2;
using RoR2.ContentManagement;
using RoR2.ExpansionManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace RandomlyGeneratedItems
{
    public class RandomContentPackProvider : IContentPackProvider
    {
        public static readonly Dictionary<ItemTier, Color> TierColors = new()
        {
            [ItemTier.Tier1]     = new Color(0.88f, 0.89f, 0.89f),
            [ItemTier.Tier2]     = new Color(0.47f, 0.77f, 0.26f),
            [ItemTier.Tier3]     = new Color(0.89f, 0.31f, 0.19f),
            [ItemTier.Boss]      = new Color(0.78f, 0.85f, 0.16f),
            [ItemTier.VoidTier1] = new Color(0.85f, 0.28f, 0.59f),
            [ItemTier.VoidTier2] = new Color(0.85f, 0.28f, 0.59f),
            [ItemTier.VoidTier3] = new Color(0.85f, 0.28f, 0.59f),
            [ItemTier.VoidBoss]  = new Color(0.85f, 0.28f, 0.59f),
            [ItemTier.Lunar]     = new Color(0.28f, 0.88f, 0.95f)
        };

        public static readonly Color EquipmentColor = new(0.89f, 0.57f, 0.19f);

        public static readonly Dictionary<SpriteShape, Tuple<Func<int, int, bool>, Func<int, int, bool>>> ShapeDelegates = new()
        {
            [SpriteShape.Square]  = Tuple.Create<Func<int, int, bool>, Func<int, int, bool>>((x, y) => x is > 128 and < 384 && y is > 128 and < 384, (x, y) => x is > 112 and < 400 && y is > 112 and < 400),
            [SpriteShape.Rhombus] = Tuple.Create<Func<int, int, bool>, Func<int, int, bool>>((x, y) => Mathf.Abs(x - 256) + Mathf.Abs(y - 256) < 192, (x, y) => Mathf.Abs(x - 256) + Mathf.Abs(y - 256) < 208),
            [SpriteShape.Circle]  = Tuple.Create<Func<int, int, bool>, Func<int, int, bool>>((x, y) => Mathf.Pow(x - 256, 2) + Mathf.Pow(y - 256, 2) < 192 * 192, (x, y) => Mathf.Pow(x - 256, 2) + Mathf.Pow(y - 256, 2) < 208 * 208),
            [SpriteShape.Diamond] = Tuple.Create<Func<int, int, bool>, Func<int, int, bool>>((x, y) => Mathf.Abs(x - 256) * 2 + Mathf.Abs(y - 256) < 192, (x, y) => Mathf.Abs(x - 256) * 2 + Mathf.Abs(y - 256) < 208),
            [SpriteShape.Cylinder] = Tuple.Create<Func<int, int, bool>, Func<int, int, bool>>((x, y) => Mathf.Abs(y - 256) < 96 ? Mathf.Abs(x - 256) < 96 : Mathf.Pow(x - 256, 2) + Mathf.Pow(Mathf.Abs(y - 256) - 96, 2) < 96 * 96, (x, y) => Mathf.Abs(y - 256) < 96 ? Mathf.Abs(x - 256) < 112 : Mathf.Pow(x - 256, 2) + Mathf.Pow(Mathf.Abs(y - 256) - 96, 2) < 112 * 112)
        };

        public static Shader HgStandard;

        public ContentPack ContentPack = new();
        public ExpansionDef RgiExpansion;
        public ArtifactDef ArtifactFrivolity;

        public SortedDictionary<ItemTier, int> ItemTypeCounts = new();
        public int EquipmentCount;
        public bool VoidsConvertNormals;

        public readonly List<ItemDef> GeneratedItemDefs = new();
        public readonly List<EquipmentDef> GeneratedEquipmentDefs = new();
        public readonly HashSet<string> GeneratedNames = new();

        public string identifier => "RandomlyGeneratedItems";

        public RandomContentPackProvider()
        {
            HgStandard = Addressables.LoadAssetAsync<Shader>("RoR2/Base/Shaders/HGStandard.shader").WaitForCompletion();
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs contentPackArgs)
        {
            ContentPack.identifier = identifier;

            RgiExpansion = ScriptableObject.CreateInstance<ExpansionDef>();
            RgiExpansion.name = "EXPANSION_RGI";
            RgiExpansion.nameToken = RgiExpansion.name + "_NAME";
            RgiExpansion.descriptionToken = RgiExpansion.name + "_DESC";
            RgiExpansion.iconSprite = GenerateIcon(Color.green, new[] { Color.green }, SpriteShape.Rhombus);
            RgiExpansion.disabledIconSprite = GenerateIcon(Color.gray, new[] { Color.gray }, SpriteShape.Rhombus);

            LanguageAPI.Add(RgiExpansion.nameToken, "Randomly Generated Items");
            LanguageAPI.Add(RgiExpansion.descriptionToken, "Enables randomly-generated items. Note that you must fully restart the game in order to generate a new batch of items.");

            ContentPack.expansionDefs.Add(new[] { RgiExpansion });

            ArtifactFrivolity = ScriptableObject.CreateInstance<ArtifactDef>();
            ArtifactFrivolity.cachedName = "ARTIFACT_RGI_FRIVOLITY";
            ArtifactFrivolity.nameToken = ArtifactFrivolity.cachedName + "_NAME";
            ArtifactFrivolity.descriptionToken = ArtifactFrivolity.cachedName + "_DESC";
            ArtifactFrivolity.requiredExpansion = RgiExpansion;
            ArtifactFrivolity.smallIconSelectedSprite = GenerateIcon(new Color(0.9f, 0.75f, 0.9f), new[] { new Color(0.9f, 0.75f, 0.9f) }, SpriteShape.Square);
            ArtifactFrivolity.smallIconDeselectedSprite = GenerateIcon(Color.gray, new[] { Color.gray }, SpriteShape.Square);

            LanguageAPI.Add(ArtifactFrivolity.nameToken, "Artifact of Frivolity");
            LanguageAPI.Add(ArtifactFrivolity.descriptionToken, "Disables all items except for randomly-generated ones.");

            ContentPack.artifactDefs.Add(new [] { ArtifactFrivolity });

            contentPackArgs.ReportProgress(0.05f);

            ItemTypeCounts[ItemTier.Tier1] = Main.RgiConfig.Bind("Configuration", "Common Items", 20,
                "The number of common items to generate.").Value;
            ItemTypeCounts[ItemTier.Tier2] = Main.RgiConfig.Bind("Configuration", "Uncommon Items", 20, 
                "The number of uncommon items to generate.").Value;
            ItemTypeCounts[ItemTier.Tier3] = Main.RgiConfig.Bind("Configuration", "Legendary Items", 20,
                "The number of legendary items to generate.").Value;
            ItemTypeCounts[ItemTier.Boss] = Main.RgiConfig.Bind("Configuration", "Boss Items", 5,
                "The number of boss items to generate.").Value;
            ItemTypeCounts[ItemTier.VoidTier1] = Main.RgiConfig.Bind("Configuration", "Void Common Items", 3,
                "The number of void common items to generate.").Value;
            ItemTypeCounts[ItemTier.VoidTier2] = Main.RgiConfig.Bind("Configuration", "Void Uncommon Items", 3,
                "The number of void uncommon items to generate.").Value;
            ItemTypeCounts[ItemTier.VoidTier3] = Main.RgiConfig.Bind("Configuration", "Void Legendary Items", 3,
                "The number of void legendary items to generate.").Value;
            EquipmentCount = Main.RgiConfig.Bind("Configuration", "Equipment Items", 20,
                "The number of equipment items to generate.").Value;
            
            VoidsConvertNormals = Main.RgiConfig.Bind("Configuration", "Void Items Convert Normal Items", true, "Whether generated void items should convert certain generated normal items. If true, at least as many normal items as void items of each tier will always be generated.").Value;
            if (VoidsConvertNormals)
            {
                if (ItemTypeCounts[ItemTier.Tier1] < ItemTypeCounts[ItemTier.VoidTier1])
                    ItemTypeCounts[ItemTier.Tier1] = ItemTypeCounts[ItemTier.VoidTier1];
                if (ItemTypeCounts[ItemTier.Tier2] < ItemTypeCounts[ItemTier.VoidTier2])
                    ItemTypeCounts[ItemTier.Tier2] = ItemTypeCounts[ItemTier.VoidTier2];
                if (ItemTypeCounts[ItemTier.Tier3] < ItemTypeCounts[ItemTier.VoidTier3])
                    ItemTypeCounts[ItemTier.Tier3] = ItemTypeCounts[ItemTier.VoidTier3];
            }

            if (VoidsConvertNormals)
            {
                On.RoR2.Items.ContagiousItemManager.Init += orig =>
                {
                    List<ItemDef.Pair> transformations = new();

                    IEnumerator<ItemDef> tier1Items =
                        GeneratedItemDefs.Where(itemDef => itemDef.tier == ItemTier.Tier1).GetEnumerator();
                    foreach (ItemDef itemDef in GeneratedItemDefs.Where(itemDef => itemDef.tier == ItemTier.VoidTier1))
                    {
                        if (!tier1Items.MoveNext()) break;
                        transformations.Add(new ItemDef.Pair
                        {
                            itemDef1 = tier1Items.Current,
                            itemDef2 = itemDef
                        });
                    }
                    tier1Items.Dispose();

                    IEnumerator<ItemDef> tier2Items =
                        GeneratedItemDefs.Where(itemDef => itemDef.tier == ItemTier.Tier2).GetEnumerator();
                    foreach (ItemDef itemDef in GeneratedItemDefs.Where(itemDef => itemDef.tier == ItemTier.VoidTier2))
                    {
                        if (!tier2Items.MoveNext()) break;
                        transformations.Add(new ItemDef.Pair
                        {
                            itemDef1 = tier2Items.Current,
                            itemDef2 = itemDef
                        });
                    }
                    tier2Items.Dispose();

                    IEnumerator<ItemDef> tier3Items =
                        GeneratedItemDefs.Where(itemDef => itemDef.tier == ItemTier.Tier3).GetEnumerator();
                    foreach (ItemDef itemDef in GeneratedItemDefs.Where(itemDef => itemDef.tier == ItemTier.VoidTier3))
                    {
                        if (!tier3Items.MoveNext()) break;
                        transformations.Add(new ItemDef.Pair
                        {
                            itemDef1 = tier3Items.Current,
                            itemDef2 = itemDef
                        });
                    }
                    tier3Items.Dispose();

                    LanguageAPI.AddOverlay(transformations.SelectMany(
                            pair => new[] { (descToken: pair.itemDef2.pickupToken, pair.itemDef1.nameToken), (descToken: pair.itemDef2.descriptionToken, pair.itemDef1.nameToken) })
                        .ToDictionary(
                            pair => pair.descToken, 
                            pair => Language.currentLanguage.GetLocalizedStringByToken(pair.descToken) 
                                    + $"\n<style=cIsVoid>Corrupts all {Language.currentLanguage.GetLocalizedStringByToken(pair.nameToken + "_PLURAL")}</style>."));

                    ItemCatalog.itemRelationships[DLC1Content.ItemRelationshipTypes.ContagiousItem]
                        = ItemCatalog.itemRelationships[DLC1Content.ItemRelationshipTypes.ContagiousItem]
                            .AddRangeToArray(transformations.ToArray());

                    orig();
                };
            }

            contentPackArgs.ReportProgress(0.1f);
            yield return AbstractEffects.Initialize(ContentPack);

            yield return GenerateItems(contentPackArgs);
            yield return GenerateEquipments(contentPackArgs);

            contentPackArgs.ReportProgress(0.95f);
            yield return Buffs.RegisterBuffs(ContentPack);
            contentPackArgs.ReportProgress(1f);
        }

        public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs contentPackArgs)
        {
            ContentPack.Copy(ContentPack, contentPackArgs.output);
            contentPackArgs.ReportProgress(1);
            yield break;
        }

        public IEnumerator FinalizeAsync(FinalizeAsyncArgs contentPackArgs)
        {
            On.RoR2.UserProfile.HasDiscoveredPickup += (orig, self, pickupIndex) =>
            {
                if (orig(self, pickupIndex)) return true;
                if (!pickupIndex.isValid) return false;
                PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
                if (pickupDef == null) return false;
                if (pickupDef.itemIndex != ItemIndex.None)
                    return ItemCatalog.GetItemDef(pickupDef.itemIndex)?.requiredExpansion == RgiExpansion;
                if (pickupDef.equipmentIndex != EquipmentIndex.None)
                    return EquipmentCatalog.GetEquipmentDef(pickupDef.equipmentIndex)?.requiredExpansion == RgiExpansion;
                return false;
            };

            On.RoR2.UserProfile.HasViewedViewable += (orig, self, viewableName) =>
            {
                if (orig(self, viewableName)) return true;
                return viewableName != null && (viewableName.Contains("ITEM_RGI") || viewableName.Contains("EQUIPMENT_RGI"));
            };

            On.RoR2.Run.BuildDropTable += (orig, self) =>
            {
                if (RunArtifactManager.instance.IsArtifactEnabled(ArtifactFrivolity))
                {
                    self.availableItems.Clear();
                    foreach (ItemDef itemDef in GeneratedItemDefs) self.availableItems.Add(itemDef.itemIndex);
                    self.availableEquipment.Clear();
                    foreach (EquipmentDef equipmentDef in GeneratedEquipmentDefs) self.availableEquipment.Add(equipmentDef.equipmentIndex);
                }
                orig(self);
            };

            On.RoR2.ExplicitPickupDropTable.GenerateWeightedSelection += (orig, self) =>
            {
                if (RunArtifactManager.instance.IsArtifactEnabled(ArtifactFrivolity))
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    for (int i = 0; i < self.entries.Length; i++)
                    {
                        PickupIndex pickupIndex = PickupCatalog.FindPickupIndex(self.entries[i].pickupName);
                        if (pickupIndex == PickupIndex.none) continue;
                        PickupDef pickup = PickupCatalog.GetPickupDef(pickupIndex);
                        if (pickup == null) continue;
                        if (pickup.itemIndex != ItemIndex.None)
                        {
                            ItemDef item = ItemCatalog.GetItemDef(pickup.itemIndex);
                            if (item == null || item.requiredExpansion == RgiExpansion) continue;
                            ItemDef randomizedItem = RandomizeItemPickup(item.tier);
                            if (randomizedItem != null)
                            {
                                self.entries[i].pickupName = PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(randomizedItem.itemIndex))?.internalName;
                            }
                        }
                        else if (pickup.equipmentIndex != EquipmentIndex.None)
                        {
                            EquipmentDef equipment = EquipmentCatalog.GetEquipmentDef(pickup.equipmentIndex);
                            if (equipment == null || equipment.requiredExpansion == RgiExpansion) continue;
                            EquipmentDef randomizedEquipment = RandomizeEquipmentPickup(equipment.isLunar, equipment.isBoss);
                            if (randomizedEquipment != null)
                            {
                                self.entries[i].pickupName = PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(randomizedEquipment.equipmentIndex))?.internalName;
                            }
                        }
                    }
#pragma warning restore CS0618 // Type or member is obsolete
                    for (int i = 0; i < self.pickupEntries.Length; i++)
                    {
                        if (self.pickupEntries[i].pickupDef is ItemDef item)
                        {
                            ItemDef randomizedItem = RandomizeItemPickup(item.tier);
                            if (randomizedItem != null)
                            {
                                self.pickupEntries[i].pickupDef = randomizedItem;
                            }
                        }
                        else if (self.pickupEntries[i].pickupDef is EquipmentDef equipment)
                        {
                            EquipmentDef randomizedEquipment = RandomizeEquipmentPickup(equipment.isLunar, equipment.isBoss);
                            if (randomizedEquipment != null)
                            {
                                self.pickupEntries[i].pickupDef = randomizedEquipment;
                            }
                        }
                    }
                }
                orig(self);
            };

            On.RoR2.BasicPickupDropTable.IsFilterRequired += (orig, self) => orig(self) && (!self.requiredItemTags.Contains(ItemTag.HalcyoniteShrine) || !RunArtifactManager.instance.IsArtifactEnabled(ArtifactFrivolity));

            bool wasNotMoving = true;
            bool wasOnGround = true;
            On.RoR2.CharacterBody.Update += (orig, self) =>
            {
                orig(self);
                if (!self || !self.isPlayerControlled) return;
                bool needsRecalculate = false;
                if (self.GetNotMoving() != wasNotMoving)
                {
                    wasNotMoving = !wasNotMoving;
                    needsRecalculate = true;
                }
                if (self.characterMotor.lastGroundedTime < Run.FixedTimeStamp.now - 0.2f != wasOnGround)
                {
                    wasOnGround = !wasOnGround;
                    needsRecalculate = true;
                }
                if (needsRecalculate) self.RecalculateStats();
            };

            RecalculateStatsAPI.GetStatCoefficients += AbstractEffects.ApplyPassiveEffects;

            On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
            {
                orig(self);
                float maxJumpCount = self.maxJumpCount;
                AbstractEffects.ApplyPassiveSpecialStat(self,
                    "MaxJumpCount", ref maxJumpCount);
                self.maxJumpCount = (int)Math.Round(maxJumpCount);
            };

            On.RoR2.GlobalEventManager.ServerDamageDealt += (orig, report) =>
            {
                AbstractEffects.TriggerEffects("Hit", report.attackerBody, report, null);
                if (report.damageInfo.crit)
                {
                    AbstractEffects.TriggerEffects("Crit", report.attackerBody, report, null);
                }
                AbstractEffects.TriggerEffects("Hurt", report.victimBody, report, null);
                orig(report);
            };

            On.RoR2.CharacterBody.OnSkillActivated += (orig, self, skill) =>
            {
                AbstractEffects.TriggerEffects("Skill", self, new Dictionary<string, object>
                {
                    ["skill"] = skill
                });
                orig(self, skill);
            };

            On.RoR2.GlobalEventManager.OnCharacterDeath += (orig, self, damageReport) =>
            {
                AbstractEffects.TriggerEffects("Kill", damageReport.damageInfo.attacker?.GetComponent<CharacterBody>(), damageReport, null);
                if (damageReport.victimIsElite)
                {
                    AbstractEffects.TriggerEffects("EliteKill", damageReport.damageInfo.attacker?.GetComponent<CharacterBody>(), damageReport, null);
                }
                orig(self, damageReport);
            };

            On.RoR2.HealthComponent.Heal += (orig, self, amount, procChainMask, nonRegen) =>
            {
                if (nonRegen)
                {
                    Dictionary<string, object> args = new Dictionary<string, object>
                    {
                        ["amount"] = amount
                    };
                    AbstractEffects.TriggerEffects("Heal", self.body, procChainMask, args);
                    amount = (float)args["amount"];
                }
                return orig(self, amount, procChainMask, nonRegen);
            };

            On.RoR2.GlobalEventManager.OnInteractionBegin += (orig, self, interactor, interactable, interactableObject) =>
            {
                AbstractEffects.TriggerEffects("Interact", interactor.GetComponent<CharacterBody>(), new Dictionary<string, object>
                {
                    ["interactor"] = interactor,
                    ["interactable"] = interactable,
                    ["interactableObject"] = interactableObject
                });
                orig(self, interactor, interactable, interactableObject);
            };

            On.RoR2.EquipmentSlot.PerformEquipmentAction += (orig, self, equipmentDef) =>
            {
                bool success = GeneratedEquipmentDefs.Contains(equipmentDef) || orig(self, equipmentDef);
                if (!success || !NetworkServer.active) return false;
                AbstractEffects.TriggerEffects("Equipment", self.characterBody, new Dictionary<string, object>
                {
                    ["equipmentDef"] = equipmentDef
                });
                return true;
            };

            On.RoR2.Inventory.CalculateEquipmentCooldownScale += (orig, self) =>
            {
                float cooldownScale = orig(self);
                AbstractEffects.ApplyPassiveSpecialStat(self.GetComponent<CharacterMaster>()?.GetBody(),
                    "EquipmentCooldownScale", ref cooldownScale);
                return cooldownScale;
            };
            
            contentPackArgs.ReportProgress(1);
            yield break;
        }

        private IEnumerator GenerateItems(LoadStaticContentAsyncArgs contentPackArgs)
        {
            int itemNum = 0;
            int totalItems = ItemTypeCounts.Values.Sum();
            foreach (KeyValuePair<ItemTier, int> itemTypeCount in ItemTypeCounts)
            {
                contentPackArgs.ReportProgress(0.5f + 0.2f * ((float)itemNum / totalItems));
                for (int i = 0; i < itemTypeCount.Value; i++)
                {
                    yield return GenerateItem(itemTypeCount.Key);
                }
                itemNum++;
            }
            ContentPack.itemDefs.Add(GeneratedItemDefs.ToArray());
        }

        public IEnumerator GenerateItem(ItemTier tier)
        {
            ItemDef itemDef = ScriptableObject.CreateInstance<ItemDef>();

            (string itemName, string itemNamePlural, string xmlSafeItemName) = GenerateRandomItemName();

            if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(xmlSafeItemName)) throw new InvalidOperationException("Failed to generate a new item name!");


            Color color = TierColors.GetValueOrDefault(tier, Color.black);

            itemDef.name = "RGI_" + xmlSafeItemName;
            itemDef.AutoPopulateTokens();
            itemDef.requiredExpansion = RgiExpansion;
            itemDef.hidden = false;
            itemDef.tier = tier;
#pragma warning disable CS0618
            itemDef.deprecatedTier = tier;
#pragma warning restore CS0618

            ItemEffects effects = new(itemDef, Main.Rng);
            SpriteShape spriteShape = effects.Generate();

            itemDef.pickupModelPrefab = GenerateRandomItemPrefab(effects.SpriteColors ?? Array.Empty<Color>(), xmlSafeItemName, spriteShape);
            itemDef.pickupIconSprite = GenerateRandomItemIcon(color, effects.SpriteColors ?? Array.Empty<Color>(), spriteShape);

            string logEntry = GenerateRandomItemLogEntry();

            LanguageAPI.Add(itemDef.nameToken, itemName);
            LanguageAPI.Add(itemDef.nameToken + "_PLURAL", itemNamePlural);
            LanguageAPI.Add(itemDef.pickupToken, effects.Description);
            LanguageAPI.Add(itemDef.descriptionToken, effects.Description);
            LanguageAPI.Add(itemDef.loreToken, logEntry);

            if (effects.HasInactiveForm)
            {
                ItemDef inactiveDef = ScriptableObject.CreateInstance<ItemDef>();
                inactiveDef.name = itemDef.name + "INACTIVE";
                inactiveDef.AutoPopulateTokens();
                inactiveDef.requiredExpansion = RgiExpansion;
                inactiveDef.hidden = true;
                inactiveDef.tier = tier;
#pragma warning disable CS0618
                inactiveDef.deprecatedTier = tier;
#pragma warning restore CS0618
                inactiveDef.pickupModelPrefab = itemDef.pickupModelPrefab;
                inactiveDef.pickupIconSprite = GenerateInactiveIcon(itemDef.pickupIconSprite);

                LanguageAPI.Add(inactiveDef.nameToken, itemName);
                LanguageAPI.Add(inactiveDef.pickupToken, effects.Description + "\nThis item is currently inactive.");
                LanguageAPI.Add(inactiveDef.descriptionToken, effects.Description + "\nThis item is currently inactive.");
                LanguageAPI.Add(inactiveDef.loreToken, logEntry);

                effects.InactiveItem = inactiveDef;
            }

            effects.Register();

            Main.RgiLogger.LogDebug("Generated a " + tier + " item named " + itemName);
            GeneratedItemDefs.Add(itemDef);
            yield break;
        }

        private IEnumerator GenerateEquipments(LoadStaticContentAsyncArgs contentPackArgs)
        {
            for (int i = 0; i < EquipmentCount; i++)
            {
                contentPackArgs.ReportProgress(0.7f + 0.2f * ((float)i / EquipmentCount));
                yield return GenerateEquipment(false, false);
            }
            ContentPack.equipmentDefs.Add(GeneratedEquipmentDefs.ToArray());
        }

        public IEnumerator GenerateEquipment(bool isLunar, bool isBoss)
        {
            EquipmentDef equipmentDef = ScriptableObject.CreateInstance<EquipmentDef>();

            (string itemName, string itemNamePlural, string xmlSafeItemName) = GenerateRandomItemName();

            if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(xmlSafeItemName)) throw new InvalidOperationException("Failed to generate a new equipment name!");


            Color color = isLunar ? TierColors[ItemTier.Lunar] : EquipmentColor;

            equipmentDef.name = "RGI_" + xmlSafeItemName;
            equipmentDef.AutoPopulateTokens();
            equipmentDef.requiredExpansion = RgiExpansion;
            equipmentDef.isLunar = isLunar;
            equipmentDef.isBoss = isBoss;
            equipmentDef.canDrop = true;

            EquipmentEffects effects = new(equipmentDef, Main.Rng);
            SpriteShape spriteShape = effects.Generate();

            equipmentDef.pickupModelPrefab = GenerateRandomItemPrefab(effects.SpriteColors ?? Array.Empty<Color>(), xmlSafeItemName, spriteShape);
            equipmentDef.pickupIconSprite = GenerateRandomItemIcon(color, effects.SpriteColors ?? Array.Empty<Color>(), spriteShape);

            string logEntry = GenerateRandomItemLogEntry();

            LanguageAPI.Add(equipmentDef.nameToken, itemName);
            LanguageAPI.Add(equipmentDef.nameToken + "_PLURAL", itemNamePlural);
            LanguageAPI.Add(equipmentDef.pickupToken, effects.Description);
            LanguageAPI.Add(equipmentDef.descriptionToken, effects.Description);
            LanguageAPI.Add(equipmentDef.loreToken, logEntry);

            if (effects.HasInactiveForm)
            {
                EquipmentDef inactiveDef = ScriptableObject.CreateInstance<EquipmentDef>();
                inactiveDef.name = equipmentDef.name + "_INACTIVE";
                inactiveDef.AutoPopulateTokens();
                inactiveDef.requiredExpansion = RgiExpansion;
                inactiveDef.isLunar = isLunar;
                inactiveDef.isBoss = isBoss;
                inactiveDef.canDrop = false;
                inactiveDef.pickupModelPrefab = equipmentDef.pickupModelPrefab;
                inactiveDef.pickupIconSprite = GenerateInactiveIcon(equipmentDef.pickupIconSprite);

                LanguageAPI.Add(inactiveDef.nameToken, itemName);
                LanguageAPI.Add(inactiveDef.pickupToken, effects.Description + "\nThis equipment is currently inactive.");
                LanguageAPI.Add(inactiveDef.descriptionToken, effects.Description + "\nThis equipment is currently inactive.");
                LanguageAPI.Add(inactiveDef.loreToken, logEntry);

                effects.InactiveEquipment = inactiveDef;
            }

            effects.Register();

            Main.RgiLogger.LogDebug("Generated a " + (isLunar ? "lunar " : "") + (isBoss ? "boss " : "") + "equipment named " + itemName);
            GeneratedEquipmentDefs.Add(equipmentDef);
            yield break;
        }

        private (string itemName, string itemNamePlural, string xmlSafeItemName) GenerateRandomItemName()
        {
            int attempts = 0;
            while (attempts < 25)
            {
                var prefixRng = Main.Rng.RangeInt(0, NameSystem.ItemNamePrefix.Count);
                var nameRng = Main.Rng.RangeInt(0, NameSystem.ItemName.Count);
                string prefix = NameSystem.ItemNamePrefix[prefixRng] + " ";
                string name = prefix + NameSystem.ItemName[nameRng];
                string namePlural = prefix + NameSystem.ItemNamePlural[nameRng];
                string xmlSafeItemName = name.ToUpper().Replace(" ", "_").Replace("'", "").Replace("&", "AND");
                if (GeneratedNames.Add(xmlSafeItemName)) return (name, namePlural, xmlSafeItemName);
                attempts++;
            }

            return (null, null, null);
        }

        public static string GenerateRandomItemLogEntry()
        {
            string log = "";
            int logLength = Main.Rng.RangeInt(0, 120);
            for (int i = 0; i < logLength; i++)
            {
                int logRng = Main.Rng.RangeInt(0, NameSystem.LogDesc.Count);
                log += NameSystem.LogDesc[logRng];
                if (i % Main.Rng.RangeInt(8, 14) == 0)
                {
                    log += ". ";
                }
                else
                {
                    log += " ";
                }
            }

            return log;
        }

        public static GameObject GenerateRandomItemPrefab(Color[] coreColors, string xmlSafeItemName, SpriteShape shape, bool randomShade = true, Vector2[] randomShadeOffsets = null)
        {
            GameObject prefab = new GameObject();
            GameObject model, scaledModel;

            switch (shape)
            {
                case SpriteShape.Square:
                    model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    break;
                case SpriteShape.Rhombus:
                    model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    model.transform.Rotate(35.25f, 0f, 45f);
                    break;
                case SpriteShape.Circle:
                    model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    break;
                case SpriteShape.Diamond:
                    model = new GameObject
                    {
                        transform =
                        {
                            localScale = new Vector3(0.5f, 1f, 0.5f)
                        }
                    };
                    scaledModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    scaledModel.transform.Rotate(35.25f, 0f, 45f);
                    scaledModel.transform.SetParent(model.transform);
                    break;
                case SpriteShape.Cylinder:
                    model = new GameObject
                    {
                        transform =
                        {
                            localScale = new Vector3(0.5f, 1f, 0.5f)
                        }
                    };
                    scaledModel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    scaledModel.transform.SetParent(model.transform);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape));
            }

            model.transform.SetParent(prefab.transform);

            Collider collider = prefab.GetComponentInChildren<Collider>();
            collider.gameObject.layer = LayerIndex.pickups.intVal;
            collider.isTrigger = true;

            Material mat = new(HgStandard);
            Texture2D tex = new(512, 512);
            Color[] pixels = new Color[512 * 512];
            Vector2[] offsets = randomShadeOffsets ?? new Vector2[coreColors.Length];
            float[] samples = new float[coreColors.Length];

            if (!randomShade)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = 1;
                }
            }

            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    int pixelIndex = y * tex.width + x;
                    float sampleSum;
                    if (randomShade)
                    {
                        sampleSum = 0;
                        float noiseX = (float)x / tex.width * 4;
                        float noiseY = (float)y / tex.height * 4;
                        for (int i = 0; i < samples.Length; i++)
                        {
                            samples[i] = Mathf.PerlinNoise(offsets[i].x + noiseX, offsets[i].y + noiseY);
                            sampleSum += samples[i];
                        }
                    }
                    else
                    {
                        sampleSum = samples.Length;
                    }

                    pixels[pixelIndex] = Color.black;
                    for (int i = 0; i < samples.Length; i++)
                    {
                        Color sampleColor = coreColors[i] * samples[i];
                        if (sampleSum > 1) sampleColor /= sampleSum;
                        pixels[pixelIndex] += sampleColor;
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            mat.color = Color.white;
            mat.mainTexture = tex;

            foreach (MeshRenderer mr in model.GetComponentsInChildren<MeshRenderer>())
            {
                mr.sharedMaterial = mat;
            }

            return prefab.InstantiateClone($"{xmlSafeItemName}-model", false);
        }

        public static Sprite GenerateRandomItemIcon(Color borderColor, Color[] coreColors, SpriteShape shape, ulong? seed = null)
        {
            Xoroshiro128Plus rng = seed.HasValue ? new Xoroshiro128Plus(seed.Value) : new Xoroshiro128Plus(Main.Rng);

            Vector2[] randomShadeOffsets = new Vector2[coreColors.Length];
            for (int i = 0; i < randomShadeOffsets.Length; i++)
                randomShadeOffsets[i] = new Vector2(rng.RangeFloat(-10000, 10000), rng.RangeFloat(-10000, 10000));
            Sprite icon = GenerateIcon(borderColor, coreColors, shape, true, randomShadeOffsets);

            UnityEngine.Object.DontDestroyOnLoad(icon.texture);
            UnityEngine.Object.DontDestroyOnLoad(icon);

            return icon;
        }

        public static Sprite GenerateIcon(Color borderColor, Color[] coreColors, SpriteShape shape,
            bool randomShade = true, Vector2[] randomShadeOffsets = null)
        {
            return GenerateIcon(borderColor, coreColors, ShapeDelegates[shape].Item1, ShapeDelegates[shape].Item2,
                randomShade, randomShadeOffsets);
        }

        public static Sprite GenerateIcon(Color borderColor, Color[] coreColors, Func<int, int, bool> shapeDelegate, Func<int, int, bool> borderDelegate, bool randomShade = true, Vector2[] randomShadeOffsets = null)
        {
            Texture2D tex = new(512, 512);

            Color[] pixels = new Color[512 * 512];
            Vector2[] offsets = randomShadeOffsets ?? new Vector2[coreColors.Length];
            float[] samples = new float[coreColors.Length];

            if (!randomShade)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = 1;
                }
            }

            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    int pixelIndex = y * tex.width + x;
                    float sampleSum;
                    if (randomShade)
                    {
                        sampleSum = 0;
                        float noiseX = (float)x / tex.width * 4;
                        float noiseY = (float)y / tex.height * 4;
                        for (int i = 0; i < samples.Length; i++)
                        {
                            samples[i] = Mathf.PerlinNoise(offsets[i].x + noiseX, offsets[i].y + noiseY);
                            sampleSum += samples[i];
                        }
                    }
                    else
                    {
                        sampleSum = samples.Length;
                    }

                    if (shapeDelegate(x, y))
                    {
                        pixels[pixelIndex] = Color.black;
                        for (int i = 0; i < samples.Length; i++)
                        {
                            Color sampleColor = coreColors[i] * samples[i];
                            if (sampleSum > 1) sampleColor /= sampleSum;
                            pixels[pixelIndex] += sampleColor;
                        }
                    }
                    else if (borderDelegate(x, y))
                    {
                        pixels[pixelIndex] = borderColor;
                    }
                    else
                    {
                        pixels[pixelIndex] = new Color(0, 0, 0, 0);
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        public static Sprite GenerateInactiveIcon(Sprite activeIcon)
        {
            Texture2D tex = new(activeIcon.texture.width, activeIcon.texture.height);
            tex.SetAllPixels32(activeIcon.texture.GetPixels32(0).Select(c =>
            {
                float cVal = c.r / 255f * 0.2126f + c.g / 255f * 0.7152f + c.b / 255f * 0.0722f;
                cVal = cVal <= 0.0031308 ? cVal * 12.92f : Mathf.Pow(cVal, 1 / 2.4f) * 1.055f - 0.055f;
                byte cByte = (byte) Mathf.RoundToInt(cVal * 255);
                return new Color32(cByte, cByte, cByte, c.a);
            }).ToArray(), 0);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        private ItemDef RandomizeItemPickup(ItemTier tier)
        {
            if (!ItemTypeCounts.TryGetValue(tier, out int tierCount) || tierCount <= 0) return null;
            int itemIndex = new Xoroshiro128Plus(Main.Rng).RangeInt(0, tierCount);
            return GeneratedItemDefs.FirstOrDefault(itemDef => itemDef.tier == tier && itemIndex-- == 0);
        }

        private EquipmentDef RandomizeEquipmentPickup(bool isLunar, bool isBoss)
        {
            if (EquipmentCount == 0) return null;
            int equipmentIndex;
            int matchingEquipmentCount = GeneratedEquipmentDefs.Count(equipmentDef => equipmentDef.isLunar == isLunar && equipmentDef.isBoss == isBoss);
            if (matchingEquipmentCount > 0)
            {
                equipmentIndex = new Xoroshiro128Plus(Main.Rng).RangeInt(0, matchingEquipmentCount);
                return GeneratedEquipmentDefs.FirstOrDefault(equipmentDef => equipmentDef.isLunar == isLunar && equipmentDef.isBoss == isBoss && equipmentIndex-- == 0);
            }
            matchingEquipmentCount = GeneratedEquipmentDefs.Count(equipmentDef => equipmentDef.isLunar == isLunar);
            if (isLunar && matchingEquipmentCount <= 0) matchingEquipmentCount = GeneratedEquipmentDefs.Count(equipmentDef => !equipmentDef.isLunar);
            if (matchingEquipmentCount <= 0) return null;
            equipmentIndex = new Xoroshiro128Plus(Main.Rng).RangeInt(0, matchingEquipmentCount);
            return GeneratedEquipmentDefs.FirstOrDefault(equipmentDef => equipmentDef.isLunar == isLunar && equipmentIndex-- == 0);
        }
    }
}
