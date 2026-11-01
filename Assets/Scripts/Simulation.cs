using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivorLab {
    public class Enemy {
        public int id; public Vector2 pos; public float hp, slowUntil, contactAt;
        public float burnAt,burnDamage,acidUntil,chillUntil,frozenUntil,freezeImmuneUntil,knockUntil,corruptionDamage;
        public int burnRemaining; public float burnInterval; public Vector2 knockVelocity;
        public float weakenUntil,weakenPercent,corruptionSpreadDamage;
    }
    public class Drop { public Vector2 pos; public float xp; public bool potion, attracted; }
    public class Rock { public Vector2 pos; public float expires; }
    public class Bolt { public Vector2 pos, dir; public float expires; }
    public class AttackVisual { public Vector2 pos, dir; public float expires, resolveAt; public bool resolved; }
    public class Reward { public string id, label; public int rarity; public float value; public bool scroll, infusion; }
    public class RewardRequest { public bool scroll,infusion; public int level; }

    // One simulation is shared by manual play, agent play, training and deterministic QA.
    public sealed partial class Simulation {
        public readonly Balance b; public readonly int classIndex;
        public readonly List<Enemy> enemies = new List<Enemy>();
        public readonly List<Drop> drops = new List<Drop>();
        public readonly List<Rock> rocks = new List<Rock>();
        public readonly List<Bolt> bolts = new List<Bolt>();
        public readonly List<AttackVisual> slams = new List<AttackVisual>();
        public readonly HashSet<string> owned = new HashSet<string>();
        public readonly Queue<RewardRequest> rewards = new Queue<RewardRequest>();
        public readonly List<Reward> offers = new List<Reward>();
        public Vector2 player, facing = Vector2.up;
        public float time, hp, maxHp, shield, xp, damageTaken, damageDealt, pulseUntil;
        public int level = 1, kills, breathStacks; public bool dead, won;
        public float damageBonus, rateBonus, radiusBonus, lifeBonus, hpBonus, speedBonus, critBonus;
        public float stillTime, fleeUntil = -100, fleeAt = -100;
        public readonly List<string> infusions = new List<string>();
        public float totalXp, lastPickupAt=-100; public int collectedOrbs, rewardLevel;
        public string rewardTitle="", lastPickupText="";
        public string lastAutoChoice=""; public int automaticChoices;
        public IRewardChooser rewardChooser=RuleRewardChooser.Instance;
        public ElementDefinition ActiveElement { get { return infusions.Count==0?null:b.CombinedElement(infusions[0],infusions.Count>1?infusions[1]:null); } }
        float castProgress, spawnAt, rockDistance, breathTime, regenAt, shieldAt, scrollAt;
        bool wasStill = true; int nextId;
        readonly System.Random random;
        public SpellDefinition Spell { get { return b.spells[classIndex]; } }
        public bool Done { get { return dead || won; } }
        public Simulation(Balance config, int cls, int seed) {
            b = config; classIndex = cls; random = new System.Random(seed);
            hp = maxHp = b.playerHp; spawnAt = b.spawnInterval; scrollAt = b.scrollPeriod;
        }
        public float Param(string id, string key, float fallback = 0) {
            return owned.Contains(id) ? b.Scroll(id).Get(key, fallback) : 0;
        }
        public float Armor { get { return Param("bulwark", "armorPerLevel") * level; } }
        public float MissingHp { get { return Mathf.Clamp01(1 - hp / maxHp); } }
        public float MoveSpeed {
            get {
                float bonus = speedBonus + Param("blood_speed", "maxMoveSpeedBonusPercent") * MissingHp;
                bonus += Armor * Param("offensive_defense", "moveSpeedPercentPerArmor");
                bonus += breathStacks * Param("second_breath", "moveSpeedPercentPerStack");
                if (time < fleeUntil) bonus += Param("fight_or_flight", "moveSpeedPercent");
                return b.playerSpeed * Mathf.Max(b.movementFloor, 1 + bonus / 100);
            }
        }
        public float Interval {
            get {
                float interval = 1 / (Spell.fireRate * (1 + rateBonus / 100));
                if (owned.Contains("focus") && stillTime >= Param("focus", "stationaryDelaySeconds"))
                    interval *= Param("focus", "finalAttackIntervalMultiplier");
                return interval;
            }
        }
        public float Damage {
            get {
                float pct = damageBonus + Armor * Param("offensive_defense", "damagePercentPerArmor");
                if (owned.Contains("last_stand") && hp / maxHp <= Param("last_stand", "hpRatioThresholdInclusive"))
                    pct += Param("last_stand", "damagePercent");
                return Spell.damage * (1 + pct / 100);
            }
        }
        public Enemy Nearest(float range = float.MaxValue) {
            Enemy result = null; float dist = range * range;
            foreach (var e in enemies) {
                if (e.hp <= 0) continue;
                float d = (e.pos - player).sqrMagnitude;
                if (d < dist || (d == dist && (result == null || e.id < result.id))) { result = e; dist = d; }
            }
            return result;
        }
        public Drop NearestXp() {
            Drop result=null; float distance=float.MaxValue;
            foreach(var d in drops) if(!d.potion && (d.pos-player).sqrMagnitude<distance) { result=d; distance=(d.pos-player).sqrMagnitude; }
            return result;
        }
        public void GainXp(float amount) {
            if(amount<0) return;
            float pct=Param("hard_lessons","maxXpBonusPercent")*MissingHp+Param("enlightenment","xpPercent");
            float gained=amount*(1+pct/100); xp+=gained; totalXp+=gained;
            lastPickupAt=time; lastPickupText="+"+gained.ToString("F1")+" XP";
            while(xp>=b.RequiredXp(level)) {
                xp-=b.RequiredXp(level); level++;
                rewards.Enqueue(new RewardRequest{level=level});
                foreach(int milestone in b.infusionLevels) if(level==milestone)
                    rewards.Enqueue(new RewardRequest{infusion=true,level=level});
                Heal(maxHp*Param("healthy_growth","healMaxHpPercent")/100);
            }
            EnsureOffer();
        }
        public void Step(Vector2 input, float dt, bool autoRewards) {
            if (Done) return;
            if (offers.Count > 0 || rewards.Count > 0) {
                EnsureOffer(); if (autoRewards && offers.Count > 0) ChooseAutomatically();
                return;
            }
            time += dt;
            Vector2 dir = Vector2.ClampMagnitude(input, 1);
            if (dir.sqrMagnitude > 0) facing = dir.normalized;
            Vector2 previous = player;
            player += dir * MoveSpeed * dt;
            player = new Vector2(Mathf.Clamp(player.x, -b.arenaHalfExtent, b.arenaHalfExtent), Mathf.Clamp(player.y, -b.arenaHalfExtent, b.arenaHalfExtent));
            float distance = Vector2.Distance(previous, player);
            bool stationary = distance / dt <= b.stationaryThreshold;
            stillTime = stationary ? stillTime + dt : 0;
            if (wasStill != stationary) breathTime = 0;
            wasStill = stationary;
            if (owned.Contains("second_breath")) {
                breathTime += dt;
                float period = Param("second_breath", "tickSeconds");
                while (breathTime >= period) { breathTime -= period; breathStacks = Mathf.Clamp(breathStacks + (stationary ? 1 : -1), 0, (int)Param("second_breath", "maxStacks")); }
            }
            if (classIndex == 2 && distance > 0) {
                float needed = Spell.spacing - rockDistance;
                while (needed <= distance + 0.00001f) {
                    rocks.Add(new Rock { pos = Vector2.Lerp(previous, player, needed / distance), expires = time + Spell.life * (1 + lifeBonus / 100) });
                    needed += Spell.spacing;
                }
                rockDistance = (rockDistance + distance) % Spell.spacing;
            }
            rocks.RemoveAll(x => x.expires <= time);
            while (time >= spawnAt) {
                spawnAt += b.spawnInterval;
                if (enemies.Count < b.maxEnemies) {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2;
                    enemies.Add(new Enemy { id = nextId++, hp = b.enemyHp, pos = player + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * b.spawnRadius });
                }
            }
            foreach (var e in enemies) {
                if (e.hp <= 0) continue;
                UpdateElementStatus(e);
                if(e.hp<=0) continue;
                float speed = b.enemySpeed;
                if (time < e.slowUntil) speed *= 1 - Param("frost_mark", "slowPercent") / 100;
                if(time<e.chillUntil) speed*=1-b.Element("Ice").slowPercent/100;
                if(time<e.frozenUntil) speed=0;
                e.pos = Vector2.MoveTowards(e.pos, player, speed * dt);
                if(time<e.knockUntil) e.pos+=e.knockVelocity*dt;
                if ((e.pos - player).sqrMagnitude <= 0.64f && time >= e.contactAt) {
                    e.contactAt = time + b.contactCooldown; TakeDamage(b.contactDamage*(time<e.weakenUntil?1-e.weakenPercent/100:1));
                    if (dead) break;
                }
            }
            if (dead) return;
            castProgress += dt / Interval;
            while (castProgress >= 1) { castProgress -= 1; Cast(); }
            for (int i = bolts.Count - 1; i >= 0; i--) {
                var bolt = bolts[i]; Vector2 previousBolt = bolt.pos;
                bolt.pos += bolt.dir * Spell.speed * dt;
                Enemy hit = null; float closestAlong = float.MaxValue;
                foreach (var e in enemies) if (e.hp > 0) {
                    Vector2 travel = bolt.pos - previousBolt;
                    float along = Mathf.Clamp01(Vector2.Dot(e.pos - previousBolt, travel) / Mathf.Max(0.000001f, travel.sqrMagnitude));
                    if ((e.pos - (previousBolt + travel * along)).sqrMagnitude <= Spell.radius * Spell.radius && along < closestAlong) { hit = e; closestAlong = along; }
                }
                if (hit != null) Hit(hit);
                if (hit != null || time >= bolt.expires) bolts.RemoveAt(i);
            }
            foreach (var slam in slams) if (!slam.resolved && time >= slam.resolveAt) {
                slam.resolved = true; Vector2 side = new Vector2(slam.dir.y, -slam.dir.x);
                foreach (var e in enemies) if (e.hp > 0) {
                    Vector2 delta = e.pos - slam.pos; float forward = Vector2.Dot(delta, slam.dir);
                    if (forward >= 0 && forward <= Spell.length && Mathf.Abs(Vector2.Dot(delta, side)) <= Spell.width / 2) Hit(e);
                }
            }
            slams.RemoveAll(x => time >= x.expires);
            ResolveElementExplosions();
            enemies.RemoveAll(x => x.hp <= 0);
            float pickup = b.pickupRadius * (1 + Param("attraction", "pickupRadiusPercent") / 100);
            for (int i = drops.Count - 1; i >= 0; i--) {
                var d=drops[i];
                float magnet=b.magnetRadius*(1+Param("attraction","pickupRadiusPercent")/100);
                if((d.pos-player).sqrMagnitude<=magnet*magnet) d.attracted=true;
                if(d.attracted) d.pos=Vector2.MoveTowards(d.pos,player,b.magnetSpeed*dt);
                if((d.pos-player).sqrMagnitude>pickup*pickup) continue;
                drops.RemoveAt(i);
                if (d.potion) {
                    float gain = Param("elixir_permanence", "maxHpPerPotion"); maxHp += gain; hp += gain;
                    Heal(maxHp * b.potionHealPercent / 100);
                } else {
                    GainXp(d.xp); collectedOrbs++;
                    if (random.NextDouble() * 100 < Param("mana_siphon", "chancePercent")) Heal(Param("mana_siphon", "healHp"));
                }
            }
            // Also admit externally granted/debug XP through the same progression path.
            if(xp>=b.RequiredXp(level)) GainXp(0);
            if (owned.Contains("regeneration") && time >= regenAt) {
                regenAt += Param("regeneration", "periodSeconds"); Heal(maxHp * Param("regeneration", "healMaxHpPercent") / 100);
            }
            if (owned.Contains("shield") && time >= shieldAt) {
                shieldAt += Param("shield", "periodSeconds");
                if (shield <= 0) shield = maxHp * Param("shield", "maxHpPercent") / 100;
            }
            if (time >= scrollAt) { scrollAt += b.scrollPeriod; if (owned.Count < b.scrolls.Length) rewards.Enqueue(new RewardRequest{scroll=true,level=level}); }
            EnsureOffer();
            won = time >= b.duration;
        }
        void Cast() {
            if (classIndex == 0) {
                pulseUntil = time + 0.18f; float radius = Spell.radius * (1 + radiusBonus / 100);
                foreach (var e in enemies) if (e.hp > 0 && (e.pos - player).sqrMagnitude <= radius * radius) Hit(e);
            } else if (classIndex == 1) {
                var target = Nearest(Spell.range);
                if (target != null) bolts.Add(new Bolt { pos = player, dir = (target.pos - player).normalized, expires = time + Spell.life });
            } else if (classIndex == 2) {
                foreach (var e in enemies) if (e.hp > 0) {
                    foreach (var rock in rocks) if ((e.pos - rock.pos).sqrMagnitude <= Spell.radius * Spell.radius) { Hit(e); break; }
                }
            } else slams.Add(new AttackVisual { pos = player, dir = facing, resolveAt = time + Spell.telegraph, expires = time + Spell.telegraph + 0.25f });
        }
        public void Heal(float amount) { hp = Mathf.Min(maxHp, hp + amount); }
        public void TakeDamage(float raw) {
            if (Done) return;
            if (random.NextDouble() * 100 < b.baseDodge) { Heal(raw * Param("evasion_mend", "healAvoidedDamagePercent") / 100); return; }
            float damage = raw * 100 / (100 + Mathf.Max(0, Armor));
            float absorb = Mathf.Min(shield, damage); shield -= absorb; damage -= absorb;
            hp = Mathf.Max(0, hp - damage); damageTaken += damage;
            if (damage > 0 && owned.Contains("fight_or_flight") && time >= fleeAt + Param("fight_or_flight", "cooldownFromActivationSeconds") && time >= fleeUntil) {
                fleeAt = time; fleeUntil = time + Param("fight_or_flight", "durationSeconds");
            }
            dead = hp <= 0;
        }
        public void Hit(Enemy e) {
            if (e.hp <= 0) return;
            bool crit = random.NextDouble() * 100 < b.baseCrit;
            float amount = Damage * (crit ? b.baseCritMultiplier + critBonus / 100 : 1);
            var element=ActiveElement;
            if(element!=null && element.corruptionFactor>0) {
                e.corruptionDamage=amount*element.corruptionFactor;
                e.corruptionSpreadDamage=amount*element.corruptionSpreadFactor;
            }
            DealDamage(e,amount,crit);
            if (owned.Contains("frost_mark") && e.hp>0) e.slowUntil = time + Param("frost_mark", "durationSeconds");
            if(element!=null) ApplyElementOnHit(e,amount,element);
        }
        public void GrantScroll(string id) {
            if (b.Scroll(id) == null || !owned.Add(id)) return;
            if (id == "regeneration") regenAt = time + Param(id, "periodSeconds");
            if (id == "shield") shieldAt = time + Param(id, "periodSeconds");
        }
        void EnsureOffer() {
            if (offers.Count > 0 || rewards.Count == 0) return;
            var request=rewards.Dequeue(); rewardLevel=request.level; var pool = new List<Reward>();
            rewardTitle=request.infusion?"속성 부여 "+(infusions.Count+1)+" / 2":request.scroll?"두루마리 선택":"레벨 "+request.level+" — 강화 선택";
            if(request.infusion) {
                if(infusions.Count>=2) { EnsureOffer(); return; }
                foreach(string id in new[]{"Fire","Lightning","Acid","Ice","Dark"}) {
                    var effect=b.CombinedElement(infusions.Count>0?infusions[0]:id,infusions.Count>0?id:null);
                    pool.Add(new Reward{id=id,infusion=true,label=effect.name+"\n"+effect.description});
                }
            } else if (request.scroll) {
                foreach (var s in b.scrolls) if (!owned.Contains(s.id)) pool.Add(new Reward { id = s.id, label = s.name + "\n" + s.description, scroll = true });
            } else {
                pool.Add(Upgrade("damage", "주문 피해", b.upgradePercent));
                pool.Add(Upgrade("rate", "시전 속도", b.upgradePercent));
                if (classIndex == 0) {
                    var r = Upgrade("radius", "파동 반지름", b.pulseSizePercent);
                    if (r.rarity > 0) pool.Add(r);
                }
                if (classIndex == 2) pool.Add(Upgrade("life", "돌 지속 시간", b.upgradePercent));
                pool.Add(Upgrade("hp", "최대 체력", b.commonHp));
                pool.Add(Upgrade("speed", "이동 속도", b.commonSpeed));
            }
            while (offers.Count < b.offerCount && pool.Count > 0) {
                int index = random.Next(pool.Count); offers.Add(pool[index]); pool.RemoveAt(index);
            }
        }
        Reward Upgrade(string id, string label, float[] values) {
            float total = 0; foreach (float w in b.rarityWeights) total += w;
            float pick = (float)random.NextDouble() * total; int r = b.rarityWeights.Length - 1;
            for (int i = 0; i < b.rarityWeights.Length; i++) { pick -= b.rarityWeights[i]; if (pick < 0) { r = i; break; } }
            return new Reward { id = id, rarity = r, value = values[r], label = label + " +" + values[r] + (id == "hp" ? "" : "%") + "\n" + new[] { "Common", "Rare", "Epic", "Legendary" }[r] };
        }
        public void Choose(int index) {
            if (index < 0 || index >= offers.Count) return;
            var r = offers[index]; offers.Clear();
            if(r.infusion) { if(infusions.Count<2) infusions.Add(r.id); }
            else if (r.scroll) GrantScroll(r.id);
            else {
                switch (r.id) {
                    case "damage": damageBonus += r.value; break;
                    case "rate": rateBonus += r.value; break;
                    case "radius": radiusBonus += r.value; break;
                    case "life": lifeBonus += r.value; break;
                    case "hp": maxHp += r.value; hp += r.value; break;
                    case "speed": speedBonus += r.value; break;
                }
                if (r.rarity == 0) critBonus += Param("humble_refinement", "critMultiplierPercentagePointsPerCommonChoice");
            }
            EnsureOffer();
        }
        public void ChooseAutomatically() {
            int index=rewardChooser.Select(this);
            if(index<0) return;
            var reward=offers[index];
            lastAutoChoice=(reward.label??reward.id).Split('\n')[0]+" — "+rewardChooser.Reason(this,index);
            automaticChoices++; Choose(index);
        }
    }
}
