using System;
using System.Collections.Generic;
using Festival.Core;

// VISION-3: the tripper's visions at their edges, each found by journey J2 of the 2026-09-30 wave: a fake never sits on a link
// still ahead on its own trail, and a clue the tripper finds says TRUE for a few seconds before the trail moves on.
public static class VisionEdgeTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("VisionEdge: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="")=>s.Execute(id,new GameCommand{Id="edge"+(sequence++),Kind=kind,TargetId=target});
    static int sequence;
    static NpcState Npc(FestivalSimulation s,string id)=>s.State.Npcs.Find(n=>n.Id==id);
    static List<string> Chain(FestivalSimulation s,int trail)=>trail==0?s.State.ClueChain:s.State.SecondFriend.ClueChain;
    static int Read(FestivalSimulation s,int trail)=>trail==0?s.State.CluesRead:s.State.SecondFriend.CluesRead;
    static string Next(FestivalSimulation s,int trail)=>Read(s,trail)<Chain(s,trail).Count?Chain(s,trail)[Read(s,trail)]:"";
    // Right in front of the festivalgoer, within reach of a check.
    static void Beside(PlayerState p,NpcState n){double yaw=n.Yaw*Math.PI/180;p.X=n.X+(float)Math.Sin(yaw);p.Z=n.Z+(float)Math.Cos(yaw);}

    // `crew` friends ready at camp for this level, the wheels land and everyone loads into the real crowd.
    static FestivalSimulation Start(int seed,int level,int crew=2,int festival=0)
    {
        var s=new FestivalSimulation(seed);for(int i=0;i<crew;i++)s.AddPlayer("p"+i,"P"+i);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: the crew reaches the festival");return s;
    }

    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{FakesKeepOffTheirOwnTrail,AFoundClueSaysTrueAWhile})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" vision edge test(s) failed:\n"+string.Join("\n",failures));
    }

    // 1. A fake clue on one of its own trail's later holders said FALSE while that person's chat pointed on down the trail, and
    // they turned out to be the real next link. On every night, before and after each link is found, no fake sits on a link
    // still ahead on its own trail (the other trail's were already kept clear).
    static void FakesKeepOffTheirOwnTrail()
    {
        int nights=0;var wrong=new List<string>();
        foreach(var (festival,level) in new[]{(0,1),(1,3)})foreach(var crew in new[]{2,5})for(int seed=0;seed<150;seed++)
        {
            var s=Start(seed,level,crew,festival);if(FestivalSimulation.TripperDose(s.State)<2)continue;nights++;
            for(int step=0;;step++)
            {
                foreach(var v in s.State.Visions)
                {
                    if(v.Kind!="Clue"||v.IsTrue)continue;int link=Chain(s,v.Trail).IndexOf(v.NpcId);
                    if(link>=Read(s,v.Trail))wrong.Add(Festivals.Name(festival)+" level "+level+", "+crew+" players, seed "+seed+", step "+step+": a fake on "+v.NpcId+", link "+link+" of trail "+v.Trail+" (read "+Read(s,v.Trail)+")");
                }
                int trail=step%2;if(Next(s,trail)=="")trail=1-trail;if(Next(s,trail)=="")break;
                s.ConfirmVisionsOf(Npc(s,Next(s,trail)));
            }
        }
        Check(nights>200,"setup: plenty of dose 2+ nights, got "+nights);
        Check(wrong.Count==0,wrong.Count+" fake(s) sat on a link still ahead on their own trail, first: "+string.Join(" | ",wrong.GetRange(0,Math.Min(3,wrong.Count))));
    }

    // 2. A correct night check never showed TRUE: finding the real holder moved the trail on in the same step and took the
    // checked vision with it. Now it stays, confirmed true in the tripper's view, for 3 s, beside the next link's visions.
    static void AFoundClueSaysTrueAWhile()
    {
        var s=Start(3,1);var tripper=s.Player(s.State.TripperId);var chain=new List<string>(s.State.ClueChain);
        Check(chain.Count==2,"setup: Palm Mirage Night 1 has a two-link trail");
        for(int link=0;link<chain.Count;link++)
        {
            var holder=Npc(s,chain[link]);var truth=s.State.Visions.Find(v=>v.Kind=="Clue"&&v.IsTrue&&v.NpcId==holder.Id);string where="link "+link+": ";
            Check(truth!=null,where+"setup: the tripper sees the real clue holder");
            Beside(tripper,holder);var chat=Act(s,tripper.Id,"ConfirmChat",holder.Id);Check(chat.Accepted,where+"setup: the tripper checks them by chat: "+chat.Reason);
            s.Tick(FestivalSimulation.ConfirmChatSeconds+.1);Check(s.State.CluesRead==link+1,where+"setup: the chat finds the link");
            VisionState Seen()=>FestivalSimulation.VisibleVisions(s.State,tripper.Id).Find(v=>v.Id==truth.Id);
            Check(Seen()!=null&&Seen().Confirmed&&Seen().IsTrue,where+"the tripper sees the clue they found say TRUE");
            if(link+1<chain.Count)Check(s.State.Visions.Exists(v=>v.Kind=="Clue"&&v.IsTrue&&!v.Confirmed&&v.NpcId==chain[link+1]),where+"beside the next link's visions");
            else Check(s.State.GateOpened&&!s.State.Visions.Exists(v=>v.Kind=="Clue"&&v!=truth),where+"the last link opens the way, and only the found clue is left");
            Check(!s.State.Visions.Exists(v=>v!=truth&&v.NpcId==holder.Id&&v.Kind=="Clue"),where+"no new fake lands on the festivalgoer showing TRUE");
            s.Tick(2.8);Check(Seen()!=null,where+"it stays a while");
            s.Tick(.3);Check(Seen()==null&&!s.State.Visions.Contains(truth),where+"then it goes, 3 s after it was found");
        }
        // While a found clue says TRUE, none of the next link's fakes lands on the same festivalgoer.
        int stacked=0,nights=0;
        for(int seed=0;seed<120;seed++)
        {
            var t=Start(seed,1,festival:1);if(FestivalSimulation.TripperDose(t.State)<3)continue;nights++;
            for(string next=Next(t,0);next!="";next=Next(t,0)){t.ConfirmVisionsOf(Npc(t,next));if(t.State.Visions.FindAll(v=>v.Kind=="Clue"&&v.NpcId==next).Count>1)stacked++;}
        }
        Check(nights>20&&stacked==0,stacked+" fake(s) landed on a found clue holder still showing TRUE, over "+nights+" nights");
    }
}
