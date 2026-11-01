using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SurvivorLab.Editor {
    // Headless balance measurement and failure replay. Runs the same Simulation as the game.
    //   balance.cmd                   -> class x mover x reward-chooser matrix, writes docs/reports/data
    //   balance.cmd --replay c s m r  -> re-runs one seed twice, checks determinism, prints timeline
    public static class BalanceLab {
        static readonly string[] ClassNames = { "공명술사", "유도술사", "각인술사", "충격술사" };
        const float Dt = 0.05f;
        public const int GameSeed = 90210; // seed GameApp uses for WATCH AI

        public sealed class Sample { public float t, hp, maxHp, x, y; public int enemies, near, level, kills; }
        public sealed class Run {
            public int cls, seed; public string mover, chooser;
            public bool won; public float time, damageDealt, damageTaken, maxHp, minHpRatio = 1;
            public int level, kills, wave, peakEnemies, scrolls, upgradesTaken; public float capReachedAt = -1, level7At = -1, level14At = -1;
            public string infusions = "", cause = "", choices = "";
            public int nearAtDeath, contactAtDeath, slams, slamWhiffs, slamBackward; public float wallTime; public float wallDistAtDeath, damageLast15;
            public List<Sample> trace = new List<Sample>();
        }

        interface IMover { string Name { get; } Vector2 Move(Simulation s); }
        sealed class QMover : IMover {
            readonly Learning agent = new Learning();
            public QMover(PolicyFile policy) { agent.Use(policy); }
            public string Name { get { return "q"; } }
            public Vector2 Move(Simulation s) { return Learning.Actions[agent.Act(Learning.Observe(s), 0)]; }
        }
        // Scripted baseline: flee the local crowd, circle-strafe, stay off walls, grab XP when calm.
        sealed class KiteMover : IMover {
            public string Name { get { return "kite"; } }
            public Vector2 Move(Simulation s) {
                Vector2 push = Vector2.zero; float threat = 0;
                foreach (var e in s.enemies) {
                    Vector2 d = s.player - e.pos; float m = d.magnitude;
                    if (m < 7 && m > 0.001f) { push += d / (m * m); threat += 1 / m; }
                }
                float h = s.b.arenaHalfExtent;
                Vector2 wall = new Vector2(Wall(h + s.player.x) - Wall(h - s.player.x), Wall(h + s.player.y) - Wall(h - s.player.y));
                if (threat < 0.6f) {
                    var orb = s.NearestXp();
                    Vector2 goal = orb != null ? orb.pos - s.player : -s.player * 0.05f;
                    return (goal.normalized + wall * 2).normalized;
                }
                Vector2 tangent = new Vector2(-push.y, push.x).normalized;
                if (Vector2.Dot(tangent, -s.player) < 0) tangent = -tangent; // circle toward open space
                return (push.normalized + tangent * 0.6f + wall * 1.5f).normalized;
            }
            static float Wall(float distance) { return distance < 10 ? (10 - distance) / 10 : 0; }
        }
        sealed class StillMover : IMover { public string Name { get { return "still"; } } public Vector2 Move(Simulation s) { return Vector2.zero; } }

        static PolicyFile movePolicy; static RewardPolicyFile rewardPolicy;
        static void LoadPolicies() {
            if (movePolicy != null) return;
            string policyPath = Arg("-policy", Path.Combine(Application.streamingAssetsPath, "initial-policy-v2.json"));
            movePolicy = JsonUtility.FromJson<PolicyFile>(File.ReadAllText(policyPath));
            rewardPolicy = JsonUtility.FromJson<RewardPolicyFile>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "reward-policy.json")));
        }
        static IMover Mover(string name) {
            switch (name) { case "q": return new QMover(movePolicy); case "kite": return new KiteMover(); case "still": return new StillMover(); }
            throw new Exception("Unknown mover " + name);
        }
        static IRewardChooser Chooser(Balance b, string name) {
            switch (name) {
                case "learned": return new RewardLearner(rewardPolicy, 1);
                case "rule": return RuleRewardChooser.Instance;
                case "random": return new FixedRewardChooser(false, 7);
            }
            throw new Exception("Unknown chooser " + name);
        }

        // A slam whiffs when its rectangle holds no enemy at cast time; backward means the nearest enemy is behind it.
        static void CountSlam(Simulation s, AttackVisual slam, Run run) {
            run.slams++; Vector2 side = new Vector2(slam.dir.y, -slam.dir.x); bool any = false;
            foreach (var e in s.enemies) {
                Vector2 d = e.pos - slam.pos; float f = Vector2.Dot(d, slam.dir);
                if (e.hp > 0 && f >= 0 && f <= s.Spell.length && Mathf.Abs(Vector2.Dot(d, side)) <= s.Spell.width / 2) { any = true; break; }
            }
            if (!any) run.slamWhiffs++;
            var n = s.Nearest(); if (n != null && Vector2.Dot(n.pos - s.player, slam.dir) < 0) run.slamBackward++;
        }
        static float WallDistance(Simulation s) { float h = s.b.arenaHalfExtent; return Mathf.Min(h - Mathf.Abs(s.player.x), h - Mathf.Abs(s.player.y)); }
        static int Near(Simulation s, float r) { int n = 0; foreach (var e in s.enemies) if (e.hp > 0 && (e.pos - s.player).sqrMagnitude <= r * r) n++; return n; }

        public static Run Play(Balance b, int cls, int seed, string mover, string chooser, float sampleEvery = 1f) {
            LoadPolicies();
            var m = Mover(mover);
            var s = new Simulation(b, cls, seed) { rewardChooser = Chooser(b, chooser) };
            var run = new Run { cls = cls, seed = seed, mover = mover, chooser = chooser };
            var choices = new StringBuilder();
            Vector2 input = Vector2.zero; float actionAt = 0, sampleAt = 0; int guard = 0;
            var hpHistory = new Queue<KeyValuePair<float, float>>();
            while (!s.Done) {
                if (s.offers.Count > 0 || s.rewards.Count > 0) {
                    int before = s.automaticChoices; s.Step(Vector2.zero, Dt, true);
                    if (s.automaticChoices > before) { if (choices.Length > 0) choices.Append('|'); choices.Append(s.time.ToString("F0", CultureInfo.InvariantCulture)).Append(':').Append(s.lastAutoChoice.Split('—')[0].Trim().Replace('\n', ' ')); }
                    if (++guard > 1000) throw new Exception("Reward queue stuck");
                    continue;
                }
                guard = 0;
                if (s.time >= actionAt) { input = m.Move(s); actionAt = s.time + 0.1f; }
                int slamCount = s.slams.Count;
                s.Step(input, Dt, true);
                if (s.slams.Count > slamCount) CountSlam(s, s.slams[s.slams.Count - 1], run);
                if (WallDistance(s) < 2) run.wallTime += Dt;
                run.peakEnemies = Mathf.Max(run.peakEnemies, s.enemies.Count);
                if (run.capReachedAt < 0 && s.enemies.Count >= b.maxEnemies) run.capReachedAt = s.time;
                if (run.level7At < 0 && s.level >= 7) run.level7At = s.time;
                if (run.level14At < 0 && s.level >= 14) run.level14At = s.time;
                run.minHpRatio = Mathf.Min(run.minHpRatio, s.hp / s.maxHp);
                hpHistory.Enqueue(new KeyValuePair<float, float>(s.time, s.damageTaken));
                while (hpHistory.Peek().Key < s.time - 15) hpHistory.Dequeue();
                if (s.time >= sampleAt || s.Done) {
                    sampleAt += sampleEvery;
                    run.trace.Add(new Sample { t = s.time, hp = s.hp, maxHp = s.maxHp, x = s.player.x, y = s.player.y, enemies = s.enemies.Count, near = Near(s, 3), level = s.level, kills = s.kills });
                }
            }
            run.won = s.won; run.time = s.time; run.wave = s.WaveNumber; run.level = s.level; run.kills = s.kills; run.maxHp = s.maxHp;
            run.damageDealt = s.damageDealt; run.damageTaken = s.damageTaken; run.scrolls = s.owned.Count;
            run.infusions = string.Join("+", s.infusions.ToArray()); run.choices = choices.ToString();
            run.upgradesTaken = s.automaticChoices;
            if (!s.won) {
                run.nearAtDeath = Near(s, 3); run.contactAtDeath = Near(s, 0.8f); run.wallDistAtDeath = WallDistance(s);
                run.damageLast15 = s.damageTaken - hpHistory.Peek().Value;
                run.cause = run.wallDistAtDeath < 2 ? "cornered" : run.capReachedAt >= 0 ? "overrun" : run.contactAtDeath >= 4 ? "surrounded" : "attrition";
            }
            return run;
        }

        static string Arg(string name, string fallback) {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        static string Csv(string v) { return "\"" + v.Replace("\"", "'") + "\""; }

        public static void Measure() {
            var b = Balance.Load(); LoadPolicies();
            int seeds = int.Parse(Arg("-seeds", "40"));
            string outDir = Path.GetFullPath(Arg("-out", "docs/reports/data"));
            Directory.CreateDirectory(outDir);
            var configs = new[] { new[] { "q", "learned" }, new[] { "q", "rule" }, new[] { "kite", "learned" }, new[] { "kite", "rule" }, new[] { "still", "learned" }, new[] { "q", "random" } };
            var runs = new List<Run>(); var clock = System.Diagnostics.Stopwatch.StartNew();
            foreach (var c in configs) for (int cls = 0; cls < 4; cls++) {
                for (int i = 0; i < seeds; i++) runs.Add(Play(b, cls, 1000 + i, c[0], c[1]));
                runs.Add(Play(b, cls, GameSeed, c[0], c[1]));
            }
            var csv = new StringBuilder("class,className,seed,mover,chooser,won,time,wave,level,kills,damageDealt,dps,damageTaken,maxHp,minHpRatio,peakEnemies,capReachedAt,level7At,level14At,scrolls,infusions,cause,nearAtDeath,contactAtDeath,wallDistAtDeath,damageLast15,wallTimeRatio,slams,slamWhiffRate,slamBackwardRate,choices\n");
            foreach (var r in runs)
                csv.AppendLine(string.Join(",", new[] { r.cls.ToString(), ClassNames[r.cls], r.seed.ToString(), r.mover, r.chooser, r.won ? "1" : "0", F(r.time), r.wave.ToString(), r.level.ToString(), r.kills.ToString(), F(r.damageDealt), F(r.damageDealt / Mathf.Max(1, r.time)), F(r.damageTaken), F(r.maxHp), F(r.minHpRatio), r.peakEnemies.ToString(), F(r.capReachedAt), F(r.level7At), F(r.level14At), r.scrolls.ToString(), r.infusions, r.cause, r.nearAtDeath.ToString(), r.contactAtDeath.ToString(), F(r.wallDistAtDeath), F(r.damageLast15), F(r.wallTime / Mathf.Max(1, r.time)), r.slams.ToString(), F(r.slamWhiffs / (float)Mathf.Max(1, r.slams)), F(r.slamBackward / (float)Mathf.Max(1, r.slams)), Csv(r.choices) }));
            File.WriteAllText(Path.Combine(outDir, "runs.csv"), csv.ToString(), new UTF8Encoding(true));
            var sum = new StringBuilder("mover,chooser,class,className,runs,wins,winRate,meanTime,medianDeath,meanLevel,meanKills,meanDps\n");
            foreach (var g in runs.Where(r => r.seed != GameSeed).GroupBy(r => r.mover + "," + r.chooser + "," + r.cls)) {
                var list = g.ToList(); var deaths = list.Where(r => !r.won).Select(r => r.time).OrderBy(x => x).ToList();
                sum.AppendLine(string.Join(",", new[] { g.Key, ClassNames[list[0].cls], list.Count.ToString(), list.Count(r => r.won).ToString(), F(list.Count(r => r.won) / (float)list.Count), F(list.Average(r => r.time)), deaths.Count > 0 ? F(deaths[deaths.Count / 2]) : "", F((float)list.Average(r => r.level)), F((float)list.Average(r => r.kills)), F(list.Average(r => r.damageDealt / Mathf.Max(1, r.time))) }));
            }
            File.WriteAllText(Path.Combine(outDir, "summary.csv"), sum.ToString(), new UTF8Encoding(true));
            Console.WriteLine(sum.ToString());
            Console.WriteLine("BALANCE MEASURE DONE: " + runs.Count + " runs in " + clock.Elapsed.TotalSeconds.ToString("F1") + "s -> " + outDir);
        }

        // Continues movement Q-learning on the current balance with the game's Trainer and saves to -save.
        public static void Train() {
            var b = Balance.Load(); LoadPolicies();
            int episodes = int.Parse(Arg("-episodes", "8000"));
            string save = Path.GetFullPath(Arg("-save", "Logs/policy-trained.json"));
            var agent = new Learning(); agent.Use(movePolicy);
            var trainer = new Trainer(b, agent, episodes, new RewardLearner(rewardPolicy, 1));
            var clock = System.Diagnostics.Stopwatch.StartNew(); int reported = 0;
            while (trainer.active) {
                trainer.Pump(20000);
                if (trainer.completed >= reported + 1000) { reported = trainer.completed; Console.WriteLine("TRAIN " + reported + "/" + episodes + " last=" + F(trainer.lastSurvival) + "s " + clock.Elapsed.TotalSeconds.ToString("F0") + "s"); }
            }
            agent.Save(save);
            Console.WriteLine("TRAIN DONE: " + episodes + " episodes, states=" + agent.data.rows.Count + " -> " + save);
        }

        // Re-runs one failure twice: identical results prove the failure reproduces from (class, seed, mover, chooser).
        public static void Replay() {
            var b = Balance.Load();
            int cls = int.Parse(Arg("-class", "0")), seed = int.Parse(Arg("-seed", GameSeed.ToString()));
            string mover = Arg("-mover", "q"), chooser = Arg("-chooser", "learned");
            float every = float.Parse(Arg("-every", "5"), CultureInfo.InvariantCulture);
            var a = Play(b, cls, seed, mover, chooser, every); var c = Play(b, cls, seed, mover, chooser, every);
            bool same = a.won == c.won && a.time == c.time && a.kills == c.kills && a.damageTaken == c.damageTaken && a.choices == c.choices;
            var sb = new StringBuilder();
            sb.AppendLine("REPLAY " + ClassNames[cls] + " seed=" + seed + " mover=" + mover + " chooser=" + chooser);
            sb.AppendLine("result=" + (a.won ? "WIN" : "DEATH") + " time=" + F(a.time) + " level=" + a.level + " kills=" + a.kills + " infusions=" + a.infusions + " cause=" + a.cause);
            sb.AppendLine("deterministic=" + same + " wallTime=" + F(a.wallTime) + "s" + (cls == 3 ? " slams=" + a.slams + " whiff=" + a.slamWhiffs + " backward=" + a.slamBackward : ""));
            sb.AppendLine("  t     hp/max   lvl kills enemies near3 pos");
            foreach (var p in a.trace) sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,5:0.0} {1,5:0}/{2,-4:0} {3,3} {4,5} {5,7} {6,5} ({7:0.0},{8:0.0})", p.t, p.hp, p.maxHp, p.level, p.kills, p.enemies, p.near, p.x, p.y));
            sb.AppendLine("choices: " + a.choices.Replace("|", "\n  "));
            string outFile = Arg("-out", "");
            if (outFile != "") { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))); File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(true)); }
            Console.WriteLine(sb.ToString());
            if (!same) throw new Exception("Replay not deterministic");
        }
    }
}
