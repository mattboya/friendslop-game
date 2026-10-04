using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

public static class UntimedChatTests
{
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("Untimed chat: "+message);}
    static CommandResult Act(FestivalSimulation s,string player,string kind,string target="",string item="",int amount=0,int direction=0)=>s.Execute(player,new GameCommand{Id="untimed_"+(sequence++),Kind=kind,TargetId=target,ItemId=item,Amount=amount,Direction=direction});
    static FestivalSimulation Pair(string kind)
    {
        var s=new FestivalSimulation(7);var p=s.AddPlayer("owner","Owner");s.AddPlayer("friend","Friend");s.State.Phase="Playing";s.State.Npcs.Clear();
        string id="goer_0";for(int n=1;DialogueGrammar.PersonaFor(id,"")!="Wook";n++)id="goer_"+n;
        s.State.Npcs.Add(new NpcState{Id=id,Kind=kind=="Police"?"Cop":"Wook",Role=kind=="Sale"?"Buyer":"Regular",CanTalk=true,Z=1.5f,Yaw=180});p.X=0;p.Z=0;
        if(kind=="Sale")p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
        if(kind=="ConfirmChat"){s.State.TripperId=p.Id;s.State.Visions.Add(new VisionState{Id="vision",NpcId=id,Kind="Buyer",IsTrue=false});}
        return s;
    }
    static InteractionState Start(FestivalSimulation s,string kind)
    {
        var n=s.State.Npcs[0];var result=Act(s,"owner",kind=="Sale"?"StartSale":kind=="Buy"?"StartBuy":kind,n.Id,kind=="Sale"?"stock_lsd":kind=="Buy"?FestivalSimulation.SellerStock(n.Id):"");
        Check(result.Accepted,"setup starts "+kind+": "+result.Reason);return s.Interaction(s.Player("owner").InteractionId);
    }
    public static void Run(){OrdinaryTalksWaitForEveryReply();ChecksEndOnlyOnTheirOwnersCommand();LegacyActiveChatsResumeWithoutInventedSilence();LeavingTalkRefundsAndCostsWitnessTrustOnce();InvalidEndsNeverConfirmOrEndAnotherAction();PoliceAndTrustDancesStillKeepTime();}
    static void LeavingTalkRefundsAndCostsWitnessTrustOnce()
    {
        foreach(string kind in new[]{"Conversation","Sale","Buy"})
        {
            var s=Pair(kind);var p=s.Player("owner");if(kind=="Buy")s.State.Npcs[0].Role="Narc";
            var watcher=new NpcState{Id="watcher",Z=3,Yaw=180};watcher.Observers.Add(new ObserverState{PlayerId=p.Id,Suspicion=20});s.State.Npcs.Add(watcher);
            var i=Start(s,kind);Check(i.WitnessIds.Contains(watcher.Id),"setup a witnessing festivalgoer");
            var command=new GameCommand{Id="end_talk_"+kind,Kind="EndChat",TargetId=i.Id};
            Check(s.Execute(p.Id,command).Accepted&&i.Status=="Cancelled"&&p.InteractionId=="","ending a talk cancels rather than settles it");
            Check(p.Life=="Alive"&&p.Cash==20&&i.ReservedCash==0&&i.ReservedItemId==""&&s.State.LevelSales==0,"a buy refunds, a narc cannot bust, and no sale pays");
            if(kind=="Sale")Check(p.Inventory.Find(x=>x.ItemId=="stock_lsd")?.Count==1,"a sale returns exactly one held unit");
            Check(watcher.Observers[0].Suspicion==40,"the existing walk-away cost is +20");
            Check(s.Execute(p.Id,command).Accepted&&!Act(s,p.Id,"EndChat",i.Id).Accepted&&p.Cash==20&&watcher.Observers[0].Suspicion==40,"retries neither refund nor penalize twice");
            if(kind=="Sale")Check(p.Inventory.Find(x=>x.ItemId=="stock_lsd")?.Count==1,"stock is not duplicated on retry");
            var next=Start(s,kind);Check(!Act(s,p.Id,"EndChat",i.Id).Accepted&&next.Status=="Active","a stale end cannot cancel the next conversation");
        }
    }
    static void InvalidEndsNeverConfirmOrEndAnotherAction()
    {
        foreach(string invalid in new[]{"reach","target","life","phase","ownership"})
        {
            var s=Pair("ConfirmChat");var i=Start(s,"ConfirmChat");var p=s.Player("owner");
            if(invalid=="reach")p.X=5;if(invalid=="target")s.State.Npcs.Clear();if(invalid=="life")p.Life="Downed";if(invalid=="phase")s.State.Phase="Results";if(invalid=="ownership")i.PlayerId="friend";
            Check(!Act(s,p.Id,"EndChat",i.Id).Accepted&&!s.State.Visions[0].Confirmed,"invalid "+invalid+" end reveals nothing");
            if(invalid=="reach"||invalid=="target")Check(i.Status!="Active"&&p.InteractionId=="","invalid task is cancelled without confirmation");
        }
        foreach(string kind in new[]{"Conversation","Dance"})
        {
            var s=Pair(kind);if(kind=="Conversation")s.State.Npcs[0].Observers.Add(new ObserverState{PlayerId="owner",Suspicion=40});var i=Start(s,kind);
            Check(!Act(s,"owner","EndChat",i.Id).Accepted&&i.Status=="Active","EndChat cannot stop a rhythm interaction");
        }
        var moved=Pair("ConfirmChat");var chat=Start(moved,"ConfirmChat");moved.Player("owner").X=5;moved.Tick(.1);
        Check(chat.Status!="Active"&&!moved.State.Visions[0].Confirmed,"walking away still cancels the check in the finish loop");
    }
    static void PoliceAndTrustDancesStillKeepTime()
    {
        var s=Pair("Police");var i=Start(s,"Police");s.Tick(4.1);
        Check(i.Status=="Active"&&i.Turns.Count==2&&i.Turns[0].Pick==-1,"Police advances an unanswered turn after four seconds");
        s.State.SimulationSeconds=i.StartSeconds+i.DurationSeconds;
        Check(!Act(s,"owner","Reply",amount:0,direction:1).Accepted,"Police rejects a reply exactly at its deadline");
        s.Tick(4.1);Check(i.Status=="Complete"&&i.Score==0&&i.Turns.Count==3,"Police settles three silent turns on its clock");
        s=Pair("Conversation");s.State.Npcs[0].Observers.Add(new ObserverState{PlayerId="owner",Suspicion=40});i=Start(s,"Conversation");
        double deadline=i.DurationSeconds;var copy=new FestivalSimulation();copy.Restore(s.State);
        Check(i.DurationSeconds==deadline&&i.Turns.Count==0&&FestivalSimulation.TrustDancing(i),"restore preserves the active trust chart");
        s.Tick(i.StartSeconds-s.State.SimulationSeconds+deadline+.8);Check(i.Status=="Complete"&&i.Turns.Count==0,"an unanswered trust dance still ends on time");
    }
    static void LegacyActiveChatsResumeWithoutInventedSilence()
    {
        foreach(string kind in new[]{"Conversation","Sale","Buy","ConfirmChat","Police"})
        {
            var s=Pair(kind);var i=Start(s,kind);var options=new JsonSerializerOptions{IncludeFields=true};
            if(kind!="ConfirmChat")Check(Act(s,"owner","Reply",amount:0,direction:0).Accepted,"setup first pick");
            i.DurationSeconds=8;s.State.SimulationSeconds=60;
            var json=JsonSerializer.Serialize(s.State,options);var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(json,options));
            var saved=restored.Interaction(i.Id);
            if(kind!="Police")Check(saved.DurationSeconds==0,"restore upgrades an old "+kind+" deadline to finite zero");
            if(kind!="ConfirmChat")Check(saved.Turns.Count==2&&saved.Turns[0].Pick==0&&saved.Turns[1].Pick==-1,"restore keeps picks and the unanswered live turn");
            if(kind=="Police"){Check(saved.DurationSeconds==8,"a saved Police deadline remains unchanged");continue;}
            restored.Tick(60);Check(saved.Status=="Active","restored ordinary chats keep waiting");
            if(kind=="ConfirmChat")continue;
            var older=JsonNode.Parse(json);var talk=older["Interactions"][0].AsObject();talk.Remove("Turns");talk.Remove("TrustDance");talk.Remove("Hunch");
            restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(older.ToJsonString(),options));saved=restored.Interaction(i.Id);
            Check(saved.Turns.Count==1&&saved.Turns[0].Pick==-1&&saved.Status=="Active","a pre-conversation snapshot opens its first turn once");
            string line=saved.Turns[0].Line;restored.Restore(restored.State);restored.Tick(60);
            Check(saved.Turns.Count==1&&saved.Turns[0].Line==line&&saved.Turns[0].Pick==-1,"restoring again neither redraws nor advances the turn");
        }
    }
    static void ChecksEndOnlyOnTheirOwnersCommand()
    {
        var s=Pair("ConfirmChat");var i=Start(s,"ConfirmChat");s.Tick(60);
        s.Player("friend").X=0;s.Player("friend").Z=0;
        Check(i.Status=="Active"&&!s.State.Visions[0].Confirmed,"a check waits 60 seconds without revealing truth");
        Check(i.DurationSeconds==0,"a check has finite zero duration");
        Check(!Act(s,"friend","EndChat",i.Id).Accepted&&i.Status=="Active","another player cannot end the owner's check");
        Check(!Act(s,"owner","EndChat","stale").Accepted&&i.Status=="Active","a stale interaction cannot end the live check");
        var command=new GameCommand{Id="end_check",Kind="EndChat",TargetId=i.Id};
        var result=s.Execute("owner",command);Check(result.Accepted&&i.Status=="Complete"&&s.State.Visions[0].Confirmed,"EndChat completes the owned check");
        Check(s.Execute("owner",command)==result,"a retry returns the original result");
        Check(!Act(s,"owner","EndChat",i.Id).Accepted,"a second command cannot complete a finished check again");
    }
    static void OrdinaryTalksWaitForEveryReply()
    {
        foreach(string kind in new[]{"Conversation","Sale","Buy"})
        {
            var s=Pair(kind);var i=Start(s,kind);var first=i.Turns[0];s.Tick(60);
            Check(i.Status=="Active"&&i.Turns.Count==1&&first.Pick==-1,kind+" waits 60 seconds without inventing silence");
            Check(i.DurationSeconds==0&&!double.IsInfinity(i.DurationSeconds)&&!double.IsNaN(i.DurationSeconds),"untimed duration is finite zero");
            for(int turn=0;turn<3;turn++)
            {
                Check(Act(s,"owner","Reply",amount:Array.IndexOf(i.Turns[turn].Grades,ConversationLines.Smooth),direction:turn).Accepted,"reply accepted after reading");
                if(turn<2){s.Tick(60);Check(i.Status=="Active"&&i.Turns.Count==turn+2&&i.Turns[turn+1].Pick==-1,"each next turn waits too");}
            }
            Check(i.Status=="Complete"&&i.Score==1&&s.Player("owner").Performances==1,"exactly three accepted replies settle once");
        }
    }
}
