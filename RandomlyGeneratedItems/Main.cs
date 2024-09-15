using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RoR2.ContentManagement;

namespace RandomlyGeneratedItems
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInIncompatibility("com.xoxfaby.BetterUI")]
    public class Main : BaseUnityPlugin
    {
        public const string PluginGuid = PluginAuthor + "." + PluginName;

        public const string PluginAuthor = "SuperKael"; // Original author is HIFUPulse!
        public const string PluginName = "RandomlyGeneratedItems";
        public const string PluginVersion = "2.0.0";

        public static ConfigFile RgiConfig;
        public static ManualLogSource RgiLogger;

        public static RandomContentPackProvider ContentPackProvider;
        
        private static ulong seed;
        public static Xoroshiro128Plus Rng;

        public static ConfigEntry<ulong> SeedConfig { get; set; }

        public void Awake()
        {
            RgiLogger = Logger;
            RgiConfig = Config;
            SeedConfig = Config.Bind<ulong>("Configuration", "Seed", 0, "The seed that will be used for random generation. A seed of 0 will generate a random seed instead. If playing multiplayer, ensure that not only does everyone use the same non-zero seed, but that all of the item counts perfectly match between every player!");

            if (SeedConfig.Value != 0)
            {
                seed = SeedConfig.Value;
            }
            else
            {
                seed = (ulong)UnityEngine.Random.RandomRangeInt(0, 10000) ^ (ulong)UnityEngine.Random.RandomRangeInt(1, 10) << 16;
            }

            Rng = new Xoroshiro128Plus(seed);
            Logger.LogInfo("Seed is " + seed);
            
            NameSystem.Populate();
            
            ContentManager.collectContentPackProviders += addContentPackProvider => addContentPackProvider(ContentPackProvider = new RandomContentPackProvider());
        }
    }
}