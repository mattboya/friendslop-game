namespace Festival.Core {
public sealed partial class FestivalSimulation {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Opt-in judge replay trace; never serialized or compiled into release rules.
    public System.Action<string> RhythmDiagnostic;
#endif
    // Presses and releases share the finite command envelope, chart window and input budget. Keep accepted arrival order.
    // A press may land up to a Good window after the chart ends: runs can put the last note a sixteenth before the end.
    CommandResult Submit(PlayerState p,GameCommand c){var i=Interaction(p.InteractionId);if(i==null||i.Status!="Active"||!IsRhythm(i))return Reject("No active rhythm challenge");double now=State.SimulationSeconds-i.StartSeconds;if(c.Amount<0||c.Amount>1||c.Direction<0||c.Direction>3||c.TimeSeconds<0||c.TimeSeconds>i.DurationSeconds+i.GoodWindowSeconds||c.TimeSeconds>now+.15||now-c.TimeSeconds>.75||i.Inputs.Count>=128)return Reject("Input outside supported timing envelope (connection issue)");bool release=c.Amount==1;i.Inputs.Add(new RhythmInput{Direction=c.Direction,TimeSeconds=c.TimeSeconds,Release=release});if(!release&&Dancing(i)){p.VisualDanceStepDirection=c.Direction;p.VisualDanceStepSequence++;}return Ok();}
    // Every score consumer replays this same ordered input log; releases never become presses or denominator entries.
    RhythmJudge ReplayRhythm(InteractionState i,RhythmChart chart){var judge=new RhythmJudge(chart,i.GoodWindowSeconds);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        judge.Diagnostic=RhythmDiagnostic;
#endif
        foreach(var input in i.Inputs)if(input.Release)judge.Release(input.Direction,input.TimeSeconds);else judge.Submit(input.Direction,input.TimeSeconds);return judge;}
}
}
