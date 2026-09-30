using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// CROWD-2: a crew of 5 or more at the start of a night looks for two lost friends at different spots, each at the end of its
// own clue trail of the night's length. The night is won only with both friends at the way back to camp, and Night 2 still
// brings everyone home. Days, and smaller crews, are unchanged.
public static class SplitObjectiveTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static readonly int[] Shown={0,1,2,4,10};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("SplitObjective: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="")=>s.Execute(id,new GameCommand{Id="split"+(sequence++),Kind=kind,TargetId=target});
    static PlayerState Tripper(FestivalSimulation s)=>s.Player(s.State.TripperId);
    static int Dose(PlayerState p)=>p.Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect)?.Intensity??0;
    static NpcState Npc(FestivalSimulation s,string id)=>s.State.Npcs.Find(n=>n.Id==id);
    static List<string> Chain(FestivalSimulation s,int trail)=>trail==0?s.State.ClueChain:s.State.SecondFriend.ClueChain;
    static List<VisionState> Clues(FestivalSimulation s,int trail)=>s.State.Visions.FindAll(v=>v.Kind=="Clue"&&v.Trail==trail);
    static double Distance(WorldPoint a,WorldPoint b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z));
    static PlayerState Place(FestivalSimulation s,string id,WorldPoint at){var p=s.Player(id);p.X=at.X;p.Z=at.Z;return p;}
    static readonly WorldPoint Gate=new WorldPoint(Festivals.CampGateX,Festivals.CampGateZ);

    // `crew` friends meet at camp and `gone` of them leave before the level; the rest ready up, the wheels land and
    // everyone left loads into the real crowd of this level (1 = Night 1).
    static FestivalSimulation Start(int crew,int level=1,int seed=3,int gone=0)
    {
        var s=new FestivalSimulation(seed);for(int i=0;i<crew;i++)s.AddPlayer("p"+i,"P"+i);
        s.State.LevelIndex=level;for(int i=0;i<gone;i++)s.Disconnect("p"+(crew-1-i));
        foreach(var p in s.State.Players.FindAll(x=>x.Connected)){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: the crew reaches the festival");return s;
    }
    // The tripper checks every remaining link of one friend's trail, in order.
    static void FollowTrail(FestivalSimulation s,int trail){foreach(var id in Chain(s,trail).ToList())s.ConfirmVisionsOf(Npc(s,id));}
    // id recruits the lost friend standing at `at`; true when the 2 s task was accepted.
    static bool Recruit(FestivalSimulation s,string id,WorldPoint at){Place(s,id,at);if(!Act(s,id,"FindFriend").Accepted)return false;s.Tick(2.1);return true;}
    // The whole crew, and any friend they have found, at the way back to camp.
    static void AllAtGate(FestivalSimulation s)
    {
        foreach(var p in s.State.Players)Place(s,p.Id,Gate);
        if(s.State.FriendFound){s.State.FriendPosition.X=Gate.X;s.State.FriendPosition.Z=Gate.Z;}
        if(s.State.SecondFriend.Found){s.State.SecondFriend.Position.X=Gate.X;s.State.SecondFriend.Position.Z=Gate.Z;}
    }
    static bool Extracts(FestivalSimulation s,string id){if(!Act(s,id,"Extract").Accepted)return false;s.Tick(3.1);return s.State.Result=="Success";}

    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{FiveOrMoreGetTwoFriends,EachTrailShowsItsOwnNextLink,FindingTheSecondFriend,SuccessNeedsBoth,
            NightTwoStillBringsEveryoneHome,TheSecondFriendFollowsTheEscort,SnapshotsKeepTheSecondFriend,GuidanceCountsBothFriends})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" split objective test(s) failed:\n"+string.Join("\n",failures));
    }

    static void FiveOrMoreGetTwoFriends()
    {
        foreach(var level in new[]{1,3})for(int crew=1;crew<=8;crew++)
        {
            var s=Start(crew,level,seed:crew+level);var second=s.State.SecondFriend;int links=Festivals.For(s.State).ChainLength;string where=crew+" players on level "+level;
            Check(s.State.ClueChain.Count==links,where+": the first friend's trail has the night's "+links+" links");
            if(crew<FestivalSimulation.TwoFriendCrew){Check(!second.Active&&second.ClueChain.Count==0&&s.State.Npcs.Count(n=>n.Role=="ClueHolder")==links,where+": one lost friend");continue;}
            Check(second.Active,where+": a second friend is lost");
            Check(second.ClueChain.Count==links&&second.ClueChain.Distinct().Count()==links,where+": the second friend has its own "+links+"-link trail, got "+second.ClueChain.Count);
            Check(!second.ClueChain.Intersect(s.State.ClueChain).Any(),where+": the two trails share no clue holder");
            Check(s.State.Npcs.Count(n=>n.Role=="ClueHolder")==2*links&&second.ClueChain.All(id=>Npc(s,id).Role=="ClueHolder"),where+": every link of both trails is a dealt clue holder");
            Check(Distance(second.Position,s.State.FriendPosition)>10,where+": the friends are lost at different spots, got "+second.Position.X+","+second.Position.Z+" and "+s.State.FriendPosition.X+","+s.State.FriendPosition.Z);
            Check(!second.GateOpened&&!second.Found&&second.CluesRead==0&&second.LeaderId=="",where+": the second friend starts lost at the start of its trail");
        }
        var spots=new HashSet<string>();for(int seed=0;seed<6;seed++){var s=Start(5,seed:seed);spots.Add(s.State.SecondFriend.Position.X+","+s.State.SecondFriend.Position.Z);}
        Check(spots.Count>1,"the second friend is not always lost at the same spot");
        foreach(var level in new[]{0,2}){var s=Start(8,level);Check(!s.State.SecondFriend.Active&&s.State.SecondFriend.ClueChain.Count==0,"days are unchanged: nobody is lost on level "+level);}
        Check(!Start(5,gone:1).State.SecondFriend.Active,"only players connected as the level starts count: 5 met at camp, 4 went");
        Check(Start(6,gone:1).State.SecondFriend.Active,"6 met at camp and 5 went: two friends");
        var left=Start(5);left.Disconnect("p4");left.Tick(1);
        Check(left.State.SecondFriend.Active&&left.State.SecondFriend.ClueChain.Count==2,"a player leaving mid-night leaves the second friend lost");
        var ember=new FestivalSimulation(4);for(int i=0;i<5;i++)ember.AddPlayer("p"+i,"P"+i);
        ember.State.UnlockedFestivalCount=Festivals.Count;ember.State.FestivalIndex=1;ember.State.LevelIndex=3;ember.State.EncoreTier=3;
        foreach(var p in ember.State.Players){p.X=0;p.Z=19;Act(ember,p.Id,"Ready");}
        ember.Tick(5.2);ember.Tick(ember.State.SpinEndsAt-ember.State.SimulationSeconds+.1);
        Check(ember.State.ClueChain.Count==Festivals.MaxChainLength&&ember.State.SecondFriend.ClueChain.Count==Festivals.MaxChainLength,"the longest trails (Ember Playa Night 2, third encore) both fit in the crowd");
    }

    static void EachTrailShowsItsOwnNextLink()
    {
        var doses=new HashSet<int>();
        for(int seed=0;seed<40&&doses.Count<4;seed++)
        {
            var s=Start(5,seed:seed);int dose=Dose(Tripper(s));if(!doses.Add(dose))continue;string where="dose "+dose+" seed "+seed+": ";
            for(int trail=0;trail<2;trail++)
            {
                var clues=Clues(s,trail);
                Check(clues.Count==Shown[dose]&&clues.Count(v=>v.IsTrue)==1&&clues.Find(v=>v.IsTrue).NpcId==Chain(s,trail)[0],where+"trail "+trail+" shows its first clue holder among the dose's "+(Shown[dose]-1)+" fakes");
            }
            var all=s.State.Visions.FindAll(v=>v.Kind=="Clue");
            Check(all.Count==2*Shown[dose]&&all.Select(v=>v.NpcId).Distinct().Count()==all.Count,where+"no festivalgoer carries two clue visions");
            var firstBefore=string.Join(";",Clues(s,0).ConvertAll(v=>v.Id+v.NpcId+v.IsTrue));var fake=Clues(s,0).Find(v=>!v.IsTrue);
            if(fake!=null)s.ConfirmVisionsOf(Npc(s,fake.NpcId));
            s.ConfirmVisionsOf(Npc(s,Chain(s,1)[0]));
            Check(s.State.SecondFriend.CluesRead==1&&s.State.CluesRead==0,where+"finding the second trail's holder moves only that trail on");
            Check(string.Join(";",Clues(s,0).ConvertAll(v=>v.Id+v.NpcId+v.IsTrue))==firstBefore&&(fake==null||fake.Confirmed),where+"the first trail's visions stay as they were, checked fakes included");
            var next=Clues(s,1);
            Check(next.Count==Shown[dose]&&next.Find(v=>v.IsTrue)?.NpcId==Chain(s,1)[1]&&next.TrueForAll(v=>!v.Confirmed),where+"the second trail now shows its next link, unchecked");
            FollowTrail(s,1);
            Check(s.State.SecondFriend.GateOpened&&!s.State.GateOpened&&Clues(s,1).Count==0&&Clues(s,0).Count==Shown[dose],where+"the second trail's end reveals only the second friend, and only its visions clear");
            FollowTrail(s,0);
            Check(s.State.GateOpened&&!s.State.Visions.Exists(v=>v.Kind=="Clue"),where+"then the first trail ends too");
        }
        Check(doses.Count==4,"setup: every dose was spun");
        // Through the real check: the tripper chats with the second trail's first clue holder.
        var chat=Start(5,seed:7);var tripper=Tripper(chat);var holder=Npc(chat,Chain(chat,1)[0]);
        double yaw=holder.Yaw*Math.PI/180;tripper.X=holder.X+(float)Math.Sin(yaw);tripper.Z=holder.Z+(float)Math.Cos(yaw);
        Check(Act(chat,tripper.Id,"ConfirmChat",holder.Id).Accepted,"the tripper can check the second trail's clue holder");
        chat.Tick(5.2);Check(chat.State.SecondFriend.CluesRead==1&&chat.State.CluesRead==0,"and the chat moves the second trail on");
    }

    static void FindingTheSecondFriend()
    {
        // No secret stash at a friend's spot muddles the crew's stash sums below.
        var s=Start(5);var second=s.State.SecondFriend;s.State.Visions.RemoveAll(v=>v.Kind=="Stash");FollowTrail(s,1);
        Check(!Recruit(s,"p0",s.State.FriendPosition),"the first friend's trail is not finished, so nobody reaches them yet");
        Check(Act(s,"p0","FindFriend").Reason.Contains("clue trail"),"and the refusal says to follow the trail");
        Place(s,"p1",new WorldPoint(second.Position.X+6,second.Position.Z));Check(!Act(s,"p1","FindFriend").Accepted,"6 m off is too far to recruit the second friend");
        Place(s,"p1",second.Position);Check(Act(s,"p1","FindFriend").Accepted,"at the second friend, the finished trail lets p1 recruit them");
        s.Player("p1").X+=5;s.Tick(2.1);Check(!second.Found,"walking off mid-recruit cancels it");
        Check(Recruit(s,"p1",second.Position)&&second.Found&&second.LeaderId=="p1"&&!s.State.FriendFound,"recruited, the second friend follows p1; the first is still lost");
        Check(s.State.StashCash==20&&s.State.ObjectiveReward==20,"a rescued friend banks $20 for the crew");
        Check(Recruit(s,"p2",second.Position)&&second.LeaderId=="p2"&&s.State.StashCash==20,"p2 can take over the escort without paying twice");
        FollowTrail(s,0);
        Check(Recruit(s,"p3",s.State.FriendPosition)&&s.State.FriendFound&&s.State.FriendLeaderId=="p3","p3 recruits the first friend once their trail ends");
        Check(s.State.StashCash==40&&s.State.ObjectiveReward==40,"each friend banks $20: $40 for both");
    }

    static void SuccessNeedsBoth()
    {
        var four=Start(4);FollowTrail(four,0);Check(Recruit(four,"p0",four.State.FriendPosition),"setup: four players recruit their one friend");
        AllAtGate(four);Check(Extracts(four,"p0"),"four players go home with one friend, as before");
        var s=Start(5);FollowTrail(s,0);FollowTrail(s,1);
        Check(Recruit(s,"p0",s.State.FriendPosition),"setup: p0 recruits the first friend");AllAtGate(s);
        var refused=Act(s,"p0","Extract");Check(!refused.Accepted&&refused.Reason.Contains("both"),"one friend is not enough for five: "+refused.Reason);
        s.State.SecondFriend.Position.X=Gate.X;s.State.SecondFriend.Position.Z=Gate.Z;
        Check(!Act(s,"p0","Extract").Accepted,"the second friend standing at the gate is not rescued until someone recruits them");
        var spot=new WorldPoint(18,5);s.State.SecondFriend.Position.X=spot.X;s.State.SecondFriend.Position.Z=spot.Z;
        Check(Recruit(s,"p1",spot),"setup: p1 recruits the second friend");Place(s,"p1",Gate);
        Check(!Act(s,"p0","Extract").Accepted,"the second friend still out in the grounds blocks the extract");
        AllAtGate(s);Check(Act(s,"p0","Extract").Accepted,"both friends at the gate: the extract starts");
        s.State.SecondFriend.Position.X=spot.X;s.State.SecondFriend.Position.Z=spot.Z;s.Player("p1").X=spot.X;s.Player("p1").Z=spot.Z;s.Tick(3.1);
        Check(s.State.Phase=="Playing","the second friend wandering off before the extract finishes means no win yet");
        AllAtGate(s);Check(Extracts(s,"p0"),"both friends back at camp: the night is won");
    }

    static void NightTwoStillBringsEveryoneHome()
    {
        var s=Start(5,3);FollowTrail(s,0);FollowTrail(s,1);
        Check(Recruit(s,"p0",s.State.FriendPosition)&&Recruit(s,"p1",s.State.SecondFriend.Position),"setup: both friends recruited on Night 2");
        AllAtGate(s);Place(s,"p4",new WorldPoint(0,-15));var refused=Act(s,"p0","Extract");
        Check(!refused.Accepted,"both friends home but p4 is 17 m out: not everyone is home");
        Check(refused.Reason.Contains("both friends")&&refused.Reason.Contains("whole crew"),"the refusal asks for both friends and the whole crew: "+refused.Reason);
        Place(s,"p4",Gate);Check(Extracts(s,"p0"),"everyone home with both friends wins the weekend");
    }

    static void TheSecondFriendFollowsTheEscort()
    {
        var s=Start(5);var second=s.State.SecondFriend;FollowTrail(s,1);Check(Recruit(s,"p1",second.Position),"setup: p1 recruits the second friend");
        var from=new WorldPoint(second.Position.X,second.Position.Z);var p1=Place(s,"p1",new WorldPoint(from.X,from.Z-10));
        s.Tick(1);Check(Math.Abs(Distance(second.Position,from)-4)<.05,"the friend walks after their escort at 4 m/s, got "+Distance(second.Position,from));
        s.Tick(5);Check(Distance(second.Position,new WorldPoint(p1.X,p1.Z))<=1.51,"and stops beside them");
        s.Disconnect("p1");s.Tick(.1);var stay=new WorldPoint(second.Position.X,second.Position.Z);s.Tick(1);
        Check(second.LeaderId==""&&Distance(second.Position,stay)<1e-4&&second.Found,"an escort who leaves lets go; the friend waits, still found");
    }

    static void SnapshotsKeepTheSecondFriend()
    {
        var s=Start(5);FollowTrail(s,1);Check(Recruit(s,"p1",s.State.SecondFriend.Position),"setup: p1 recruits the second friend");
        var json=JsonSerializer.Serialize(s.State,Json);var copy=new FestivalSimulation();copy.Restore(JsonSerializer.Deserialize<RoundState>(json,Json));
        var a=s.State.SecondFriend;var b=copy.State.SecondFriend;
        Check(b.Active&&b.GateOpened&&b.Found&&b.LeaderId=="p1"&&b.CluesRead==a.CluesRead&&string.Join(",",b.ClueChain)==string.Join(",",a.ClueChain)&&Distance(a.Position,b.Position)<1e-4,"the second friend survives a snapshot");
        Check(string.Join(",",copy.State.Visions.ConvertAll(v=>v.Id+v.Trail))==string.Join(",",s.State.Visions.ConvertAll(v=>v.Id+v.Trail)),"each vision keeps its trail");
        var old=JsonNode.Parse(json).AsObject();Check(old.ContainsKey("SecondFriend"),"SecondFriend is a serialised public field");
        old.Remove("SecondFriend");copy=new FestivalSimulation();copy.Restore(JsonSerializer.Deserialize<RoundState>(old.ToJsonString(),Json));
        Check(!copy.State.SecondFriend.Active&&copy.State.SecondFriend.ClueChain.Count==0,"a snapshot from before the second friend restores with one friend");
        foreach(var field in new[]{"","ClueChain","Position"})
        {
            var broken=JsonNode.Parse(json).AsObject();if(field=="")broken["SecondFriend"]=null;else broken["SecondFriend"][field]=null;bool refused=false;
            try{new FestivalSimulation().Restore(JsonSerializer.Deserialize<RoundState>(broken.ToJsonString(),Json));}catch(ArgumentException){refused=true;}
            Check(refused,"a snapshot with a null SecondFriend"+(field==""?"":"."+field)+" is refused");
        }
    }

    // The objective card (FestivalGuidance, read from a player's view) tells a big crew there are two friends to find.
    static void GuidanceCountsBothFriends()
    {
        var four=Start(4);Check(FestivalGuidance.Headline(four.State,four.Player("p0"))=="FOLLOW THE CLUE TRAIL 0 / 2","four players keep the one-friend trail headline");
        var s=Start(5);var second=s.State.SecondFriend;var tripper=Tripper(s);var mate=s.State.Players.Find(p=>p!=tripper);
        foreach(var p in s.State.Players)Place(s,p.Id,Gate);
        string Headline(PlayerState p)=>FestivalGuidance.Headline(s.State,p);string Hint(PlayerState p)=>FestivalGuidance.Hint(s.State,p);
        Check(Headline(mate)=="TWO FRIENDS LOST • 0 / 2 FOUND","the headline says two friends are lost: "+Headline(mate));
        Check(Hint(tripper).StartsWith("Your visions mark the next clue holder")&&Hint(mate).StartsWith("Stick with"),"until a trail ends the crew sticks with the tripper: "+Hint(tripper)+" / "+Hint(mate));
        FollowTrail(s,1);
        Check(Hint(mate).StartsWith("Friend spotted")&&Hint(mate).Contains(" m "),"the second trail's end points the crew at the second friend: "+Hint(mate));
        // A client's view hides a lost friend out of sight at (0, 0): head for a friend in sight before searching for one who is not.
        var view=JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json);view.GateOpened=true;view.FriendPosition=new WorldPoint(0,0);
        Check(FestivalGuidance.Hint(view,mate).StartsWith("Friend spotted"),"with both trails done, the friend in sight comes before the one out of sight: "+FestivalGuidance.Hint(view,mate));
        view.SecondFriend.Position=new WorldPoint(0,0);
        Check(FestivalGuidance.Hint(view,mate).StartsWith("Search the north"),"with neither in sight, the crew searches: "+FestivalGuidance.Hint(view,mate));
        Check(Recruit(s,mate.Id,second.Position),"setup: the second friend is recruited");Place(s,mate.Id,Gate);
        Check(Headline(mate)=="TWO FRIENDS LOST • 1 / 2 FOUND","one found, one to go: "+Headline(mate));
        Check(Hint(tripper).StartsWith("Your visions mark the next clue holder")&&!Hint(mate).Contains("shuttle"),"with the first friend still lost nobody is sent home yet: "+Hint(tripper)+" / "+Hint(mate));
        FollowTrail(s,0);Check(Recruit(s,tripper.Id,s.State.FriendPosition),"setup: the first friend is recruited");
        Check(Headline(mate)=="ESCORT BOTH FRIENDS BACK TO CAMP","both found: take them home: "+Headline(mate));
        Check(Hint(tripper).StartsWith("Lead your friend"),"and their escort leads the way: "+Hint(tripper));
        var early=Start(5,seed:5);var guide=Tripper(early);FollowTrail(early,0);Check(Recruit(early,guide.Id,early.State.FriendPosition),"setup: the first friend is found first");
        Check(FestivalGuidance.Hint(early.State,guide).StartsWith("Your visions mark the next clue holder"),"the first friend's escort is not sent home while the second is lost: "+FestivalGuidance.Hint(early.State,guide));
    }
}
