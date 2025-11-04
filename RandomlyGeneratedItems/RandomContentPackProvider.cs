using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using R2API;
using RandomlyGeneratedItems.RandomEffects;
using RandomlyGeneratedItems.Utilities;
using Rewired.Utils;
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
        public const int RandomSpriteResolution = 512;
        public const int RandomSpriteNoiseGranularity = 4;
        public const int RandomSpriteNoiseResolution = RandomSpriteResolution / RandomSpriteNoiseGranularity;
        public const float RandomSpriteNoiseScale = 4;

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

        private static Shader HgStandard;
        private static FastNoiseLite Noise;
        public static readonly Queue<Action> AsyncTaskFinalizers = new();

        public ContentPack ContentPack = new();
        public ExpansionDef RgiExpansion;
        public ArtifactDef ArtifactFrivolity;

        public SortedDictionary<ItemTier, int> ItemTypeCounts = new();
        public int EquipmentCount;
        public int LunarEquipmentCount;
        public bool VoidsConvertNormals;

        public readonly List<ItemDef> GeneratedItemDefs = new();
        public readonly Dictionary<ItemDef, ItemEffects> GeneratedItemEffects = new();

        public readonly List<EquipmentDef> GeneratedEquipmentDefs = new();
        public readonly Dictionary<EquipmentDef, EquipmentEffects> GeneratedEquipmentEffects = new();
        public readonly HashSet<string> GeneratedNames = new();

        public string identifier => "RandomlyGeneratedItems";

        public RandomContentPackProvider()
        {
            HgStandard = Addressables.LoadAssetAsync<Shader>("RoR2/Base/Shaders/HGStandard.shader").WaitForCompletion();
            Noise = new FastNoiseLite(Main.Rng.nextInt);
            Noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            Noise.SetFrequency(RandomSpriteNoiseScale / RandomSpriteNoiseResolution);
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs contentPackArgs)
        {
            ContentPack.identifier = identifier;

            RgiExpansion = ScriptableObject.CreateInstance<ExpansionDef>();
            RgiExpansion.name = "EXPANSION_RGI";
            RgiExpansion.nameToken = RgiExpansion.name + "_NAME";
            RgiExpansion.descriptionToken = RgiExpansion.name + "_DESC";
            RgiExpansion.iconSprite = GenerateIcon(new[] { Color.green }, Color.green, SpriteShape.Rhombus);
            RgiExpansion.disabledIconSprite = GenerateIcon(new[] { Color.gray }, Color.gray, SpriteShape.Rhombus);

            LanguageAPI.Add(RgiExpansion.nameToken, "Randomly Generated Items");
            LanguageAPI.Add(RgiExpansion.descriptionToken, "Enables randomly-generated items. Note that you must fully restart the game in order to generate a new batch of items.");

            ContentPack.expansionDefs.Add(new[] { RgiExpansion });

            ArtifactFrivolity = ScriptableObject.CreateInstance<ArtifactDef>();
            ArtifactFrivolity.cachedName = "ARTIFACT_RGI_FRIVOLITY";
            ArtifactFrivolity.nameToken = ArtifactFrivolity.cachedName + "_NAME";
            ArtifactFrivolity.descriptionToken = ArtifactFrivolity.cachedName + "_DESC";
            ArtifactFrivolity.requiredExpansion = RgiExpansion;
            ArtifactFrivolity.smallIconSelectedSprite = GenerateIcon(new[] { new Color(0.9f, 0.75f, 0.9f) }, new Color(0.9f, 0.75f, 0.9f), SpriteShape.Square);
            ArtifactFrivolity.smallIconDeselectedSprite = GenerateIcon(new[] { Color.gray }, Color.gray, SpriteShape.Square);

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
            ItemTypeCounts[ItemTier.Lunar] = Main.RgiConfig.Bind("Configuration", "Lunar Items", 20,
                "The number of lunar items to generate.").Value;
            EquipmentCount = Main.RgiConfig.Bind("Configuration", "Equipment Items", 20,
                "The number of equipment items to generate.").Value;
            LunarEquipmentCount = Main.RgiConfig.Bind("Configuration", "Lunar Equipment Items", 10,
                "The number of lumar equipment items to generate.").Value;


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
                        GeneratedItemEffects.Keys.Where(itemDef => itemDef.tier == ItemTier.Tier1).GetEnumerator();
                    foreach (KeyValuePair<ItemDef, ItemEffects> item in GeneratedItemEffects.Where(itemDef => itemDef.Key.tier == ItemTier.VoidTier1))
                    {
                        if (!tier1Items.MoveNext()) break;
                        transformations.Add(new ItemDef.Pair
                        {
                            itemDef1 = tier1Items.Current,
                            itemDef2 = item.Key
                        });
                        item.Value.VoidCorruptsItemNameToken = tier1Items.Current.nameToken + "_PLURAL";
                        item.Value.RegenerateDescription();
                    }
                    tier1Items.Dispose();

                    IEnumerator<ItemDef> tier2Items =
                        GeneratedItemEffects.Keys.Where(itemDef => itemDef.tier == ItemTier.Tier2).GetEnumerator();
                    foreach (KeyValuePair<ItemDef, ItemEffects> item in GeneratedItemEffects.Where(itemDef => itemDef.Key.tier == ItemTier.VoidTier2))
                    {
                        if (!tier2Items.MoveNext()) break;
                        transformations.Add(new ItemDef.Pair
                        {
                            itemDef1 = tier2Items.Current,
                            itemDef2 = item.Key
                        });
                        item.Value.VoidCorruptsItemNameToken = tier2Items.Current.nameToken + "_PLURAL";
                        item.Value.RegenerateDescription();
                    }
                    tier2Items.Dispose();

                    IEnumerator<ItemDef> tier3Items =
                        GeneratedItemEffects.Keys.Where(itemDef => itemDef.tier == ItemTier.Tier3).GetEnumerator();
                    foreach (KeyValuePair<ItemDef, ItemEffects> item in GeneratedItemEffects.Where(itemDef => itemDef.Key.tier == ItemTier.VoidTier3))
                    {
                        if (!tier3Items.MoveNext()) break;
                        transformations.Add(new ItemDef.Pair
                        {
                            itemDef1 = tier3Items.Current,
                            itemDef2 = item.Key
                        });
                        item.Value.VoidCorruptsItemNameToken = tier3Items.Current.nameToken + "_PLURAL";
                        item.Value.RegenerateDescription();
                    }
                    tier3Items.Dispose();

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
                if (!RunArtifactManager.instance.IsArtifactEnabled(ArtifactFrivolity))
                {
                    orig(self);
                    return;
                }

                self.availableItems.Clear();
                foreach (ItemDef itemDef in GeneratedItemEffects.Keys) self.availableItems.Add(itemDef.itemIndex);
                self.availableEquipment.Clear();
                foreach (EquipmentDef equipmentDef in GeneratedEquipmentEffects.Keys) self.availableEquipment.Add(equipmentDef.equipmentIndex);

                orig(self);
            };

            On.RoR2.ExplicitPickupDropTable.GenerateWeightedSelection += (orig, self) =>
            {
                if (!RunArtifactManager.instance.IsArtifactEnabled(ArtifactFrivolity))
                {
                    orig(self);
                    return;
                }

#pragma warning disable CS0618 // Type or member is obsolete
                for (int i = 0; i < self.entries.Length; i++)
                {
                    PickupIndex? pickupIndex = FrivolizePickup(PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(self.entries[i].pickupName)));
                    if (pickupIndex.HasValue)
                    {
                        self.entries[i].pickupName = PickupCatalog.GetPickupDef(pickupIndex.Value)?.internalName;
                    }
                }
