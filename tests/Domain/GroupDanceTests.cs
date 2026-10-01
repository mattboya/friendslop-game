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
    // A sale (command StartSale) carries one stock_lsd; the partner, dealt no role, buys like every festivalgoer. A cop walks its
    // patrol until someone talks to it, so the player first walks back up to their cop, 1 m south as Crew placed it.
    static InteractionState Start(FestivalSimulation s,string id,string kind="Dance"){var partner=s.State.Npcs.Find(n=>n.Id=="partner_"+id);if(partner.Kind=="Cop"){partner.X=s.Player(id).X;partner.Z=s.Player(id).Z-1;}string item="";if(kind=="StartSale"){item="stock_lsd";s.Player(id).Inventory.Add(new ItemStack{ItemId=item,Count=1});}var started=s.Execute(id,new GameCommand{Id="group"+(sequence++),Kind=kind,TargetId="partner_"+id,ItemId=item});Check(started.Accepted,id+" starts "+kind+": "+started.Reason);return s.Interaction(s.Player(id).InteractionId);}
    // b's partner is a cop for a security talk (Police), a festivalgoer otherwise.
    static FestivalSimulation Crew(string bKind){var s=Crew();if(bKind=="Police")s.State.Npcs.Find(n=>n.Id=="partner_b").Kind="Cop";return s;}
    // Enter the correct input for every note in beats [from,to) of the chart (DANCE-5: a beat can hold up to four notes).
    static void Hit(InteractionState i,int from,int to){foreach(var n in RhythmChart.For(i).Notes)if(n.TimeSeconds>=2+from*i.BeatSeconds-1e-9&&n.TimeSeconds<2+to*i.BeatSeconds-1e-9)i.Inputs.Add(new RhythmInput{Direction=n.Direction,TimeSeconds=n.TimeSeconds});}
    // Tick until done, entering each note's correct input through the Rhythm command once the clock reaches it, as a player would.
    static void Live(FestivalSimulation s,Func<bool> done,params InteractionState[] dances)
    {
        for(int guard=0;guard<400&&!done();guard++)
        {
            foreach(var d in dances)
            {
                var notes=RhythmChart.For(d).Notes;
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

        // TRIP-3: the tripper's check dance is dancing too. A perfect one beside a friend's perfect Dance counts as dancing.
        s=Crew();s.State.TripperId="a";s.State.Visions.Add(new VisionState{Id="v",Kind="Buyer",NpcId="partner_a",IsTrue=true});
        second=Start(s,"b");var check=Start(s,"a","ConfirmDance");Live(s,()=>check.Status!="Active"&&second.Status!="Active",check,second);
        Check(check.Score==1&&second.Score==1&&Heat(s,"a")==solo&&Heat(s,"b")==solo,"a perfect check dance beside a friend's perfect Dance: both lose, each exactly once (a="+Heat(s,"a")+", b="+Heat(s,"b")+", solo="+solo+")");
        // The check dance is the tripper's own: its score moves only the tripper, either way round. A missed check (+20) leaves a
        // friend's perfect Dance at solo, and a friend's missed Dance leaves a perfect check at solo.
        foreach(bool checkHits in new[]{false,true})
        {
            s=Crew();s.State.TripperId="a";s.State.Visions.Add(new VisionState{Id="v",Kind="Buyer",NpcId="partner_a",IsTrue=true});
            second=Start(s,"b");check=Start(s,"a","ConfirmDance");var hit=checkHits?check:second;
            Live(s,()=>check.Status!="Active"&&second.Status!="Active",hit);
            double hitHeat=Heat(s,hit.PlayerId),missHeat=Heat(s,checkHits?"b":"a");
            Check(hit.Score==1&&(checkHits?second:check).Score==0&&hitHeat==solo&&missHeat==40,(checkHits?"a perfect check dance beside a friend's missed Dance":"a missed check dance beside a friend's perfect Dance")+": the perfect one loses as if alone, the missed one gains 20 (a="+Heat(s,"a")+", b="+Heat(s,"b")+", solo="+solo+")");
        }

        // DANCE-8: a friend's sale, chat or security talk is danced (DANCE-2: everyone sees them dancing), so it counts as dancing
        // here, scored by their own chart: the notes due so far while it runs, its final score once done, nothing yet in its 2 s
        // lead-in. Its own witnesses settle the friend, so the Dance never charges them on top.
        foreach(var kind in new[]{"StartSale","Conversation","Police"})
        {
            // Reference: the same half-hit challenge with no crew member nearby.
            s=Crew(kind);Place(s,"a",-20,0);var talk=Start(s,"b",kind);Hit(talk,0,4);Finish(s,talk);double sloppy=Heat(s,"b");
            Check(talk.Score<1&&sloppy>20,kind+": a half-hit challenge alone raises suspicion");

            // Still running when the Dance finishes: judged on the notes due so far.
            s=Crew(kind);dance=Start(s,"a");Hit(dance,0,8);s.Tick(2);talk=Start(s,"b",kind);Hit(talk,0,6);Finish(s,dance);
            Check(talk.Status=="Active"&&Heat(s,"a")==solo&&Heat(s,"b")==20,kind+": a friend's perfect challenge still running counts as perfect dancing and does not charge them (a="+Heat(s,"a")+", b="+Heat(s,"b")+", solo="+solo+")");
            Hit(talk,6,8);Finish(s,talk);Check(Heat(s,"b")==solo&&Heat(s,"a")==solo,kind+": the friend settles once, through their own challenge (a="+Heat(s,"a")+", b="+Heat(s,"b")+")");

            // A sloppy one still sets the worst, and the friend is charged only by their own challenge.
            s=Crew(kind);dance=Start(s,"a");talk=Start(s,"b",kind);Hit(dance,0,8);Hit(talk,0,4);Finish(s,dance);Finish(s,talk);
            Check(Heat(s,"a")>solo&&Heat(s,"b")==sloppy,kind+": a friend's half-hit challenge worsens the dancer, and the friend gains only their own result (a="+Heat(s,"a")+", b="+Heat(s,"b")+", alone="+sloppy+")");

            // Finished before the Dance does, but overlapping it: judged on its final score.
            s=Crew(kind);talk=Start(s,"b",kind);Hit(talk,0,8);s.Tick(3);dance=Start(s,"a");Hit(dance,0,8);Finish(s,talk);Finish(s,dance);
            Check(talk.Score==1&&Heat(s,"a")==solo&&Heat(s,"b")==solo,kind+": a friend's perfect challenge that finished first counts by its score (a="+Heat(s,"a")+", b="+Heat(s,"b")+", solo="+solo+")");

            // Pressed 5.5 s after a's Dance, live: still in its lead-in when the Dance finishes, so dancing but not scored yet.
            s=Crew(kind);dance=Start(s,"a");double press=s.State.SimulationSeconds+5.5;Live(s,()=>s.State.SimulationSeconds>=press-1e-9,dance);
            talk=Start(s,"b",kind);Live(s,()=>dance.Status!="Active",dance,talk);
            Check(talk.Status=="Active"&&talk.Inputs.Count==0&&Heat(s,"a")==solo&&Heat(s,"b")==20,kind+": a friend's challenge still in its lead-in is dancing but not scored yet (a="+Heat(s,"a")+", b="+Heat(s,"b")+", solo="+solo+")");
            Live(s,()=>talk.Status!="Active",talk);Check(talk.Score==1&&Heat(s,"b")==solo&&Heat(s,"a")==solo,kind+": the friend then settles once, through their own challenge");
        }
    }
}
