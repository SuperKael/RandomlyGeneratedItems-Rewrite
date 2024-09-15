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

        public static readonly List<Tuple<Func<int, int, bool>, Func<int, int, bool>>> ShapeDelegates = new()
        {
            Tuple.Create<Func<int, int, bool>, Func<int, int, bool>>((x, y) => x is > 128 and < 384 && y is > 128 and < 384, (x, y) => x is > 112 and < 400 && y is > 112 and < 400),
            Tuple.Create<Func<int, int, bool>, Func<int, int, bool>>((x, y) => Mathf.Abs(x - 256) + Mathf.Abs(y - 256) < 192, (x, y) => Mathf.Abs(x - 256) + Mathf.Abs(y - 256) < 208),
            Tuple.Create<Func<int, int, bool>, Func<int, int, bool>>((x, y) => Mathf.Pow(x - 256, 2) + Mathf.Pow(y - 256, 2) < 192 * 192, (x, y) => Mathf.Pow(x - 256, 2) + Mathf.Pow(y - 256, 2) < 208 * 208)
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
            RgiExpansion.iconSprite = GenerateIcon(Color.green, new[] { Color.green }, 1);
            RgiExpansion.disabledIconSprite = GenerateIcon(Color.gray, new[] { Color.gray }, 1);

            LanguageAPI.Add(RgiExpansion.nameToken, "Randomly Generated Items");
            LanguageAPI.Add(RgiExpansion.descriptionToken, "Enables randomly-generated items. Note that you must fully restart the game in order to generate a new batch of items.");

            ContentPack.expansionDefs.Add(new[] { RgiExpansion });

            ArtifactFrivolity = ScriptableObject.CreateInstance<ArtifactDef>();
            ArtifactFrivolity.cachedName = "ARTIFACT_RGI_FRIVOLITY";
            ArtifactFrivolity.nameToken = ArtifactFrivolity.cachedName + "_NAME";
            ArtifactFrivolity.descriptionToken = ArtifactFrivolity.cachedName + "_DESC";
            ArtifactFrivolity.requiredExpansion = RgiExpansion;
            ArtifactFrivolity.smallIconSelectedSprite = GenerateIcon(new Color(0.9f, 0.75f, 0.9f), new[] { new Color(0.9f, 0.75f, 0.9f) });
            ArtifactFrivolity.smallIconDeselectedSprite = GenerateIcon(Color.gray, new[] { Color.gray });

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
            ItemTypeCounts[ItemTier.VoidTier1] = Main.RgiConfig.Bind("Configuration", "Void Common Items", 3,
                "The number of void common items to generate.").Value;
            ItemTypeCounts[ItemTier.VoidTier2] = Main.RgiConfig.Bind("Configuration", "Void Uncommon Items", 3,
                "The number of void uncommon items to generate.").Value;
            ItemTypeCounts[ItemTier.VoidTier3] = Main.RgiConfig.Bind("Configuration", "Void Legendary Items", 3,
                "The number of void legendary items to generate.").Value;
            EquipmentCount = Main.RgiConfig.Bind("Configuration", "Equipment Items", 10,
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

            bool wasNotMoving = true;
            On.RoR2.CharacterBody.Update += (orig, self) =>
            {
                orig(self);
                if (!self || !NetworkServer.active || !self.isPlayerControlled ||
                    self.GetNotMoving() == wasNotMoving) return;
                wasNotMoving = !wasNotMoving;
                self.RecalculateStats();
            };

            RecalculateStatsAPI.GetStatCoefficients += AbstractEffects.ApplyPassiveEffects;

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
                if (!success) return false;
                AbstractEffects.TriggerEffects("Equipment", self.characterBody, new Dictionary<string, object>
                {
                    ["equipmentDef"] = equipmentDef
                });
                return true;
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

            (string itemName, string xmlSafeItemName) = GenerateRandomItemName();

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

            itemDef.AutoPopulateTokens();

            ItemEffects effects = new(itemDef, Main.Rng);
            int spriteShape = effects.Generate();

            itemDef.pickupModelPrefab = GenerateRandomItemPrefab(effects.SpriteColors ?? Array.Empty<Color>(), xmlSafeItemName, spriteShape);
            itemDef.pickupIconSprite = GenerateRandomItemIcon(color, effects.SpriteColors ?? Array.Empty<Color>(), spriteShape);

            effects.Register();

            LanguageAPI.Add(itemDef.nameToken, itemName);
            LanguageAPI.Add(itemDef.pickupToken, effects.Description);
            LanguageAPI.Add(itemDef.descriptionToken, effects.Description);
            LanguageAPI.Add(itemDef.loreToken, GenerateRandomItemLogEntry());

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

            (string itemName, string xmlSafeItemName) = GenerateRandomItemName();

            if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(xmlSafeItemName)) throw new InvalidOperationException("Failed to generate a new equipment name!");


            Color color = isLunar ? TierColors[ItemTier.Lunar] : EquipmentColor;

            equipmentDef.name = "RGI_" + xmlSafeItemName;
            equipmentDef.AutoPopulateTokens();
            equipmentDef.requiredExpansion = RgiExpansion;
            equipmentDef.isLunar = isLunar;
            equipmentDef.isBoss = isBoss;
            equipmentDef.canDrop = true;

            EquipmentEffects effects = new(equipmentDef, Main.Rng);
            int spriteShape = effects.Generate();

            equipmentDef.pickupModelPrefab = GenerateRandomItemPrefab(effects.SpriteColors ?? Array.Empty<Color>(), xmlSafeItemName, spriteShape);
            equipmentDef.pickupIconSprite = GenerateRandomItemIcon(color, effects.SpriteColors ?? Array.Empty<Color>(), spriteShape);

            effects.Register();

            LanguageAPI.Add(equipmentDef.nameToken, itemName);
            LanguageAPI.Add(equipmentDef.pickupToken, effects.Description);
            LanguageAPI.Add(equipmentDef.descriptionToken, effects.Description);
            LanguageAPI.Add(equipmentDef.loreToken, GenerateRandomItemLogEntry());

            Main.RgiLogger.LogDebug("Generated a " + (isLunar ? "lunar " : "") + (isBoss ? "boss " : "") + "equipment named " + itemName);
            GeneratedEquipmentDefs.Add(equipmentDef);
            yield break;
        }

        private (string itemName, string xmlSafeItemName) GenerateRandomItemName()
        {
            int attempts = 0;
            while (attempts < 25)
            {
                var prefixRng2 = Main.Rng.RangeInt(0, NameSystem.ItemNamePrefix.Count);
                var nameRng2 = Main.Rng.RangeInt(0, NameSystem.ItemName.Count);
                string itemName = "";
                itemName += NameSystem.ItemNamePrefix[prefixRng2] + " ";
                itemName += NameSystem.ItemName[nameRng2];
                string xmlSafeItemName = itemName.ToUpper().Replace(" ", "_").Replace("'", "").Replace("&", "AND");
                if (GeneratedNames.Add(xmlSafeItemName)) return (itemName, xmlSafeItemName);
                attempts++;
            }

            return (null, null);
        }

        private string GenerateRandomItemLogEntry()
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

        private GameObject GenerateRandomItemPrefab(Color[] coreColors, string xmlSafeItemName, int shape = 0, bool randomShade = true, Vector2[] randomShadeOffsets = null)
        {
            GameObject prefab = new GameObject();
            GameObject model;

            switch (shape)
            {
                default: // cube
                    model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    break;
                case 1: // diamond
                    model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    model.transform.Rotate(45, 45, 45);
                    break;
                case 2: // sphere
                    model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    break;
            }

            model.transform.SetParent(prefab.transform);


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

        private static Sprite GenerateRandomItemIcon(Color borderColor, Color[] coreColors, int shape = 0, ulong? seed = null)
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

        private static Sprite GenerateIcon(Color borderColor, Color[] coreColors, int shape = 0,
            bool randomShade = true, Vector2[] randomShadeOffsets = null)
        {
            return GenerateIcon(borderColor, coreColors, ShapeDelegates[shape].Item1, ShapeDelegates[shape].Item2,
                randomShade, randomShadeOffsets);
        }

        private static Sprite GenerateIcon(Color borderColor, Color[] coreColors, Func<int, int, bool> shapeDelegate, Func<int, int, bool> borderDelegate, bool randomShade = true, Vector2[] randomShadeOffsets = null)
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
    }
}
