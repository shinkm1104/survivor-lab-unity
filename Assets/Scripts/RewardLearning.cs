using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivorLab {
    public interface IRewardChooser {
        string Name { get; }
        int Select(Simulation s);
        string Reason(Simulation s, int index);
    }
    public sealed class RuleRewardChooser : IRewardChooser {
        public static readonly RuleRewardChooser Instance = new RuleRewardChooser();
        public string Name { get { return "규칙 기반"; } }
        public int Select(Simulation s) { return RewardAdvisor.Select(s); }
        public string Reason(Simulation s, int index) { return "규칙: " + RewardAdvisor.Reason(s, s.offers[index]); }
    }
    // Baselines used only for evaluation.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public sealed class FixedRewardChooser : IRewardChooser {
        readonly System.Random random; readonly bool first;
        public FixedRewardChooser(bool alwaysFirst, int seed) { first = alwaysFirst; random = new System.Random(seed); }
        public string Name { get { return first ? "첫 후보" : "무작위"; } }
        public int Select(Simulation s) { return s.offers.Count == 0 ? -1 : first ? 0 : random.Next(s.offers.Count); }
        public string Reason(Simulation s, int index) { return Name; }
    }

#endif
    [Serializable] public class RewardPolicyFile {
        public int version = 1, episodes;
        public string method = "reinforce";
        public string[] cards;
        public float[] weights, baseline;
    }

    // Learns which reward card to take from episode outcomes. Both methods share the same
    // linear features of (state, card); only the update rule differs.
    //   reinforce: softmax policy, Monte-Carlo return-to-go, learned state-value baseline.
    //   linear-q:  Q(s,card)=w·φ, semi-MDP one-step Q-learning between consecutive reward decisions.
    public sealed class RewardLearner : IRewardChooser {
        public static readonly string[] Upgrades = { "damage", "rate", "radius", "life", "hp", "speed" };
        public static readonly string[] Elements = { "Fire", "Lightning", "Acid", "Ice", "Dark" };
        public const int Classes = 4, BaselineSize = Classes * 4, MaxFeatures = 6;
        public RewardPolicyFile data;
        public bool explore; public float epsilon = 0.1f, learningRate = 0.01f;
        readonly Dictionary<string, int> cardIndex = new Dictionary<string, int>();
        readonly List<Decision> trace = new List<Decision>();
        readonly System.Random random;
        float advantageScale = 1;

        class Decision {
            public int[][] idx; public float[][] val; public int[] count; public float[] psi;
            public int chosen; public float objective;
        }

        public RewardLearner(Balance b, string method, int seed) {
            var cards = new List<string>(Upgrades);
            foreach (var s in b.scrolls) cards.Add(s.id);
            cards.AddRange(Elements);
            data = new RewardPolicyFile { method = method, cards = cards.ToArray() };
            data.weights = new float[FeatureSize]; data.baseline = new float[BaselineSize];
            random = new System.Random(seed); Index();
        }
        public RewardLearner(RewardPolicyFile file, int seed) {
            if (file == null || file.version != 1 || file.cards == null || file.weights == null) throw new Exception("Invalid reward policy");
            data = file; random = new System.Random(seed); Index();
            if (data.weights.Length != FeatureSize || data.baseline == null || data.baseline.Length != BaselineSize) throw new Exception("Reward policy feature size mismatch");
        }
        void Index() { cardIndex.Clear(); for (int i = 0; i < data.cards.Length; i++) cardIndex[data.cards[i]] = i; }
        int C { get { return data.cards.Length; } }
        public int FeatureSize { get { return 6 * C + Classes * Upgrades.Length + Classes * Elements.Length * Elements.Length; } }
        public bool Reinforce { get { return data.method == "reinforce"; } }
        public string Name { get { return Reinforce ? "학습 정책 · REINFORCE" : "학습 정책 · 선형 Q"; } }

        // Episode objective the policy maximizes; return-to-go is its change after a decision.
        public static float Objective(Simulation s) { return s.time / 30f + s.kills / 50f + (s.won ? 2 : 0); }

        static float Relative(Simulation s, Reward r) {
            switch (r.id) {
                case "damage": return r.value / (100 + s.damageBonus);
                case "rate": return r.value / (100 + s.rateBonus);
                case "radius": return r.value / (100 + s.radiusBonus);
                case "life": return r.value / (100 + s.lifeBonus);
                case "speed": return r.value / (100 + s.speedBonus);
                case "hp": return r.value / s.maxHp;
                default: return 0;
            }
        }
        int Features(Simulation s, Reward r, int[] idx, float[] val) {
            int c, n = 0, cls = s.classIndex;
            if (!cardIndex.TryGetValue(r.id, out c)) throw new Exception("Unknown reward card " + r.id);
            idx[n] = cls * C + c; val[n++] = 1;
            idx[n] = 4 * C + c; val[n++] = s.MissingHp;
            idx[n] = 5 * C + c; val[n++] = Mathf.Clamp01(s.time / s.b.duration);
            int upgrade = Array.IndexOf(Upgrades, r.id);
            if (!r.scroll && !r.infusion && upgrade >= 0) { idx[n] = 6 * C + cls * Upgrades.Length + upgrade; val[n++] = 3 * Relative(s, r); }
            if (r.infusion && s.infusions.Count == 1) {
                int first = Array.IndexOf(Elements, s.infusions[0]), second = Array.IndexOf(Elements, r.id);
                idx[n] = 6 * C + Classes * Upgrades.Length + (cls * Elements.Length + first) * Elements.Length + second; val[n++] = 1;
            }
            return n;
        }
        static float[] Psi(Simulation s) {
            var psi = new float[BaselineSize]; int o = s.classIndex * 4;
            psi[o] = 1; psi[o + 1] = Mathf.Clamp01(s.time / s.b.duration); psi[o + 2] = s.hp / s.maxHp; psi[o + 3] = s.level / 30f;
            return psi;
        }
        float Dot(int[] idx, float[] val, int n) { float sum = 0; for (int i = 0; i < n; i++) sum += data.weights[idx[i]] * val[i]; return sum; }
        float Value(float[] psi) { float sum = 0; for (int i = 0; i < psi.Length; i++) sum += data.baseline[i] * psi[i]; return sum; }

        public float[] Scores(Simulation s) {
            var scores = new float[s.offers.Count]; var idx = new int[MaxFeatures]; var val = new float[MaxFeatures];
            for (int i = 0; i < scores.Length; i++) scores[i] = Dot(idx, val, Features(s, s.offers[i], idx, val));
            return scores;
        }
        static float[] Softmax(float[] scores) {
            var p = new float[scores.Length]; float max = float.NegativeInfinity, total = 0;
            foreach (float x in scores) max = Mathf.Max(max, x);
            for (int i = 0; i < p.Length; i++) { p[i] = Mathf.Exp(scores[i] - max); total += p[i]; }
            for (int i = 0; i < p.Length; i++) p[i] /= total;
            return p;
        }
        static int ArgMax(float[] x) { int best = 0; for (int i = 1; i < x.Length; i++) if (x[i] > x[best]) best = i; return best; }

        public int Select(Simulation s) {
            if (s.offers.Count == 0) return -1;
            var scores = Scores(s); int chosen = ArgMax(scores);
            if (explore) {
                if (Reinforce) {
                    var p = Softmax(scores); double pick = random.NextDouble(); chosen = p.Length - 1;
                    for (int i = 0; i < p.Length; i++) { pick -= p[i]; if (pick < 0) { chosen = i; break; } }
                } else if (random.NextDouble() < epsilon) chosen = random.Next(scores.Length);
                Record(s, chosen);
            }
            return chosen;
        }
        void Record(Simulation s, int chosen) {
            int k = s.offers.Count;
            var d = new Decision { idx = new int[k][], val = new float[k][], count = new int[k], psi = Psi(s), chosen = chosen, objective = Objective(s) };
            for (int i = 0; i < k; i++) { d.idx[i] = new int[MaxFeatures]; d.val[i] = new float[MaxFeatures]; d.count[i] = Features(s, s.offers[i], d.idx[i], d.val[i]); }
            trace.Add(d);
        }
        public string Reason(Simulation s, int index) {
            var scores = Scores(s);
            if (Reinforce) return "학습(REINFORCE) 선택 확률 " + (Softmax(scores)[index] * 100).ToString("F0") + "%";
            return "학습(선형 Q) 예상 잔여 가치 " + scores[index].ToString("F2");
        }

        // Applies one episode of experience collected while explore=true.
        public void EndEpisode(Simulation s) {
            float final = Objective(s);
            if (Reinforce) {
                foreach (var d in trace) {
                    float g = final - d.objective, adv = g - Value(d.psi);
                    for (int i = 0; i < d.psi.Length; i++) data.baseline[i] += learningRate * adv * d.psi[i];
                    advantageScale = 0.99f * advantageScale + 0.01f * Mathf.Abs(adv);
                    float step = learningRate * Mathf.Clamp(adv / Mathf.Max(0.05f, advantageScale), -3, 3);
                    var scores = new float[d.count.Length];
                    for (int i = 0; i < scores.Length; i++) scores[i] = Dot(d.idx[i], d.val[i], d.count[i]);
                    var p = Softmax(scores);
                    for (int i = 0; i < scores.Length; i++) {
                        float coefficient = (i == d.chosen ? 1 : 0) - p[i];
                        for (int f = 0; f < d.count[i]; f++) data.weights[d.idx[i][f]] += step * coefficient * d.val[i][f];
                    }
                }
            } else {
                for (int t = trace.Count - 1; t >= 0; t--) {
                    var d = trace[t]; float target;
                    if (t == trace.Count - 1) target = final - d.objective;
                    else {
                        var next = trace[t + 1]; float best = float.NegativeInfinity;
                        for (int i = 0; i < next.count.Length; i++) best = Mathf.Max(best, Dot(next.idx[i], next.val[i], next.count[i]));
                        target = next.objective - d.objective + best;
                    }
                    int a = d.chosen; float error = target - Dot(d.idx[a], d.val[a], d.count[a]);
                    for (int f = 0; f < d.count[a]; f++) data.weights[d.idx[a][f]] += learningRate * error * d.val[a][f];
                }
            }
            trace.Clear(); data.episodes++;
        }
        public void DiscardEpisode() { trace.Clear(); }
    }
}
