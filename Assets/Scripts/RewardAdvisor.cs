using UnityEngine;

namespace SurvivorLab {
    // Explicit rule-based reward decisions, separate from movement Q-learning.
    public static class RewardAdvisor {
        public static int Select(Simulation s) {
            int best=-1; float bestScore=float.NegativeInfinity;
            for(int i=0;i<s.offers.Count;i++) {
                float score=Score(s,s.offers[i]);
                if(score>bestScore || (score==bestScore && best>=0 && string.CompareOrdinal(s.offers[i].id,s.offers[best].id)<0)) { best=i; bestScore=score; }
            }
            return best;
        }
        static float Score(Simulation s,Reward r) {
            float health=s.hp/s.maxHp;
            if(r.infusion) {
                float score=1;
                switch(r.id) {
                    case "Fire": score=s.classIndex==2?3.2f:2.5f; break;
                    case "Lightning": score=s.classIndex==1?4:2.1f; break;
                    case "Ice": score=s.classIndex==3?3.5f:2.2f; if(health<0.4f) score+=1.5f; break;
                    case "Acid": score=s.classIndex==2?2.8f:1.8f; break;
                    case "Dark": score=s.classIndex==0?3:2; break;
                }
                if(s.infusions.Count>0 && s.infusions[0]==r.id) score+=0.4f;
                return score;
            }
            if(r.scroll) {
                switch(r.id) {
                    case "regeneration": case "healthy_growth": case "shield": return health<0.5f?6:3;
                    case "attraction": return s.level<14?4:2;
                    case "enlightenment": return s.level<14?3.8f:2;
                    case "bulwark": return s.owned.Contains("offensive_defense")?4:2.8f;
                    case "offensive_defense": return s.Armor>0?4:1;
                    case "focus": return s.classIndex==2?0.5f:2.5f;
                    case "frost_mark": return s.classIndex==2 || s.classIndex==3?3.5f:2.7f;
                    case "elixir_permanence": return 2.8f;
                    case "hard_lessons": return health<0.6f?3.5f:2;
                    case "blood_speed": case "fight_or_flight": case "second_breath": return health<0.5f?3:2.2f;
                    case "evasion_mend": return 1.3f;
                    case "vital_strike": case "humble_refinement": return 1.1f;
                    default: return 2;
                }
            }
            switch(r.id) {
                case "hp": return health<0.4f?6+r.value/s.maxHp:0.3f+r.value/s.maxHp;
                case "damage": return 1+r.value/(100+s.damageBonus)*10;
                case "rate": return 1.05f+r.value/(100+s.rateBonus)*10;
                case "radius": return s.classIndex==0?1+r.value/(100+s.radiusBonus)*8:0;
                case "life": return s.classIndex==2?0.9f+r.value/(100+s.lifeBonus)*8:0;
                case "speed": return (s.classIndex==2?1.2f:0.6f)+r.value/(100+s.speedBonus)*8;
                default: return 0;
            }
        }
        public static string Reason(Simulation s,Reward r) {
            if(r.infusion) return "클래스와 기존 속성을 고려한 조합";
            if(s.hp/s.maxHp<0.4f && (r.id=="hp" || r.id=="regeneration" || r.id=="shield" || r.id=="healthy_growth")) return "낮은 체력에서 생존 보강";
            if(r.id=="attraction" || r.id=="enlightenment") return "경험치 수집·성장 보강";
            return "클래스·현재 강화량·보유 효과를 고려";
        }
    }
}
