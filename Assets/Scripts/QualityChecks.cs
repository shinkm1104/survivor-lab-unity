#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace SurvivorLab {
    public static class QualityChecks {
        static int count;
        static void Check(bool condition,string label) { if(!condition) throw new Exception("QA FAILED: "+label); count++; Console.WriteLine("QA PASS: "+label); }
        static bool Near(float a,float b) { return Mathf.Abs(a-b)<0.002f; }
        public static void Run(Balance b) {
            count=0;
            Check(b.scrolls.Length==19 && b.spells.Length==4,"catalog counts");
            Check(Near(b.RequiredXp(14),110.7f)&&Near(b.RequiredXp(15),136.3f),"XP boundary 14/15");
            Check(Near(b.RequiredXp(29),267.9f)&&Near(b.RequiredXp(30),338.66f),"XP boundary 29/30");
            Check(Near(b.RequiredXp(59),671.58f)&&Near(b.RequiredXp(60),683.06f)&&Near(b.RequiredXp(61),706.64f),"XP procedural boundary 59/60/61");
            var s=new Simulation(b,2,13);
            for(int i=0;i<10;i++) s.Step(Vector2.right,0.05f,true);
            Check(s.rocks.Count==4,"distance-spaced rocks");
            int rocks=s.rocks.Count; for(int i=0;i<10;i++) s.Step(Vector2.zero,0.05f,true);
            Check(s.rocks.Count==rocks,"no stationary rocks");
            Check(s.facing==Vector2.right,"last direction retained while stationary");
            s=new Simulation(b,0,14); s.GrantScroll("focus"); float normal=s.Interval;
            for(int i=0;i<31;i++) s.Step(Vector2.zero,0.05f,true);
            Check(Near(s.Interval,normal*0.7f),"focus uses interval multiplier");
            s.Step(Vector2.right,0.05f,true); Check(Near(s.Interval,normal),"movement cancels focus");
            s=new Simulation(b,0,16); s.xp=b.RequiredXp(1); s.Step(Vector2.zero,0.05f,false);
            Check(s.level==2 && Near(s.xp,0),"exact XP threshold levels up");
            s=new Simulation(b,0,17); s.GrantScroll("shield");
            Check(s.shield==0,"shield not granted immediately");
            for(int i=0;i<601;i++) { s.enemies.Clear(); s.Step(Vector2.zero,0.05f,true); }
            Check(Near(s.shield,10),"shield at 30 seconds");
            s.shield=4; for(int i=0;i<602;i++) { s.enemies.Clear(); s.Step(Vector2.zero,0.05f,true); }
            Check(Near(s.shield,4),"shield remaining is not replenished");
            s=new Simulation(b,0,18); s.GrantScroll("regeneration"); s.hp=50;
            for(int i=0;i<101;i++) { s.enemies.Clear(); s.Step(Vector2.zero,0.05f,true); }
            Check(Near(s.hp,51),"regeneration heals one percent");
            s=new Simulation(b,2,19); s.rocks.Add(new Rock{pos=Vector2.zero,expires=10}); s.rocks.Add(new Rock{pos=Vector2.zero,expires=10});
            var e=new Enemy{id=1,hp=1000,pos=new Vector2(0.5f,0)}; s.enemies.Add(e); s.Step(Vector2.zero,0.5f,true);
            Check(s.damageDealt<=s.Damage*b.baseCritMultiplier+0.01f,"overlapping rocks deduplicate damage");
            s=new Simulation(b,0,20); s.GrantScroll("frost_mark"); e=new Enemy{id=1,hp=1000}; s.Hit(e); float until=e.slowUntil; s.time=0.5f; s.Hit(e);
            Check(e.slowUntil>until && Near(e.slowUntil,2),"frost refreshes duration");
            var a=new Simulation(b,1,101); var c=new Simulation(b,1,101);
            for(int i=0;i<1200;i++) { Vector2 input=Learning.Actions[(i/10)%9]; a.Step(input,0.05f,true); c.Step(input,0.05f,true); }
            Check(a.kills==c.kills && a.hp==c.hp && a.player==c.player,"seeded simulation deterministic");
            var agent=new Learning(); agent.UpdateQ(1,2,10,2,true); Check(agent.Act(1,0)==2,"learning changes greedy action");
            for(int cls=0;cls<4;cls++) {
                s=new Simulation(b,cls,200+cls);
                s.drops.Add(new Drop{pos=Vector2.zero,xp=b.RequiredXp(1)+0.01f});
                s.Step(Vector2.zero,0.05f,false);
                Check(s.level==2 && s.collectedOrbs==1 && s.offers.Count==3,"real XP pickup yields three choices class "+cls);
                float clock=s.time; s.Step(Vector2.right,0.05f,false);
                Check(s.time==clock && s.player==Vector2.zero,"reward pauses simulation class "+cls);
                var reward=s.offers[1]; s.Choose(1);
                Check(s.offers.Count==0 && (reward.id!="damage" || s.damageBonus==reward.value),"manual reward selection applies class "+cls);
            }
            s=new Simulation(b,1,300); float required=0;
            for(int l=1;l<7;l++) required+=b.RequiredXp(l);
            s.GainXp(required+0.01f);
            int upgrades=0;
            while(s.offers.Count>0 && !s.offers[0].infusion) { Check(s.offers.Count==3,"queued level has three distinct cards"); upgrades++; s.Choose(0); }
            Check(s.level==7 && upgrades==6 && s.offers.Count==3 && s.infusions.Count==0,"first infusion offered after six level-up choices");
            string first=s.offers[0].id; s.Choose(0);
            Check(s.infusions.Count==1 && s.ActiveElement.id==first,"first infusion attaches to unique spell");
            required=0; for(int l=7;l<14;l++) required+=b.RequiredXp(l);
            s.GainXp(required+0.01f);
            while(s.offers.Count>0 && !s.offers[0].infusion) s.Choose(0);
            Check(s.level==14 && s.offers.Count==3 && s.offers[0].infusion,"second infusion offered at 14");
            string second=s.offers[0].id; s.Choose(0);
            Check(s.infusions.Count==2 && s.ActiveElement==b.CombinedElement(first,second),"second infusion combines or doubles");
            s.GainXp(b.RequiredXp(14)+0.01f);
            bool extraInfusion=false; while(s.offers.Count>0) { extraInfusion|=s.offers[0].infusion; s.Choose(0); }
            Check(!extraInfusion && s.infusions.Count==2,"no third infusion");
            Check(b.CombinedElement("Fire","Fire").burnTicks==5 && b.CombinedElement("Fire","Fire").burnFactor>b.Element("Fire").burnFactor,"double fire differs from single");
            Check(b.CombinedElement("Fire","Lightning")==b.CombinedElement("Lightning","Fire"),"element combination order independent");
            Check(b.elements.Length==20,"all five singles, five doubles, ten mixed elements configured");
            s=new Simulation(b,0,400); s.infusions.Add("Fire"); e=new Enemy{id=1,hp=1000,pos=new Vector2(5,0)}; s.enemies.Add(e); s.Hit(e);
            float afterDirect=e.hp;
            for(int i=0;i<11;i++) s.Step(Vector2.zero,0.05f,true);
            Check(e.hp<afterDirect && e.burnRemaining==2,"fire damage ticks after hit");
            s=new Simulation(b,0,401); s.infusions.Add("Lightning");
            e=new Enemy{id=1,hp=1000,pos=new Vector2(5,0)}; var e2=new Enemy{id=2,hp=1000,pos=new Vector2(6,0)}; var e3=new Enemy{id=3,hp=1000,pos=new Vector2(7,0)};
            s.enemies.Add(e); s.enemies.Add(e2); s.enemies.Add(e3); s.Hit(e);
            Check(e2.hp<1000 && e3.hp<1000 && s.elementVisuals.Count==2,"lightning chains to two unique enemies");
            s=new Simulation(b,0,402); e=new Enemy{id=1,hp=1000,acidUntil=3};
            Check(Near(s.DealDamage(e,10),12.5f),"acid vulnerability uses explicit game rule");
            s=new Simulation(b,0,403); s.infusions.Add("Ice"); e=new Enemy{id=1,hp=1000}; s.Hit(e);
            Check(Near(e.chillUntil,4),"ice chill lasts configured duration");
            s=new Simulation(b,0,404); s.infusions.Add("Dark"); e=new Enemy{id=1,hp=1,pos=new Vector2(5,0)}; e2=new Enemy{id=2,hp=1000,pos=new Vector2(5.5f,0)};
            s.enemies.Add(e); s.enemies.Add(e2); s.Hit(e); s.Step(Vector2.zero,0.05f,true);
            Check(e2.hp<1000 && s.kills==1,"dark marked death causes secondary explosion once");
            s=new Simulation(b,0,405); e=new Enemy{id=1,hp=1}; s.Hit(e); s.Hit(e);
            Check(s.kills==1 && s.drops.Count==1,"dead enemy cannot award duplicate XP");
            s=new Simulation(b,0,406); s.drops.Add(new Drop{pos=new Vector2(5,0),xp=1});
            for(int i=0;i<10;i++) s.Step(Vector2.zero,0.05f,true);
            Check(s.collectedOrbs==1,"nearby XP magnet reaches player");
            s=new Simulation(b,1,500);
            s.offers.Add(new Reward{id="speed",value=4}); s.offers.Add(new Reward{id="damage",value=35}); s.offers.Add(new Reward{id="hp",value=10});
            Check(RewardAdvisor.Select(s)==1,"AI reward selection is not fixed to first card");
            s.hp=10; Check(RewardAdvisor.Select(s)==2,"AI prioritizes health when critically low");
            s.ChooseAutomatically(); Check(s.automaticChoices==1 && s.maxHp==b.playerHp+10 && s.lastAutoChoice!="","automatic reward applies and records decision");
            s=new Simulation(b,1,501);
            s.offers.Add(new Reward{id="Fire",infusion=true}); s.offers.Add(new Reward{id="Lightning",infusion=true}); s.offers.Add(new Reward{id="Dark",infusion=true});
            Check(RewardAdvisor.Select(s)==1,"orb AI favors chain-lightning infusion among offered cards");
            s=new Simulation(b,0,502); s.hp=10;
            s.offers.Add(new Reward{id="humble_refinement",scroll=true}); s.offers.Add(new Reward{id="regeneration",scroll=true}); s.offers.Add(new Reward{id="vital_strike",scroll=true});
            s.Step(Vector2.zero,0.05f,true);
            Check(s.owned.Contains("regeneration") && s.offers.Count==0,"simulation auto-selects scroll without user input");
            s=new Simulation(b,0,503); required=0; for(int l=1;l<14;l++) required+=b.RequiredXp(l);
            s.GainXp(required+0.01f); int guard=0;
            while((s.offers.Count>0 || s.rewards.Count>0) && guard++<30) s.Step(Vector2.zero,0.05f,true);
            Check(s.level==14 && s.infusions.Count==2 && s.automaticChoices==15 && s.offers.Count==0 && s.rewards.Count==0,"AI drains every level and both infusion rewards without blocking");
            Check(RewardAdvisor.Select(s)==-1,"AI safely handles empty reward list");
            s=new Simulation(b,1,600){rewardChooser=new FixedRewardChooser(true,1)};
            s.offers.Add(new Reward{id="speed",value=4}); s.offers.Add(new Reward{id="damage",value=35}); s.offers.Add(new Reward{id="hp",value=10});
            s.ChooseAutomatically(); Check(s.speedBonus==4 && s.damageBonus==0,"simulation uses injected reward chooser");
            var learner=new RewardLearner(b,"reinforce",7);
            Check(learner.data.cards.Length==6+19+5 && learner.data.weights.Length==learner.FeatureSize,"reward learner catalog covers upgrades, scrolls and elements");
            s=new Simulation(b,1,601){rewardChooser=learner};
            s.offers.Add(new Reward{id="speed",value=4}); s.offers.Add(new Reward{id="damage",value=35}); s.offers.Add(new Reward{id="Lightning",infusion=true});
            Check(learner.Select(s)>=0 && learner.Select(s)<3,"untrained reward learner returns a valid card");
            learner.explore=true; int picked=learner.Select(s), other=picked==0?1:0; learner.explore=false;
            float before=learner.Scores(s)[picked]-learner.Scores(s)[other];
            s.time+=30; learner.EndEpisode(s); s.time-=30;
            float after=learner.Scores(s)[picked]-learner.Scores(s)[other];
            Check(after>before && learner.data.episodes==1,"REINFORCE raises preference for a card followed by positive return");
            var q=new RewardLearner(b,"linear-q",8){explore=true,epsilon=0};
            int qa=q.Select(s); float q0=q.Scores(s)[qa]; s.time+=30; q.EndEpisode(s); s.time-=30;
            Check(q.Scores(s)[qa]>q0 && q.Scores(s)[qa]<=1+0.0001f,"linear Q moves chosen card value toward observed return");
            q.explore=false; Check(q.Select(s)==qa && q.Select(s)==qa,"greedy learned choice is deterministic");
            bool rejected=false; try { new RewardLearner(new RewardPolicyFile{cards=learner.data.cards,weights=new float[3],baseline=new float[RewardLearner.BaselineSize]},1); } catch(Exception) { rejected=true; }
            Check(rejected,"reward policy with wrong feature size is rejected");
            if(b.HasWaves) {
                s=new Simulation(b,2,700);
                for(int i=0;i<100;i++) s.Step(Vector2.zero,0.05f,true);
                Check(s.enemies.Count>=b.waves[0].minEnemies,"spawner refills to wave minimum");
                Check(s.WaveNumber==1 && Near(b.WaveAt(b.waves[1].start).start,b.waves[1].start),"wave lookup by time");
                s=new Simulation(b,2,701); e=new Enemy{id=999,hp=10,pos=new Vector2(b.despawnDistance+1,0)}; s.enemies.Add(e);
                s.Step(Vector2.zero,0.05f,true);
                Check(!s.enemies.Contains(e) && s.kills==0 && s.drops.Count==0,"far straggler recycled without XP");
                var line=b.lineEvents[0]; s=new Simulation(b,2,702); s.time=line.time-0.01f; s.Step(Vector2.zero,0.05f,true);
                var sweepers=s.enemies.FindAll(x=>x.Sweeping);
                int slots=Mathf.FloorToInt(2*b.lineHalfLength/b.lineSpacing)+1;
                Check(sweepers.Count>0 && sweepers.Count<slots,"line event spawns a wall with a gap");
                // Reward picks pause the clock, so advance by time rather than a fixed step count.
                float leaveAt=sweepers[0].sweepUntil; int guardSteps=0;
                while(s.time<=leaveAt+0.05f && !s.Done && guardSteps++<2000) { s.player=Vector2.one*30; s.enemies.RemoveAll(x=>!x.Sweeping); s.Step(Vector2.zero,0.05f,true); }
                Check(!s.enemies.Exists(x=>x.Sweeping),"line enemies leave after their sweep");
                Check(s.kills==0,"line enemies that leave give no kills");
            }
            Console.WriteLine("QA COMPLETE: "+count+" checks passed");
        }
    }
}
#endif
