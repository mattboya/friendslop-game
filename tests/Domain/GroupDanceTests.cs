using System;
using Festival.Core;
// DANCE-1: when a Dance finishes, its witnesses judge every crew member within 6 m by the group's worst dancer.
public static class GroupDanceTests
{
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("GroupDance: "+message);}
    // a and b stand 2 m apart; c is 10 m from a but still inside the one watcher's view.
    // Each player has a partner NPC to dance with; partners face away, so the watcher is the only witness.
    static FestivalSimulation Crew()
    {
        var s=new FestivalSimulation(4);
        foreach(var id in new[]{"a","b","c"})s.AddPlayer(id,id.ToUpperInvariant());
        s.State.Phase="Playing";s.State.Npcs.Clear();
        Place(s,"a",0,0);Place(s,"b",2,0);Place(s,"c",10,0);
        var watcher=new NpcState{Id="watcher",X=1,Z=6,Yaw=180};
        foreach(var p in s.State.Players)watcher.Observers.Add(new ObserverState{PlayerId=p.Id,Suspicion=20});
        s.State.Npcs.Add(watcher);
        foreach(var p in s.State.Players)s.State.Npcs.Add(new NpcState{Id="partner_"+p.Id,X=p.X,Z=p.Z-1,Yaw=180,CanTalk=true});
        return s;
    }
    static void Place(FestivalSimulation s,string id,float x,float z){var p=s.Player(id);p.X=x;p.Z=z;}
    static double Heat(FestivalSimulation s,string id)=>s.State.Npcs.Find(n=>n.Id=="watcher").Observers.Find(o=>o.PlayerId==id).Suspicion;
    static InteractionState Start(FestivalSimulation s,string id,string kind="Dance"){Check(s.Execute(id,new GameCommand{Id="group"+(sequence++),Kind=kind,TargetId="partner_"+id}).Accepted,id+" starts "+kind);return s.Interaction(s.Player(id).InteractionId);}
    // Enter the correct input for notes [from,to) of the chart.
    static void Hit(InteractionState i,int from,int to){var notes=RhythmChart.Create(i.ChartSeed,i.NoteCount,i.BeatSeconds).Notes;for(int n=from;n<to;n++)i.Inputs.Add(new RhythmInput{Direction=notes[n].Direction,TimeSeconds=notes[n].TimeSeconds});}
    // Tick until done, entering each note's correct input through the Rhythm command once the clock reaches it, as a player would.
    static void Live(FestivalSimulation s,Func<bool> done,params InteractionState[] dances)
    {
        for(int guard=0;guard<400&&!done();guard++)
        {
            foreach(var d in dances)
            {
                var notes=RhythmChart.Create(d.ChartSeed,d.NoteCount,d.BeatSeconds).Notes;
                while(d.Status=="Active"&&d.Inputs.Count<notes.Count&&notes[d.Inputs.Count].TimeSeconds<=s.State.SimulationSeconds-d.StartSeconds)
                {var n=notes[d.Inputs.Count];Check(s.Execute(d.PlayerId,new GameCommand{Id="group"+(sequence++),Kind="Rhythm",Direction=n.Direction,TimeSeconds=n.TimeSeconds}).Accepted,d.PlayerId+" hits note "+n.Id+" live");}
            }
            s.Tick(.1);
        }
        Check(done(),"the live dance reaches its checkpoint");
    }
    static void Finish(FestivalSimulation s,InteractionState i){for(int guard=0;i.Status=="Active"&&guard<200;guard++)s.Tick(.1);Check(i.Status=="Complete",i.Kind+" completes");}

    public static void Run()
    {
        // Reference: a perfect dancer with no crew member nearby.
        var s=Crew();Place(s,"b",-20,0);var dance=Start(s,"a");Hit(dance,0,8);Finish(s,dance);
        double solo=Heat(s,"a");Check(dance.Score==1&&solo<20,"a perfect solo dance lowers suspicion");

        s=Crew();dance=Start(s,"a");Hit(dance,0,8);Finish(s,dance);
        Check(Heat(s,"a")>20&&Heat(s,"b")==Heat(s,"a"),"a perfect dancer next to a non-dancing friend: both gain suspicion");
        Check(Heat(s,"c")==20,"a friend 10 m away is unaffected");

        s=Crew();Place(s,"b",5.9f,0);Place(s,"c",6.1f,0);dance=Start(s,"a");Hit(dance,0,8);Finish(s,dance);
        Check(Heat(s,"b")>20&&Heat(s,"c")==20,"the group reaches 6 m from the dancer and no further");

        s=Crew();var first=Start(s,"a");var second=Start(s,"b");Hit(first,0,8);Hit(second,0,8);Finish(s,first);Finish(s,second);
        Check(Heat(s,"a")==solo&&Heat(s,"b")==solo,"two good dancers both lose, each exactly once");
        Check(Heat(s,"c")==20,"a friend 10 m away is unaffected by the pair");

        s=Crew();first=Start(s,"a");second=Start(s,"b");Hit(first,0,8);Hit(second,0,4);Finish(s,first);Finish(s,second);
        Check(Heat(s,"a")>20&&Heat(s,"b")==Heat(s,"a"),"the worst dancer sets the whole group's suspicion");

        // b starts 2 s after a; when a finishes, b has danced every note due so far perfectly.
        s=Crew();first=Start(s,"a");Hit(first,0,8);s.Tick(2);second=Start(s,"b");Hit(second,0,6);Finish(s,first);
        Check(second.Status=="Active"&&Heat(s,"a")==solo,"a friend still mid-dance is judged on the notes due so far");
        Hit(second,6,8);Finish(s,second);Check(Heat(s,"b")==solo&&Heat(s,"a")==solo,"the later dancer settles once, with the earlier dancer's final score");

        // b presses Dance 5-6 s after a: the windows overlap, but b is still in the lead-in before their first note when a finishes.
        // Both dance perfectly, live. b is dancing but not scored yet, so a's finish neither counts b as 0 nor charges b.
        foreach(double gap in new[]{5,5.5,6})
        {
            s=Crew();first=Start(s,"a");double pressB=s.State.SimulationSeconds+gap;Live(s,()=>s.State.SimulationSeconds>=pressB-1e-9,first);
            second=Start(s,"b");Live(s,()=>first.Status!="Active",first,second);
            Check(second.Status=="Active"&&second.Inputs.Count==0&&Heat(s,"a")==solo&&Heat(s,"b")==20,"a friend "+gap+" s behind, still in the lead-in, is dancing but not scored yet (a="+Heat(s,"a")+", b="+Heat(s,"b")+", solo="+solo+")");
            Live(s,()=>second.Status!="Active",second);
            Check(second.Status=="Complete"&&Heat(s,"a")==solo&&Heat(s,"b")==solo,"two perfect dancers "+gap+" s apart both lose, each exactly once (a="+Heat(s,"a")+", b="+Heat(s,"b")+", solo="+solo+")");
        }

        // b danced first, alone, and finished before a started.
        s=Crew();Place(s,"a",-20,0);dance=Start(s,"b");Hit(dance,0,8);Finish(s,dance);Place(s,"a",0,0);
        double before=Heat(s,"a"),idle=Heat(s,"b");dance=Start(s,"a");Hit(dance,0,8);Finish(s,dance);
        Check(Heat(s,"a")>before&&Heat(s,"b")>idle,"a dance that ended before this one started does not count");

        s=Crew();second=Start(s,"b");Check(s.Execute("b",new GameCommand{Id="group"+(sequence++),Kind="Cancel"}).Accepted,"b quits");
        idle=Heat(s,"b");dance=Start(s,"a");Hit(dance,0,8);Finish(s,dance);
        Check(Heat(s,"a")>20&&Heat(s,"b")>idle,"a friend who quit their dance counts as not dancing");

        s=Crew();s.HasLineOfSight=(x,z,tx,tz)=>!(tx==2&&tz==0);dance=Start(s,"a");Hit(dance,0,8);Finish(s,dance);
        Check(Heat(s,"a")==solo&&Heat(s,"b")<=20,"a nearby friend no witness can see is not in the group");

        s=Crew();s.Disconnect("b");dance=Start(s,"a");Hit(dance,0,8);Finish(s,dance);
        Check(Heat(s,"a")==solo,"a disconnected friend is not in the group");

        s=Crew();var chat=Start(s,"a","Conversation");Hit(chat,0,chat.NoteCount);Finish(s,chat);
        Check(Heat(s,"a")==solo&&Heat(s,"b")==20,"only a Dance judges the group; a good chat still helps just the talker");
    }
}
