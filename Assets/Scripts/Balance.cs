using System;
using System.IO;
using UnityEngine;

namespace SurvivorLab {
    [Serializable] public class NumberSetting { public string key; public float value; }
    [Serializable] public class ScrollDefinition {
        public string id, name, description;
        public NumberSetting[] values;
        public float Get(string key, float fallback = 0) {
            foreach (var v in values) if (v.key == key) return v.value;
            return fallback;
        }
    }
    [Serializable] public class SpellDefinition {
        public string id, name; public float damage, fireRate;
        public float radius, range, speed, life, spacing, length, width, telegraph;
    }
    [Serializable] public class XpSegment { public int minLevel; public float baseValue, growth; }
    // Active from `start` seconds until the next wave. The spawner keeps at least minEnemies alive.
    [Serializable] public class WaveDefinition {
        public float start, spawnInterval, hpScale = 1, speedScale = 1, damageScale = 1, xp, blockChance, fastChance;
        public int minEnemies, perSpawn = 1;
    }
    // A straight wall of enemies that sweeps past the player with one gap in it.
    [Serializable] public class LineEventDefinition { public float time, gap; }
    [Serializable] public class ElementDefinition {
        public string id, name, description;
        public float burnFactor, burnInterval, acidDuration, vulnerabilityPercent, chillDuration, slowPercent;
        public int burnTicks, chainJumps;
        public float chainFactor, chainRange, extraStrikeFactor, freezeChance, freezeDuration, freezeImmunity;
        public float corruptionFactor, explosionRadius, splashFactor, splashRadius, knockbackSpeed, knockbackDuration;
        public float enemyDamageReductionPercent,corruptionSpreadFactor;
    }
    [Serializable] public class Balance {
        public int version = 1;
        public float playerHp, playerSpeed, enemyHp, enemySpeed, contactDamage, contactCooldown;
        public float spawnInterval, enemyXp, pickupRadius, potionHealPercent, scrollPeriod, duration;
        public float baseDodge, baseCrit, baseCritMultiplier, stationaryThreshold, movementFloor;
        public int maxEnemies, offerCount;
        public float[] upgradePercent, pulseSizePercent, commonHp, commonSpeed, rarityWeights;
        public SpellDefinition[] spells;
        public ScrollDefinition[] scrolls;
        public XpSegment[] xpSegments;
        public float xpScale;
        public int[] infusionLevels;
        public ElementDefinition[] elements;
        public float magnetRadius, magnetSpeed, spawnRadius, arenaHalfExtent;
        public int potionEveryKills;
        public WaveDefinition[] waves;
        public LineEventDefinition[] lineEvents;
        public float topUpInterval, fastSpeedMultiplier, fastHpMultiplier, blockSpreadDegrees, despawnDistance;
        public float lineSpeed, lineSpacing, lineDistance, lineHalfLength;
        public bool HasWaves { get { return waves != null && waves.Length > 0; } }
        public WaveDefinition WaveAt(float time) {
            var current = waves[0];
            foreach (var w in waves) if (time >= w.start) current = w;
            return current;
        }
        public ElementDefinition Element(string id) { return Array.Find(elements,x=>x.id==id); }
        public ElementDefinition CombinedElement(string first,string second) {
            if(string.IsNullOrEmpty(first)) return null;
            if(string.IsNullOrEmpty(second)) return Element(first);
            if(first==second) return Element(first+"Double");
            return Element(first+second) ?? Element(second+first);
        }
        public float RequiredXp(int level) {
            XpSegment s = xpSegments[0];
            foreach (var part in xpSegments) if (level >= part.minLevel) s = part;
            return (2 * level - 1) * (s.baseValue + (level - s.minLevel) * s.growth) * xpScale;
        }
        public ScrollDefinition Scroll(string id) { return Array.Find(scrolls, x => x.id == id); }
        public static Balance Load() {
            string file = Path.Combine(Application.streamingAssetsPath, "balance.json");
            var b = JsonUtility.FromJson<Balance>(File.ReadAllText(file));
            if (b.spells.Length != 4 || b.scrolls.Length != 19) throw new Exception("Invalid balance catalog");
            if(b.offerCount!=3 || b.infusionLevels.Length!=2 || b.elements.Length!=20) throw new Exception("Invalid progression config");
            return b;
        }
    }
}