#pragma warning restore CS0618 // Type or member is obsolete
                for (int i = 0; i < self.pickupEntries.Length; i++)
                {
                    if (self.pickupEntries[i].pickupDef is ItemDef item)
                    {
                        if (item.requiredExpansion == RgiExpansion) continue;
                        ItemDef randomizedItem = RandomizeItemPickup(item.tier);
                        if (randomizedItem != null)
                        {
                            self.pickupEntries[i].pickupDef = randomizedItem;
                        }
                    }
                    else if (self.pickupEntries[i].pickupDef is EquipmentDef equipment)
                    {
                        if (equipment.requiredExpansion == RgiExpansion) continue;
                        EquipmentDef randomizedEquipment = RandomizeEquipmentPickup(equipment.isLunar, equipment.isBoss);
                        if (randomizedEquipment != null)
                        {
                            self.pickupEntries[i].pickupDef = randomizedEquipment;
                        }
                    }
                }

                orig(self);
            };

            On.RoR2.BasicPickupDropTable.IsFilterRequired += (orig, self) => orig(self) && !(self.requiredItemTags.Contains(ItemTag.HalcyoniteShrine) && RunArtifactManager.instance.IsArtifactEnabled(ArtifactFrivolity));

            On.RoR2.PickupDropletController.CreatePickupDroplet_CreatePickupInfo_Vector3_Vector3 += (orig, pickupInfo, position, velocity) =>
            {
                if (RunArtifactManager.instance.IsArtifactEnabled(ArtifactFrivolity))
                {
                    pickupInfo.pickupIndex = FrivolizePickupIndex(pickupInfo.pickupIndex);
                    if (pickupInfo.pickerOptions != null)
                    {
                        for (int i = 0; i < pickupInfo.pickerOptions.Length; i++) {
                            pickupInfo.pickerOptions[i].pickupIndex = FrivolizePickupIndex(pickupInfo.pickerOptions[i].pickupIndex);
                        }
                    }
                }

                orig(pickupInfo, position, velocity);
            };

            bool wasNotMoving = true;
            bool wasOnGround = true;
            float lastRecalculateTime = 0f;
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
                if (needsRecalculate || Time.time - lastRecalculateTime > 0.1f)
                {
                    self.RecalculateStats();
                    lastRecalculateTime = Time.time;
                }
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
                bool success = GeneratedEquipmentEffects.ContainsKey(equipmentDef) || orig(self, equipmentDef);
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
                    GenerateItem(itemTypeCount.Key);
                    yield return null;
                }
                itemNum++;
            }
            ContentPack.itemDefs.Add(GeneratedItemEffects.Keys.ToArray());
        }

        private ItemDef CreateItemDef(ItemTier tier)
        {
            ItemDef itemDef = ScriptableObject.CreateInstance<ItemDef>();

            itemDef.name = "RGI_" + tier.ToString().ToUpperInvariant() + "_" + GeneratedItemDefs.Count.ToString("00000000");
            itemDef.AutoPopulateTokens();
            itemDef.requiredExpansion = RgiExpansion;
            itemDef.hidden = false;
            itemDef.tier = tier;
#pragma warning disable CS0618
            itemDef.deprecatedTier = tier;
#pragma warning restore CS0618

            LanguageAPI.Add(itemDef.nameToken, "Tabula Rasa");
            LanguageAPI.Add(itemDef.nameToken + "_PLURAL", "Tabula Rasa");
            LanguageAPI.Add(itemDef.loreToken, "This is an uninitialized Randomly Generated Item - if you can read this, something went wrong!");

            GeneratedItemDefs.Add(itemDef);

            return itemDef;
        }

        public void GenerateItem(ItemTier tier)
        {
            GenerateItemEffects(CreateItemDef(tier));
        }

        public void GenerateItemEffects(ItemDef itemDef)
        {
            Color color = TierColors.GetValueOrDefault(itemDef.tier, Color.black);

            ItemEffects effects = new(itemDef, Main.Rng);
            SpriteShape spriteShape = effects.Generate();

            itemDef.pickupModelPrefab = GenerateRandomItemPrefab(effects.SpriteColors ?? Array.Empty<Color>(), itemDef.name, spriteShape);
            itemDef.pickupIconSprite = GenerateRandomItemIconAsync(effects.SpriteColors ?? Array.Empty<Color>(), color, spriteShape);

            (string itemName, string itemNamePlural) = GenerateRandomItemName();
            string lore = GenerateRandomItemLogEntry();
            effects.SetNameAndLore(itemName, itemNamePlural, lore);

            LanguageAPI.Add(itemDef.pickupToken, effects.Description);
            LanguageAPI.Add(itemDef.descriptionToken, effects.Description);

            ItemDef inactiveDef = null;
            if (effects.HasInactiveForm)
            {
                inactiveDef = ScriptableObject.CreateInstance<ItemDef>();
                inactiveDef.name = itemDef.name + "_INACTIVE";
                inactiveDef.AutoPopulateTokens();
                inactiveDef.requiredExpansion = RgiExpansion;
                inactiveDef.hidden = true;
                inactiveDef.tier = itemDef.tier;
#pragma warning disable CS0618
                inactiveDef.deprecatedTier = itemDef.deprecatedTier;
#pragma warning restore CS0618
                inactiveDef.pickupModelPrefab = itemDef.pickupModelPrefab;
                inactiveDef.pickupIconSprite = GenerateInactiveIcon(itemDef.pickupIconSprite);

                LanguageAPI.Add(inactiveDef.nameToken, Language.GetString(itemDef.nameToken));
                LanguageAPI.Add(inactiveDef.loreToken, Language.GetString(itemDef.loreToken));

                LanguageAPI.Add(inactiveDef.pickupToken, effects.Description + "\nThis item is currently inactive.");
                LanguageAPI.Add(inactiveDef.descriptionToken, effects.Description + "\nThis item is currently inactive.");

                effects.InactiveItem = inactiveDef;
            }

            effects.Register();

            Main.RgiLogger.LogDebug("Generated a " + itemDef.tier + " item named " + Language.GetString(itemDef.nameToken));
            GeneratedItemEffects[itemDef] = effects;
        }

        private IEnumerator GenerateEquipments(LoadStaticContentAsyncArgs contentPackArgs)
        {
            for (int i = 0; i < EquipmentCount; i++)
            {
                contentPackArgs.ReportProgress(0.7f + 0.1f * ((float)i / EquipmentCount));
                GenerateEquipment(false, false);
                yield return null;
            }
            for (int i = 0; i < LunarEquipmentCount; i++)
            {
                contentPackArgs.ReportProgress(0.8f + 0.1f * ((float)i / LunarEquipmentCount));
                GenerateEquipment(true, false);
                yield return null;
            }
            ContentPack.equipmentDefs.Add(GeneratedEquipmentEffects.Keys.ToArray());
        }

        private EquipmentDef CreateEquipmentDef(bool isLunar, bool isBoss)
        {
            EquipmentDef equipmentDef = ScriptableObject.CreateInstance<EquipmentDef>();

            if (isLunar)
            {
                if (isBoss)
                {
                    equipmentDef.name = "RGI_LUNAR_BOSS_EQUIP_" + GeneratedEquipmentDefs.Count.ToString("00000000");
                }
                else
                {
                    equipmentDef.name = "RGI_LUNAR_EQUIP_" + GeneratedEquipmentDefs.Count.ToString("00000000");
                }
            }
            else if (isBoss)
            {
                equipmentDef.name = "RGI_BOSS_EQUIP_" + GeneratedEquipmentDefs.Count.ToString("00000000");
            }
            else
            {
                equipmentDef.name = "RGI_EQUIP_" + GeneratedEquipmentDefs.Count.ToString("00000000");
            }
            equipmentDef.AutoPopulateTokens();
            equipmentDef.requiredExpansion = RgiExpansion;
            equipmentDef.isLunar = isLunar;
            equipmentDef.isBoss = isBoss;
            equipmentDef.canDrop = true;

            LanguageAPI.Add(equipmentDef.nameToken, "Tabula Rasa");
            LanguageAPI.Add(equipmentDef.nameToken + "_PLURAL", "Tabula Rasa");
            LanguageAPI.Add(equipmentDef.loreToken, "This is an uninitialized Randomly Generated Item - if you can read this, something went wrong!");

            GeneratedEquipmentDefs.Add(equipmentDef);

            return equipmentDef;
        }

        public void GenerateEquipment(bool isLunar, bool isBoss)
        {
            GenerateEquipmentEffects(CreateEquipmentDef(isLunar, isBoss));
        }

        public EquipmentEffects GenerateEquipmentEffects(EquipmentDef equipmentDef)
        {
            Color color = equipmentDef.isLunar ? TierColors[ItemTier.Lunar] : EquipmentColor;

            EquipmentEffects effects = new(equipmentDef, Main.Rng);
            SpriteShape spriteShape = effects.Generate();

            equipmentDef.pickupModelPrefab = GenerateRandomItemPrefab(effects.SpriteColors ?? Array.Empty<Color>(), equipmentDef.name, spriteShape);
            equipmentDef.pickupIconSprite = GenerateRandomItemIconAsync(effects.SpriteColors ?? Array.Empty<Color>(), color, spriteShape);

            (string itemName, string itemNamePlural) = GenerateRandomItemName();
            string lore = GenerateRandomItemLogEntry();
            effects.SetNameAndLore(itemName, itemNamePlural, lore);

            LanguageAPI.Add(equipmentDef.pickupToken, effects.Description);
            LanguageAPI.Add(equipmentDef.descriptionToken, effects.Description);

            EquipmentDef inactiveDef = null;
            if (effects.HasInactiveForm)
            {
                inactiveDef = ScriptableObject.CreateInstance<EquipmentDef>();
                inactiveDef.name = equipmentDef.name + "_INACTIVE";
                inactiveDef.AutoPopulateTokens();
                inactiveDef.requiredExpansion = RgiExpansion;
                inactiveDef.isLunar = equipmentDef.isLunar;
                inactiveDef.isBoss = equipmentDef.isBoss;
                inactiveDef.canDrop = false;
                inactiveDef.pickupModelPrefab = equipmentDef.pickupModelPrefab;
                inactiveDef.pickupIconSprite = GenerateInactiveIcon(equipmentDef.pickupIconSprite);

                LanguageAPI.Add(inactiveDef.nameToken, Language.GetString(equipmentDef.nameToken));
                LanguageAPI.Add(inactiveDef.loreToken, Language.GetString(equipmentDef.loreToken));

                LanguageAPI.Add(inactiveDef.pickupToken, effects.Description + "\nThis equipment is currently inactive.");
                LanguageAPI.Add(inactiveDef.descriptionToken, effects.Description + "\nThis equipment is currently inactive.");

                effects.InactiveEquipment = inactiveDef;
            }

            effects.Register();

            Main.RgiLogger.LogDebug("Generated a " + (equipmentDef.isLunar ? "lunar " : "") + (equipmentDef.isBoss ? "boss " : "") + "equipment named " + Language.GetString(equipmentDef.nameToken));
            GeneratedEquipmentEffects[equipmentDef] = effects;

            return effects;
        }

        private (string itemName, string itemNamePlural) GenerateRandomItemName()
        {
            int attempts = 0;
            while (attempts < 25)
            {
                var prefixRng = Main.Rng.RangeInt(0, NameSystem.ItemNamePrefix.Count);
                var nameRng = Main.Rng.RangeInt(0, NameSystem.ItemName.Count);
                string prefix = NameSystem.ItemNamePrefix[prefixRng] + " ";
                string name = prefix + NameSystem.ItemName[nameRng];
                string namePlural = prefix + NameSystem.ItemNamePlural[nameRng];
                if (GeneratedNames.Add(name)) return (name, namePlural);
                attempts++;
            }

            return (null, null);
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

            Material mat = new(HgStandard)
            {
                color       = Color.white,
                mainTexture = GenerateNoiseTex(coreColors, default, null, null, true, null)
            };

            foreach (MeshRenderer mr in model.GetComponentsInChildren<MeshRenderer>())
            {
                mr.sharedMaterial = mat;
            }

            return prefab.InstantiateClone($"{xmlSafeItemName}-model", false);
        }

        public static Sprite GenerateRandomItemIconAsync(Color[] coreColors, Color borderColor, SpriteShape shape, ulong? seed = null)
        {
            Xoroshiro128Plus rng = seed.HasValue ? new Xoroshiro128Plus(seed.Value) : new Xoroshiro128Plus(Main.Rng);

            Vector2[] randomShadeOffsets = new Vector2[coreColors.Length];
            for (int i = 0; i < randomShadeOffsets.Length; i++)
                randomShadeOffsets[i] = new Vector2(rng.RangeFloat(-10000, 10000), rng.RangeFloat(-10000, 10000));

            Sprite icon = GenerateIcon(coreColors, borderColor, shape, true, randomShadeOffsets);

            UnityEngine.Object.DontDestroyOnLoad(icon.texture);
            UnityEngine.Object.DontDestroyOnLoad(icon);

            return icon;
        }

        public static Sprite GenerateIcon(Color[] coreColors, Color borderColor, SpriteShape shape,
            bool randomShade = true, Vector2[] randomShadeOffsets = null)
        {
            Texture2D noiseTex = GenerateNoiseTex(coreColors, borderColor, ShapeDelegates[shape].Item1, ShapeDelegates[shape].Item2,
                randomShade, randomShadeOffsets);

            return Sprite.Create(noiseTex, new Rect(0, 0, noiseTex.width, noiseTex.height), new Vector2(0.5f, 0.5f));
        }

        public static Texture2D GenerateNoiseTex(Color[] coreColors, Color borderColor = default, Func<int, int, bool> shapeDelegate = null, Func<int, int, bool> borderDelegate = null,
            bool randomShade = true, Vector2[] randomShadeOffsets = null)
        {
            shapeDelegate ??= (x, y) => true;
            borderDelegate ??= (x, y) => false;

            Texture2D tex = new(RandomSpriteResolution, RandomSpriteResolution);

            StartAsyncTaskWithSyncFinalizer(() => GenerateNoiseTexPixels(coreColors, borderColor, shapeDelegate, borderDelegate, randomShade, randomShadeOffsets),
                pixels =>
            {
                tex.SetPixels(GenerateNoiseTexPixels(coreColors, borderColor, shapeDelegate, borderDelegate, randomShade, randomShadeOffsets));
                tex.Apply();
            });
            
            return tex;
        }

        public static Color[] GenerateNoiseTexPixels(Color[] coreColors, Color borderColor = default, Func<int, int, bool> shapeDelegate = null, Func<int, int, bool> borderDelegate = null,
            bool randomShade = true, Vector2[] randomShadeOffsets = null)
        {
            Color[] pixels = new Color[RandomSpriteResolution * RandomSpriteResolution];
            Vector2[] offsets = randomShadeOffsets ?? new Vector2[coreColors.Length];
            float[] noiseSamples = new float[coreColors.Length];
            Color[,] noiseColors = new Color[RandomSpriteNoiseResolution, RandomSpriteNoiseResolution];
            Color baseColor = Color.black;

            if (!randomShade)
            {
                for (int i = 0; i < coreColors.Length; i++)
                {
                    baseColor += coreColors[i] / coreColors.Length;
                }
            }

            for (int y = 0; y < RandomSpriteResolution; y++)
            {
                for (int x = 0; x < RandomSpriteResolution; x++)
                {
                    int pixelIndex = y * RandomSpriteResolution + x;

                    if (shapeDelegate(x, y))
                    {
                        if (randomShade)
                        {
                            int noiseX = x / RandomSpriteNoiseGranularity;
                            int noiseY = y / RandomSpriteNoiseGranularity;

                            if (noiseColors[noiseX, noiseY].a == 0)
                            {
                                float sampleSum = 0;
                                for (int i = 0; i < noiseSamples.Length; i++)
                                {
                                    noiseSamples[i] = (Noise.GetNoise(offsets[i].x + noiseX, offsets[i].y + noiseY) + 1) / 2;
                                    sampleSum += noiseSamples[i];
                                }
                                if (sampleSum < 1) sampleSum = 1;

                                Color noiseColor = baseColor;
                                for (int i = 0; i < coreColors.Length; i++)
                                {
                                    noiseColor += coreColors[i] * noiseSamples[i] / sampleSum;
                                }
                                noiseColors[noiseX, noiseY] = noiseColor;
                            }

                            pixels[pixelIndex] = noiseColors[noiseX, noiseY];
                        }
                        else
                        {
                            pixels[pixelIndex] = baseColor;
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

            return pixels;
        }

        public static Sprite GenerateInactiveIcon(Sprite activeIcon)
        {
            Texture2D tex = new(activeIcon.texture.width, activeIcon.texture.height);
            
            StartAsyncTaskWithSyncFinalizer(async () =>
                {
                    while (activeIcon.texture.updateCount == 0)
                        await Task.Delay(1);
                    return activeIcon.texture;
                }, texture =>
                {
                    tex.SetAllPixels32(texture.GetPixels32(0).Select(c =>
                    {
                        float cVal = c.r / 255f * 0.2126f + c.g / 255f * 0.7152f + c.b / 255f * 0.0722f;
                        cVal = cVal <= 0.0031308 ? cVal * 12.92f : Mathf.Pow(cVal, 1 / 2.4f) * 1.055f - 0.055f;
                        byte cByte = (byte)Mathf.RoundToInt(cVal * 255);
                        return new Color32(cByte, cByte, cByte, c.a);
                    }).ToArray(), 0);
                });

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        private static void StartAsyncTaskWithSyncFinalizer<T>(Func<T> AsyncRunDelegate, Action<T> SyncFinalizerAction)
        {
            Task<T> task = Task.Run(AsyncRunDelegate);
            AsyncTaskFinalizers.Enqueue(() => SyncFinalizerAction(task.Result));
        }

        private static void StartAsyncTaskWithSyncFinalizer<T>(Func<Task<T>> AsyncRunDelegate, Action<T> SyncFinalizerAction)
        {
            Task<T> task = Task.Run(AsyncRunDelegate);
            AsyncTaskFinalizers.Enqueue(() => SyncFinalizerAction(task.Result));
        }

        private PickupIndex FrivolizePickupIndex(PickupIndex pickupIndex)
        {
            PickupIndex? frivolizedPickup = FrivolizePickup(PickupCatalog.GetPickupDef(pickupIndex));
            return frivolizedPickup ?? pickupIndex;
        }

        private PickupDef FrivolizePickupDef(PickupDef pickupDef)
        {
            PickupIndex? frivolizedPickup = FrivolizePickup(pickupDef);
            return frivolizedPickup.HasValue ? PickupCatalog.GetPickupDef(frivolizedPickup.Value) : pickupDef;
        }

        private PickupIndex? FrivolizePickup(PickupDef pickupDef)
        {
            if (pickupDef == null) return null;
            if (pickupDef.itemIndex != ItemIndex.None)
            {
                ItemDef item = ItemCatalog.GetItemDef(pickupDef.itemIndex);
                if (item == null || item.requiredExpansion == RgiExpansion || !item.canRemove || item.hidden
                    || (item.ContainsTag(ItemTag.WorldUnique) && item.DoesNotContainTag(ItemTag.Damage) && item.DoesNotContainTag(ItemTag.Healing) && item.DoesNotContainTag(ItemTag.Utility))) return null;
                ItemDef randomizedItem = RandomizeItemPickup(item.tier);
                if (randomizedItem != null)
                {
                    return PickupCatalog.FindPickupIndex(randomizedItem.itemIndex);
                }
            }
            else if (pickupDef.equipmentIndex != EquipmentIndex.None)
            {
                EquipmentDef equipment = EquipmentCatalog.GetEquipmentDef(pickupDef.equipmentIndex);
                if (equipment == null || equipment.requiredExpansion == RgiExpansion) return null;
                EquipmentDef randomizedEquipment = RandomizeEquipmentPickup(equipment.isLunar, equipment.isBoss);
                if (randomizedEquipment != null)
                {
                    return PickupCatalog.FindPickupIndex(randomizedEquipment.equipmentIndex);
                }
            }
            return null;
        }

        private ItemDef RandomizeItemPickup(ItemTier tier)
        {
            if (!ItemTypeCounts.TryGetValue(tier, out int tierCount) || tierCount <= 0) return null;
            int itemIndex = new Xoroshiro128Plus(Main.Rng).RangeInt(0, tierCount);
            return GeneratedItemEffects.Keys.FirstOrDefault(itemDef => itemDef.tier == tier && itemIndex-- == 0);
        }

        private EquipmentDef RandomizeEquipmentPickup(bool isLunar, bool isBoss)
        {
            if (EquipmentCount == 0) return null;
            int equipmentIndex;
            int matchingEquipmentCount = GeneratedEquipmentEffects.Keys.Count(equipmentDef => equipmentDef.isLunar == isLunar && equipmentDef.isBoss == isBoss);
            if (matchingEquipmentCount > 0)
            {
                equipmentIndex = new Xoroshiro128Plus(Main.Rng).RangeInt(0, matchingEquipmentCount);
                return GeneratedEquipmentEffects.Keys.FirstOrDefault(equipmentDef => equipmentDef.isLunar == isLunar && equipmentDef.isBoss == isBoss && equipmentIndex-- == 0);
            }
            matchingEquipmentCount = GeneratedEquipmentEffects.Keys.Count(equipmentDef => equipmentDef.isLunar == isLunar);
            if (isLunar && matchingEquipmentCount <= 0) matchingEquipmentCount = GeneratedEquipmentEffects.Keys.Count(equipmentDef => !equipmentDef.isLunar);
            if (matchingEquipmentCount <= 0) return null;
            equipmentIndex = new Xoroshiro128Plus(Main.Rng).RangeInt(0, matchingEquipmentCount);
            return GeneratedEquipmentEffects.Keys.FirstOrDefault(equipmentDef => equipmentDef.isLunar == isLunar && equipmentIndex-- == 0);
        }
    }
}
