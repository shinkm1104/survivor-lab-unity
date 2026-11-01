using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SurvivorLab {
    [Serializable] public class QRow { public int state; public float[] values = new float[9]; }
    [Serializable] public class PolicyFile { public int version = 2, episodes; public List<QRow> rows = new List<QRow>(); }
    public sealed class Learning {
        public static readonly Vector2[] Actions = { Vector2.zero, Vector2.up, new Vector2(1,1).normalized, Vector2.right, new Vector2(1,-1).normalized, Vector2.down, new Vector2(-1,-1).normalized, Vector2.left, new Vector2(-1,1).normalized };
        public PolicyFile data = new PolicyFile();
        readonly Dictionary<int,QRow> table = new Dictionary<int,QRow>();
        readonly System.Random random = new System.Random(47219);
        public static int Observe(Simulation s) {
            var enemy = s.Nearest(); int direction = 8, distance = 3;
            if (enemy != null) {
                Vector2 delta = enemy.pos - s.player;
                direction = ((int)Mathf.Round(Mathf.Atan2(delta.y, delta.x) / (Mathf.PI / 4)) + 8) % 8;
                distance = delta.magnitude < 2 ? 0 : delta.magnitude < 5 ? 1 : 2;
            }
            float edge=s.b.arenaHalfExtent-6;
            int wallX = s.player.x < -edge ? 0 : s.player.x > edge ? 2 : 1;
            int wallY = s.player.y < -edge ? 0 : s.player.y > edge ? 2 : 1;
            int health = Mathf.Clamp((int)(s.hp / s.maxHp * 3), 0, 2);
            var orb=s.NearestXp(); int xpDirection=8,xpDistance=2;
            if(orb!=null) {
                Vector2 delta=orb.pos-s.player;
                xpDirection=((int)Mathf.Round(Mathf.Atan2(delta.y,delta.x)/(Mathf.PI/4))+8)%8;
                xpDistance=delta.magnitude<=s.b.magnetRadius?0:1;
            }
            int state=(((((s.classIndex * 9 + direction) * 4 + distance) * 3 + wallX) * 3 + wallY) * 3 + health);
            return ((state*9+xpDirection)*3+xpDistance)*3+s.infusions.Count;
        }
        QRow Row(int state) {
            QRow row;
            if (!table.TryGetValue(state, out row)) { row = new QRow { state = state }; table.Add(state,row); data.rows.Add(row); }
            return row;
        }
        public int Act(int state, float epsilon) {
            if (random.NextDouble() < epsilon) return random.Next(9);
            QRow row;
            if (!table.TryGetValue(state, out row)) return random.Next(9);
            int best = 0; float value = float.NegativeInfinity; int ties = 0;
            for (int i=0;i<9;i++) {
                if (row.values[i] > value) { best = i; value = row.values[i]; ties = 1; }
                else if (row.values[i] == value && random.Next(++ties) == 0) best = i;
            }
            return best;
        }
        public void UpdateQ(int state, int action, float reward, int next, bool terminal) {
            QRow row = Row(state); float max = 0;
            if (!terminal) { max = float.NegativeInfinity; foreach(float q in Row(next).values) max = Mathf.Max(max,q); }
            row.values[action] += 0.14f * (reward + 0.98f * max - row.values[action]);
        }
        public void Save(string path) { File.WriteAllText(path, JsonUtility.ToJson(data, true)); }
        public void Load(string path) {
            if (!File.Exists(path)) return;
            Use(JsonUtility.FromJson<PolicyFile>(File.ReadAllText(path)));
        }
        public void Use(PolicyFile loaded) {
            if (loaded == null || loaded.version != 2) throw new Exception("Incompatible policy version");
            table.Clear(); data = loaded;
            foreach (var row in data.rows) { if(row.values.Length != 9) throw new Exception("Invalid policy row"); table[row.state] = row; }
        }
    }
    public sealed class Trainer {
        readonly Balance b; readonly Learning agent; public Simulation sim;
        public int completed, target; public float lastSurvival; public bool active;
        readonly IRewardChooser chooser;
        public Trainer(Balance balance, Learning policy, int episodes, IRewardChooser rewards=null) { b=balance; agent=policy; target=episodes; chooser=rewards??RuleRewardChooser.Instance; active=true; Next(); }
        void Next() { sim = new Simulation(b, (agent.data.episodes + completed) % 4, 1000 + agent.data.episodes + completed); sim.rewardChooser=chooser; }
        public void Pump(int decisions) {
            for(int i=0;i<decisions && active;i++) {
                if(sim.offers.Count>0 || sim.rewards.Count>0) { sim.Step(Vector2.zero,0.05f,true); continue; }
                int state=Learning.Observe(sim), action=agent.Act(state, Mathf.Max(0.06f,0.85f-0.0004f*(agent.data.episodes+completed)));
                int kills=sim.kills; float damage=sim.damageTaken, before=sim.time, xpBefore=sim.totalXp;
                sim.Step(Learning.Actions[action],0.05f,true); sim.Step(Learning.Actions[action],0.05f,true);
                float reward=(sim.time-before)*0.5f+(sim.kills-kills)*2+(sim.totalXp-xpBefore)*0.35f-(sim.damageTaken-damage)*0.2f+(sim.dead?-12:sim.won?12:0);
                agent.UpdateQ(state,action,reward,Learning.Observe(sim),sim.Done);
                if(sim.Done) { lastSurvival=sim.time; completed++; if(completed>=target) { active=false; agent.data.episodes+=completed; } else Next(); }
            }
        }
        public void Stop() { if(active) { active=false; agent.data.episodes+=completed; } }
    }
}
