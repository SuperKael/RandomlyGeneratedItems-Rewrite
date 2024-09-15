using System.Collections;
using System.Collections.Generic;
using R2API;
using RoR2;
using RoR2.ContentManagement;
using UnityEngine;
using UnityEngine.Networking;

namespace RandomlyGeneratedItems
{
    public class Buffs
    {
        public static List<BuffDef> RegisteredBuffs = new();

        public static IEnumerator RegisterBuffs(ContentPack contentPack)
        {
            NoMaxBarrier.Register();

            contentPack.buffDefs.Add(RegisteredBuffs.ToArray());

            yield break;
        }

        public class NoMaxBarrier
        {
            public static BuffDef BuffDef;

            public static void Register()
            {
                BuffDef = ScriptableObject.CreateInstance<BuffDef>();
                BuffDef.name = "BUFF_NO_DECAY";
                BuffDef.isHidden = true;
                BuffDef.isDebuff = false;

                RegisteredBuffs.Add(BuffDef);

                RecalculateStatsAPI.GetStatCoefficients += (body, _) =>
                {
                    if (!NetworkServer.active || !body.HasBuff(BuffDef)) return;
                    
                    // Extremely large number, but reduced from float.MaxValue a bit to try to prevent overflow if additional modifiers are applied after this
                    body.maxBarrier = float.MaxValue / 16;
                    body.barrierDecayRate = (body.maxHealth + body.maxShield) / 30;
                };
            }
        }
    }
}