using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RandomlyGeneratedItems.Components
{
    public class EnhancedDelayBlast : MonoBehaviour
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Unity Event")]
        private void Awake()
        {
            if (!NetworkServer.active)
            {
                enabled = false;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Unity Event")]
        private void FixedUpdate()
        {
            if (!NetworkServer.active) return;
            timer += Time.fixedDeltaTime;
            if (DelayEffect && !hasSpawnedDelayEffect && timer > TimerStagger)
            {
                hasSpawnedDelayEffect = true;
                EffectManager.SpawnEffect(DelayEffect, new EffectData
                {
                    origin = transform.position,
                    rotation = Util.QuaternionSafeLookRotation(transform.forward),
                    scale = BlastAttack.radius
                }, true);
            }
            if (timer >= MaxTimer + TimerStagger)
            {
                Detonate();
            }
        }

        public void TransferDataFromDelayBlast()
        {
            DelayBlast delayBlast = GetComponent<DelayBlast>();
            BlastAttack = new BlastAttack
            {
                position = delayBlast.position,
                baseDamage = delayBlast.baseDamage,
                baseForce = delayBlast.baseForce,
                bonusForce = delayBlast.bonusForce,
                radius = delayBlast.radius,
                attacker = delayBlast.attacker,
                inflictor = delayBlast.inflictor,
                teamIndex = delayBlast.GetComponent<TeamFilter>().teamIndex,
                crit = delayBlast.crit,
                damageColorIndex = delayBlast.damageColorIndex,
                damageType = delayBlast.damageType,
                falloffModel = delayBlast.falloffModel,
                procCoefficient = delayBlast.procCoefficient
            };
            MaxTimer = delayBlast.maxTimer;
            ExplosionEffect = delayBlast.explosionEffect;
            DelayEffect = delayBlast.delayEffect;
            TimerStagger = delayBlast.timerStagger;
            Destroy(delayBlast);
        }
        
        public void Detonate()
        {
            EffectManager.SpawnEffect(ExplosionEffect, new EffectData
            {
                origin = transform.position,
                rotation = Util.QuaternionSafeLookRotation(transform.forward),
                scale = BlastAttack.radius
            }, true);
            BlastAttack.Fire();
            Destroy(gameObject);
        }

        public BlastAttack BlastAttack;
        
        public float MaxTimer;
        public GameObject ExplosionEffect;
        public GameObject DelayEffect;
        public float TimerStagger;
        private float timer;
        private bool hasSpawnedDelayEffect;
    }
}
