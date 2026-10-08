using System;
using System.Collections.Generic;

namespace Festival.Core
{
    /// <summary>Renderer-free seeded parts and the three approved worst-drummer payout bands.</summary>
    public static class DrumCircle
    {
        public static RhythmChart Part(int seed,int participant,double beatSeconds)
        {
            if(participant<0||participant>=8)throw new ArgumentOutOfRangeException(nameof(participant));
            return RhythmChart.For(new InteractionState{ChartSeed=PartSeed(seed,participant),NoteCount=24,BeatSeconds=beatSeconds,ChartDifficulty=RhythmChart.QuarterNotes});
        }
        // Candidate seeds are resolved against every preceding part, so even a rare whole-pattern collision cannot duplicate a roster part.
        public static int PartSeed(int seed,int participant)
        {
            if(participant<0||participant>=8)throw new ArgumentOutOfRangeException(nameof(participant));
            var patterns=new List<string>();int candidate=0;
            for(int index=0;index<=participant;index++)
            {
                candidate=unchecked(seed*61+index*7919+29);string pattern;
                do{var chart=RhythmChart.Create(candidate,24,.5,RhythmChart.QuarterNotes);pattern=string.Join(",",chart.Notes.ConvertAll(n=>n.Direction));if(patterns.Contains(pattern))candidate=unchecked(candidate+1);else break;}while(true);
                patterns.Add(pattern);
            }
            return candidate;
        }
        public static int ClockChange(IReadOnlyList<double> scores)
        {
            if(scores==null||scores.Count==0)throw new ArgumentException("A drum circle requires participants.",nameof(scores));
            double worst=1;foreach(double score in scores)
            {
                if(double.IsNaN(score)||double.IsInfinity(score)||score<0||score>1)throw new ArgumentOutOfRangeException(nameof(scores));
                worst=Math.Min(worst,score);
            }
            return worst>=.75?45:worst<.4?-30:0;
        }
    }

    [Serializable] public sealed class DrumAccuracy
    {
        public string PlayerId=""; public double Accuracy;
    }

    public sealed partial class FestivalSimulation
    {
        public const string DrumCircleGame="DrumCircle",DrumPartKind="DrumPart";
        // The chart starts with the game at the held stage position. Existing simulation timestamps advance by exactly the game clock;
        // chart/music consumers use this public game-local adapter, never the held world transport.
        public static double DrumChartSeconds(RoundState s,InteractionState i)=>s.Booth.Seconds-(i.StartSeconds-(s.SimulationSeconds-s.Booth.Seconds));
        void StartDrumCircle()
        {
            var b=State.Booth;b.DrumAccuracies.Clear();b.DrumWorstScore=-1;
            for(int k=0;k<b.PlayerIds.Count;k++)
            {
                var p=Player(b.PlayerIds[k]);var i=NewInteraction(p,DrumPartKind,MinigameKind,0);i.ChartSeed=DrumCircle.PartSeed(unchecked(State.SpinSeed*67)^State.DealSeed,k);i.ChartDifficulty=RhythmChart.QuarterNotes;i.NoteCount=24;i.BeatSeconds=.5;
                RhythmChart.InitializeStage(i,RhythmChart.StageTrackFor(State.FestivalIndex,State.LevelIndex),WorldSeconds(State));
                b.DrumAccuracies.Add(new DrumAccuracy{PlayerId=p.Id});
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RhythmDiagnostic?.Invoke("[Festival.DrumCircle] start participants="+b.PlayerIds.Count);
#endif
        }
        // Replay accepted owner inputs and advance every frozen part, even when its participant is silent or has disconnected.
        // Live accuracy judges due notes after transport grace; the final whole-chart score includes every roster member.
        void DrumCircleTick()
        {
            var b=State.Booth;bool finished=true;
            foreach(string id in b.PlayerIds)
            {
                var p=Player(id);var i=State.Interactions.FindLast(x=>x.PlayerId==id&&x.Kind==DrumPartKind);
                if(i!=null&&i.Status=="Active"&&(p==null||!p.Connected||p.Life!="Alive")){i.Status="Interrupted";if(p!=null&&p.InteractionId==i.Id)p.InteractionId="";}
                double score=0;if(i!=null)
                {
                    var chart=RhythmChart.For(i);var judge=new RhythmJudge(chart,i.GoodWindowSeconds);
                    // Live replay intentionally has no detailed diagnostic callback: each tick would otherwise repeat private note traces.
                    foreach(var input in i.Inputs)if(input.Release)judge.Release(input.Direction,input.TimeSeconds);else judge.Submit(input.Direction,input.TimeSeconds);
                    double now=DrumChartSeconds(State,i);judge.Advance(now-.75);
                    score=now>=i.DurationSeconds+.75||b.Seconds>=MinigameMaxSeconds?judge.Score:judge.ScoreSoFar??0;i.Score=score;
                    if(now<i.DurationSeconds+.75)finished=false;
                }
                var summary=b.DrumAccuracies.Find(x=>x.PlayerId==id);if(summary==null){summary=new DrumAccuracy{PlayerId=id};b.DrumAccuracies.Add(summary);}summary.Accuracy=score;
            }
            if(!finished&&b.Seconds<MinigameMaxSeconds)return;
            var scores=b.PlayerIds.ConvertAll(id=>b.DrumAccuracies.Find(x=>x.PlayerId==id)?.Accuracy??0);
            b.DrumWorstScore=scores.Count==0?0:MinimumDrumScore(scores);
            int seconds=scores.Count==0?-30:DrumCircle.ClockChange(scores),delta=seconds>0?-20:seconds<0?15:0;
            var at=BoothAt(State);if(at!=null&&delta!=0)foreach(string id in b.PlayerIds)
            {
                var p=Player(id);if(p==null)continue;foreach(var n in State.Npcs)if(n.Kind=="Wook"&&Distance(n.X,n.Z,at.X,at.Z)<=25)
                {var observer=Observe(n,p);observer.Suspicion=Math.Max(0,Math.Min(100,observer.Suspicion+delta));observer.LastSeenSeconds=State.SimulationSeconds;}
            }
            ScoreMinigame(seconds);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RhythmDiagnostic?.Invoke("[Festival.DrumCircle] scored participants="+b.PlayerIds.Count+" clock_change="+b.ClockChange);
#endif
        }
        static double MinimumDrumScore(List<double> scores){double worst=1;foreach(double score in scores)worst=Math.Min(worst,score);return worst;}
        void CloseDrumParts()
        {
            foreach(var i in State.Interactions)if(i.Kind==DrumPartKind)
            {if(i.Status=="Active")i.Status="Complete";var p=Player(i.PlayerId);if(p!=null&&p.InteractionId==i.Id)p.InteractionId="";}
        }
    }
}
