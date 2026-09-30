using System;
using System.Collections.Generic;
namespace Festival.Core {
public sealed partial class FestivalSimulation {
    // DANCE-1: a finished Dance's witnesses judge every connected crew member within GroupDanceRadius
    // they can see by the group's worst score; a member with no overlapping Dance of their own counts as 0.
    // Members who are dancing settle when their own Dance finishes, so each dancer changes once per dance;
    // one still in the lead-in before their first note is dancing but not scored yet, so they do not set the worst.
    const double GroupDanceRadius=6;
    static int RhythmDelta(double score)=>(int)Math.Round(20-35*score,MidpointRounding.AwayFromZero);
    void JudgeGroupDance(InteractionState i,PlayerState dancer){double worst=i.Score;var judged=new List<PlayerState>{dancer};foreach(var q in State.Players){if(q==dancer||!q.Connected||Distance(q.X,q.Z,dancer.X,dancer.Z)>GroupDanceRadius||!State.Npcs.Exists(n=>i.WitnessIds.Contains(n.Id)&&Sees(n,q)))continue;if(!OverlappingDance(q,i,out double? own)){judged.Add(q);worst=0;}else if(own!=null)worst=Math.Min(worst,own.Value);}int delta=RhythmDelta(worst);foreach(var n in State.Npcs)if(i.WitnessIds.Contains(n.Id))foreach(var q in judged)Adjust(n,q,delta);}
    // The tripper's check dance (TRIP-3 ConfirmDance) counts as a Dance here. Overlap means the [Start, Start+Duration] windows
    // intersect. A Dance still running is judged on the notes due so far; worst stays null while every overlapping Dance is still before its first note.
    bool OverlappingDance(PlayerState q,InteractionState i,out double? worst){worst=null;bool dancing=false;foreach(var x in State.Interactions){if(x==i||(x.Kind!="Dance"&&x.Kind!="ConfirmDance")||x.PlayerId!=q.Id||(x.Status!="Active"&&x.Status!="Complete")||x.StartSeconds>i.StartSeconds+i.DurationSeconds||i.StartSeconds>x.StartSeconds+x.DurationSeconds)continue;dancing=true;double? score=x.Status=="Complete"?x.Score:ScoreSoFar(x);if(score!=null)worst=worst==null?score:Math.Min(worst.Value,score.Value);}return dancing;}
    double? ScoreSoFar(InteractionState x){var judge=new RhythmJudge(RhythmChart.Create(x.ChartSeed,x.NoteCount,x.BeatSeconds),x.GoodWindowSeconds);foreach(var input in x.Inputs)judge.Submit(input.Direction,input.TimeSeconds);judge.Advance(State.SimulationSeconds-x.StartSeconds);return judge.ScoreSoFar;}
    // CROWD-1: big packs draw attention. Every positive suspicion gain on a player (passive gains in WookTick, rhythm results
    // through Adjust) is multiplied by 1 + PackStep for each connected crew member standing within PackRadius of them beyond
    // the first, so a pair counts x1 and four bunched x1.7, up to MaxPackFactor. WookTick stacks it with the level's
    // suspicion multiplier up to MaxCrowdHeat.
    const double PackRadius=5,PackStep=.35,MaxPackFactor=2.5,MaxCrowdHeat=3;
    double PackFactor(PlayerState p){int near=0;foreach(var q in State.Players)if(q!=p&&q.Connected&&q.Life=="Alive"&&Distance(q.X,q.Z,p.X,p.Z)<=PackRadius)near++;return Math.Min(MaxPackFactor,1+PackStep*Math.Max(0,near-1));}
}
}
