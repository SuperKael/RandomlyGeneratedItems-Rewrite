using System.Collections;
using System.Collections.Generic;
using R2API;
using RandomlyGeneratedItems.Components;
using RoR2;
using RoR2.Orbs;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace RandomlyGeneratedItems.RandomEffects
{
    public readonly struct SpawnableEffectPayload
    {
        public static readonly Dictionary<string, SpawnableEffectPayload> RegisteredEffectPrefabs = new();

        public readonly string Name;
        public readonly float StrengthModifier;
        public readonly ProcType ProcType;
        public readonly PayloadSpawnDelegate SpawnDelegate;
        public readonly AbstractEffects.DescriptionDelegate DescriptionDelegate;
        public readonly int MinimumGrade;
        
        public static IEnumerator Initialize()
        {
            RegisterProjectilePrefab("RandomMissile", 1f, ProcType.Missile, _ => "<style=cIsDamage>Missile</style>", "RoR2/Base/Common/MissileProjectile.prefab", 2);
            RegisterProjectilePrefab("RandomClayPot", 2f, ProcType.Missile, _ => "<style=cIsDamage>Clay Pot</style>", "RoR2/Base/ClayBoss/ClayPotProjectile.prefab")
                .prefab.GetComponent<ProjectileImpactExplosion>().blastRadius = 9f;
            RegisterProjectilePrefab("RandomVoidSpike", 1.5f, ProcType.Missile, _ => "<style=cIsVoid>Void Spike</style>", "RoR2/Base/ImpBoss/ImpVoidspikeProjectile.prefab");
            RegisterProjectilePrefab("RandomSaw", 1f, ProcType.Missile, _ => "<style=cDeath>Sawblade</style>", "RoR2/Base/Saw/Sawmerang.prefab", 2)
                .prefab.GetComponent<ProjectileDotZone>().resetFrequency = 0;
            RegisterProjectilePrefab("RandomNade", 2f, ProcType.Missile, _ => "<style=cIsDamage>Grenade</style>", "RoR2/Base/Commando/CommandoGrenadeProjectile.prefab");
            RegisterProjectilePrefab("RandomFireball", 1.5f, ProcType.Missile, _ => "<style=cIsDamage>Fireball</style>", "RoR2/Base/LemurianBruiser/LemurianBigFireball.prefab");

            GameObject randomExplosionPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Common/VFX/OmniExplosionVFXQuick.prefab").WaitForCompletion().InstantiateClone("RandomWispExplosion");
            RegisterPayloadSpawnDelegate("RandomExplosion", 1f, ProcType.Behemoth,
                (character, effects, origin, direction, stackCount, procCoefficient, procChainMask, _) =>
                {
                    float radius = (1.5f + 2.5f * stackCount) * procCoefficient;
                    EffectManager.SpawnEffect(randomExplosionPrefab, new EffectData
                    {
                        origin = origin,
                        scale = radius,
                        rotation = Util.QuaternionSafeLookRotation(direction)
                    }, true);
                    BlastAttack blastAttack = new()
                    {
                        position = origin,
                        baseDamage = effects.GetTriggeredStrength(stackCount, procCoefficient),
                        baseForce = 0f,
                        radius = radius,
                        attacker = character.gameObject,
                        inflictor = null,
                        teamIndex = character.teamComponent.teamIndex,
                        crit = Util.CheckRoll(character.crit, character.master),
                        procChainMask = procChainMask,
                        procCoefficient = 0f,
                        damageColorIndex = DamageColorIndex.Item,
                        falloffModel = BlastAttack.FalloffModel.None,
                        damageType = DamageTypeCombo.Generic
                    };
                    blastAttack.Fire();
                }, _ => "<style=cIsDamage>Explosion</style>", 3);

            GameObject randomWispExplosionPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/ExplodeOnDeath/WilloWispDelay.prefab").WaitForCompletion().InstantiateClone("RandomWispExplosion");
            randomWispExplosionPrefab.AddComponent<EnhancedDelayBlast>().TransferDataFromDelayBlast();
            RegisterPayloadSpawnDelegate("RandomWispExplosion", 1f, ProcType.Behemoth,
                (character, effects, origin, _, stackCount, procCoefficient, procChainMask, _) =>
                {
                    GameObject explosionObject = Object.Instantiate(randomWispExplosionPrefab, origin, Quaternion.identity);
                    EnhancedDelayBlast delayBlast = explosionObject.GetComponent<EnhancedDelayBlast>();
                    delayBlast.BlastAttack = new BlastAttack()
                    {
                        position = origin,
                        baseDamage = effects.GetTriggeredStrength(stackCount, procCoefficient),
                        baseForce = 2000f,
                        bonusForce = Vector3.up * 1000f,
                        radius = 12f + 2.4f * (stackCount - 1f),
                        attacker = character.gameObject,
                        inflictor = null,
                        crit = Util.CheckRoll(character.crit, character.master),
                        damageColorIndex = DamageColorIndex.Item,
                        falloffModel = BlastAttack.FalloffModel.SweetSpot,
                        procChainMask = procChainMask,
                        procCoefficient = procCoefficient,
                        teamIndex = character.teamComponent.teamIndex,
                    };
                    delayBlast.MaxTimer = 0.5f;
                    explosionObject.GetComponent<TeamFilter>().teamIndex = character.teamComponent.teamIndex;
                    NetworkServer.Spawn(explosionObject);
                }, _ => "<style=cIsDamage>Wisp Explosion</style>");

            GameObject randomVoidWispExplosionPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/ExplodeOnDeathVoid/ExplodeOnDeathVoidExplosion.prefab").WaitForCompletion().InstantiateClone("RandomVoidWispExplosion");
            randomVoidWispExplosionPrefab.AddComponent<EnhancedDelayBlast>().TransferDataFromDelayBlast();
            RegisterPayloadSpawnDelegate("RandomVoidWispExplosion", 1f, ProcType.Behemoth,
                (character, effects, origin, _, stackCount, procCoefficient, procChainMask, _) =>
                {
                    GameObject explosionObject = Object.Instantiate(randomVoidWispExplosionPrefab, origin, Quaternion.identity);
                    EnhancedDelayBlast delayBlast = explosionObject.GetComponent<EnhancedDelayBlast>();
                    delayBlast.BlastAttack = new BlastAttack()
                    {
                        position = origin,
                        baseDamage = effects.GetTriggeredStrength(stackCount, procCoefficient),
                        baseForce = 1000f,
                        bonusForce = Vector3.up * 1000f,
                        radius = 12f + 2.4f * (stackCount - 1f),
                        attacker = character.gameObject,
                        inflictor = null,
                        crit = Util.CheckRoll(character.crit, character.master),
                        damageColorIndex = DamageColorIndex.Void,
                        falloffModel = BlastAttack.FalloffModel.SweetSpot,
                        procChainMask = procChainMask,
                        procCoefficient = procCoefficient,
                        teamIndex = character.teamComponent.teamIndex,
                    };
                    delayBlast.MaxTimer = 0.5f;
                    explosionObject.GetComponent<TeamFilter>().teamIndex = character.teamComponent.teamIndex;
                    NetworkServer.Spawn(explosionObject);
                }, _ => "<style=cIsVoid>Void Wisp Explosion</style>", 2);

            RegisterPayloadSpawnDelegate("RandomChainLightning", 0.5f, ProcType.ChainLightning,
                (character, effects, origin, _, stackCount, procCoefficient, procChainMask, args) =>
                {
                    List<HealthComponent> bouncedObjects = new();
                    if (args["damageReport"] is DamageReport damageReport) bouncedObjects.Add(damageReport.victim);

                    LightningOrb lightningOrb2 = new()
                    {
                        origin = origin,
                        damageValue = effects.GetTriggeredStrength(stackCount, procCoefficient),
                        isCrit = Util.CheckRoll(character.crit, character.master),
                        bouncesRemaining = 2 * stackCount,
                        teamIndex = character.teamComponent.teamIndex,
                        attacker = character.gameObject,
                        bouncedObjects = bouncedObjects,
                        procChainMask = procChainMask,
                        procCoefficient = 0.2f,
                        lightningType = LightningOrb.LightningType.Ukulele,
                        damageColorIndex = DamageColorIndex.Item
                    };
                    lightningOrb2.range += 2 * stackCount;
                    HurtBox hurtBox2 = lightningOrb2.PickNextTarget(origin);
                    if (hurtBox2)
                    {
                        lightningOrb2.target = hurtBox2;
                        OrbManager.instance.AddOrb(lightningOrb2);
                    }
                }, _ => "<style=cIsDamage>Chain Lightning</style>", 2);

            yield break;
        }

        public static (SpawnableEffectPayload spawnableEffectPayload, GameObject prefab) RegisterProjectilePrefab(string name, float strengthModifier, ProcType procType, AbstractEffects.DescriptionDelegate descriptionDelegate, string key, int minimumGrade = 0)
        {
            GameObject prefab = Addressables.LoadAssetAsync<GameObject>(key).WaitForCompletion().InstantiateClone(name);
            return (RegisterPayloadSpawnDelegate(name, strengthModifier, procType,
                    (character, effects, origin, direction, stackCount, procCoefficient, procChainMask, _) =>
                {
                    FireProjectileInfo proj = new()
                    {
                        damage = character.damage * effects.GetTriggeredStrength(stackCount, procCoefficient),
                        owner = character.gameObject,
                        speedOverride = 100 * direction.magnitude,
                        fuseOverride = 2,
                        rotation = Util.QuaternionSafeLookRotation(direction),
                        position = origin,
                        damageColorIndex = DamageColorIndex.Item,
                        projectilePrefab = prefab,
                        procChainMask = procChainMask
                    };

                    ProjectileManager.instance.FireProjectile(proj);
                }, descriptionDelegate, minimumGrade), prefab);
        }

        public static SpawnableEffectPayload RegisterPayloadSpawnDelegate(string name, float strengthModifier, ProcType procType,
            PayloadSpawnDelegate spawnDelegate, AbstractEffects.DescriptionDelegate descriptionDelegate,
            int minimumGrade = 0)
        {
            SpawnableEffectPayload spawnableEffectPayload = new(name, strengthModifier, procType, spawnDelegate
                , descriptionDelegate, minimumGrade);
            RegisteredEffectPrefabs[name] = spawnableEffectPayload;
            return spawnableEffectPayload;
        }

        public SpawnableEffectPayload(string name, float strengthModifier, ProcType procType, PayloadSpawnDelegate spawnDelegate, AbstractEffects.DescriptionDelegate descriptionDelegate, int minimumGrade)
        {
            Name = name;
            StrengthModifier = strengthModifier;
            ProcType = procType;
            SpawnDelegate = spawnDelegate;
            DescriptionDelegate = descriptionDelegate;
            MinimumGrade = minimumGrade;
        }

        public void SpawnEffect(CharacterBody character, AbstractEffects effects,
            Vector3 origin, Vector3 direction, int stackCount, float procCoefficient, ProcChainMask procChainMask, Dictionary<string, object> args)
        {
            SpawnDelegate(character, effects, origin, direction, stackCount, procCoefficient, procChainMask, args);
        }

        public delegate void PayloadSpawnDelegate(CharacterBody character, AbstractEffects effects, 
            Vector3 origin, Vector3 direction, int stackCount, float procCoefficient, ProcChainMask procChainMask, Dictionary<string, object> args);
    }
}