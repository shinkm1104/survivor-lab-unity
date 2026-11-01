using System.Collections.Generic;
using UnityEngine;

namespace SurvivorLab {
    public class ElementVisual { public Vector2 from,to; public float radius,expires,spreadDamage; public bool beam; }
    public sealed partial class Simulation {
        public readonly List<ElementVisual> elementVisuals=new List<ElementVisual>();
        readonly Queue<ElementVisual> deathExplosions=new Queue<ElementVisual>();
        readonly Queue<float> deathExplosionDamage=new Queue<float>();
        public float DealDamage(Enemy e,float amount,bool critical=false) {
            if(e.hp<=0 || amount<=0) return 0;
            if(time<e.acidUntil) amount*=1+b.Element("Acid").vulnerabilityPercent/100;
            float actual=Mathf.Min(e.hp,amount); e.hp-=amount; damageDealt+=actual;
            if(critical) Heal(actual*Param("vital_strike","healActualCritDamagePercent")/100);
            if(e.hp<=0) {
                kills++; drops.Add(new Drop{pos=e.pos,xp=b.HasWaves && Wave.xp>0?Wave.xp:b.enemyXp});
                if(kills%b.potionEveryKills==0) drops.Add(new Drop{pos=e.pos+Vector2.right*0.2f,potion=true});
                if(e.corruptionDamage>0) {
                    deathExplosions.Enqueue(new ElementVisual{from=e.pos,radius=b.Element("Dark").explosionRadius,expires=time+0.3f,spreadDamage=e.corruptionSpreadDamage});
                    deathExplosionDamage.Enqueue(e.corruptionDamage);
                }
            }
            return actual;
        }
        void UpdateElementStatus(Enemy e) {
            while(e.hp>0 && e.burnRemaining>0 && time>=e.burnAt) {
                e.burnRemaining--; e.burnAt+=e.burnInterval; DealDamage(e,e.burnDamage);
            }
        }
        void ApplyElementOnHit(Enemy e,float hitDamage,ElementDefinition effect) {
            if(e.hp>0) {
                if(effect.burnTicks>0) {
                    e.burnDamage=e.burnRemaining>0?Mathf.Max(e.burnDamage,hitDamage*effect.burnFactor):hitDamage*effect.burnFactor;
                    e.burnRemaining=Mathf.Max(e.burnRemaining,effect.burnTicks);
                    e.burnInterval=effect.burnInterval;
                    // Re-hits refresh remaining ticks without postponing the next tick forever.
                    if(e.burnAt<=time) e.burnAt=time+effect.burnInterval;
                }
                if(effect.acidDuration>0) e.acidUntil=time+effect.acidDuration;
                if(effect.enemyDamageReductionPercent>0) { e.weakenUntil=time+effect.acidDuration; e.weakenPercent=effect.enemyDamageReductionPercent; }
                if(effect.chillDuration>0) e.chillUntil=time+effect.chillDuration;
                if(effect.freezeDuration>0 && time>=e.freezeImmuneUntil && random.NextDouble()<effect.freezeChance) {
                    e.frozenUntil=time+effect.freezeDuration; e.freezeImmuneUntil=e.frozenUntil+effect.freezeImmunity;
                }
                if(effect.knockbackSpeed>0) { e.knockVelocity=(e.pos-player).normalized*effect.knockbackSpeed; e.knockUntil=time+effect.knockbackDuration; }
            }
            if(effect.splashFactor>0) {
                elementVisuals.Add(new ElementVisual{from=e.pos,radius=effect.splashRadius,expires=time+0.2f});
                foreach(var target in enemies) if(target!=e && target.hp>0 && Vector2.Distance(target.pos,e.pos)<=effect.splashRadius) DealDamage(target,hitDamage*effect.splashFactor);
            }
            var seen=new HashSet<int>{e.id}; var previous=e;
            for(int jump=0;jump<effect.chainJumps;jump++) {
                Enemy next=null; float distance=effect.chainRange;
                foreach(var candidate in enemies) if(candidate.hp>0 && !seen.Contains(candidate.id)) {
                    float d=Vector2.Distance(previous.pos,candidate.pos);
                    if(d<=distance) { distance=d; next=candidate; }
                }
                if(next==null) break;
                seen.Add(next.id);
                elementVisuals.Add(new ElementVisual{beam=true,from=previous.pos,to=next.pos,expires=time+0.18f});
                DealDamage(next,hitDamage*effect.chainFactor);
                previous=next;
            }
            if(effect.extraStrikeFactor>0) DealDamage(e,hitDamage*effect.extraStrikeFactor);
        }
        void ResolveElementExplosions() {
            while(deathExplosions.Count>0) {
                var explosion=deathExplosions.Dequeue(); float amount=deathExplosionDamage.Dequeue();
                elementVisuals.Add(explosion);
                foreach(var target in enemies) if(target.hp>0 && Vector2.Distance(target.pos,explosion.from)<=explosion.radius) {
                    if(explosion.spreadDamage>0 && target.corruptionDamage<=0) { target.corruptionDamage=explosion.spreadDamage; target.corruptionSpreadDamage=explosion.spreadDamage; }
                    DealDamage(target,amount);
                }
            }
            elementVisuals.RemoveAll(x=>time>=x.expires);
        }
    }
}
