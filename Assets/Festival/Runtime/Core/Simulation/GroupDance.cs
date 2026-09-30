using System;
using System.Collections.Generic;
namespace Festival.Core {
public sealed partial class FestivalSimulation {
    // DANCE-1: a finished Dance's witnesses judge every connected crew member within GroupDanceRadius
    // they can see by the group's worst score; a member with no overlapping Dance of their own counts as 0.
    // Members who are dancing settle when their own Dance finishes, so each dancer changes once per dance.
    const double GroupDanceRadius=6;
    static int RhythmDelta(double score)=>(int)Math.Round(20-35*score,MidpointRounding.AwayFromZero);
    void JudgeGroupDance(InteractionState i,PlayerState dancer){double worst=i.Score;var judged=new List<PlayerState>{dancer};foreach(var q in State.Players){if(q==dancer||!q.Connected||Distance(q.X,q.Z,dancer.X,dancer.Z)>GroupDanceRadius||!State.Npcs.Exists(n=>i.WitnessIds.Contains(n.Id)&&Sees(n,q)))continue;double? own=OverlappingDanceScore(q,i);if(own==null)judged.Add(q);worst=Math.Min(worst,own??0);}int delta=RhythmDelta(worst);foreach(var n in State.Npcs)if(i.WitnessIds.Contains(n.Id))foreach(var q in judged)Adjust(n,q,delta);}
    // Overlap means the [Start, Start+Duration] windows intersect. A Dance still running is judged on the notes due so far.
    double? OverlappingDanceScore(PlayerState q,InteractionState i){double? worst=null;foreach(var x in State.Interactions){if(x==i||x.Kind!="Dance"||x.PlayerId!=q.Id||(x.Status!="Active"&&x.Status!="Complete")||x.StartSeconds>i.StartSeconds+i.DurationSeconds||i.StartSeconds>x.StartSeconds+x.DurationSeconds)continue;double score=x.Status=="Complete"?x.Score:ScoreSoFar(x);worst=worst==null?score:Math.Min(worst.Value,score);}return worst;}
    double ScoreSoFar(InteractionState x){var judge=new RhythmJudge(RhythmChart.Create(x.ChartSeed,x.NoteCount,x.BeatSeconds),x.GoodWindowSeconds);foreach(var input in x.Inputs)judge.Submit(input.Direction,input.TimeSeconds);judge.Advance(State.SimulationSeconds-x.StartSeconds);return judge.ScoreSoFar;}
}
}
