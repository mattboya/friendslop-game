using System;
using System.Collections.Generic;
using Festival.Core;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public static class SpeechTests
{
    static int sequence;
    static void Check(bool value,string message){if(!value)throw new Exception("Speech: "+message);}
    static string Speaker(PlayerState p)
    {
        var field=typeof(PlayerState).GetField("SpeechById");
        Check(field!=null,"player speech records an actual speaker id");return (string)field.GetValue(p);
    }
    static FestivalSimulation Playing(out PlayerState p,out NpcState npc)
    {
        var s=new FestivalSimulation(7);p=s.AddPlayer("p0","P0");s.AddPlayer("p1","P1");
        s.State.Phase="Playing";s.State.SimulationSeconds=5;npc=s.State.Npcs.Find(n=>n.Kind=="Wook"&&n.CanTalk);
        npc.Role="Regular";p.X=npc.X;p.Z=npc.Z;return s;
    }
    static void Start(FestivalSimulation s,PlayerState p,NpcState n,string kind)
    {
        var result=s.Execute(p.Id,new GameCommand{Id="speech"+(sequence++),Kind=kind,TargetId=n.Id});Check(result.Accepted,"setup "+kind+": "+result.Reason);
    }
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{TalkNamesTheFestivalgoerWhoSpoke,ASoberChallengeLineIsSaidByTheNpc,ADosedChallengeLineIsSaidByThePlayer,AChatCheckIsNeverPutOnThePlayer,AcceptedBarksHaveEightStableDeliveries,AcceptedRepliesAndNpcTurnsSurviveOneSnapshot,EqualLinesAndASharedNpcKeepTheirIdentities,TrustAskAndFirstLineArePublicButTheDanceReactionIsPrivate,SecurityRetractionsAndCancellationKeepDeliveryIds,DitchingNeverLeavesAPublicWarningQueued,LegacyExpiryRemainsPauseSafeWithoutPublishingAnonymousSpeech})
            try{test();}catch(Exception e){failures.Add(test.Method.Name+" -> "+e.Message);}
        if(failures.Count>0)throw new Exception(string.Join("\n",failures));
    }
    static JsonArray Deliveries(RoundState state)
    {
        var wire=JsonNode.Parse(JsonSerializer.Serialize(state,new JsonSerializerOptions{IncludeFields=true}));
        Check(wire["PublicSpeech"] is JsonArray,"snapshots retain a bounded public delivery window");return wire["PublicSpeech"].AsArray();
    }
    static void AcceptedBarksHaveEightStableDeliveries()
    {
        var game=Playing(out var player,out var npc);
        for(int n=0;n<12;n++){game.State.SimulationSeconds=5+2*n;Start(game,player,npc,"Talk");}
        var delivered=Deliveries(game.State);Check(delivered.Count==8,"only the latest eight accepted utterances are retained");
        Check(delivered.Select(d=>d["Sequence"].GetValue<long>()).SequenceEqual(new long[]{5,6,7,8,9,10,11,12}),"eviction retains ascending monotonic identities");
        Check(delivered.Select(d=>d["DeliveryId"].GetValue<string>()).Distinct().Count()==8,"each spoken line has a unique delivery id");
        Check(delivered.All(d=>d["SpeakerId"].GetValue<string>()==npc.Id),"delivery records point to the real NPC");
        var command=new GameCommand{Id="repeat_delivery",Kind="Talk",TargetId=npc.Id};game.State.SimulationSeconds+=2;
        Check(game.Execute(player.Id,command).Accepted,"new bark accepted");string first=Deliveries(game.State).ToJsonString();
        Check(game.Execute(player.Id,command).Accepted&&Deliveries(game.State).ToJsonString()==first,"retransmission cannot deliver a second utterance");
        Check(!game.Execute(player.Id,new GameCommand{Id="rejected_delivery",Kind="Talk",TargetId=npc.Id}).Accepted&&Deliveries(game.State).ToJsonString()==first,"refused speech cannot create a delivery");
        var restore=new FestivalSimulation();restore.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(game.State,new JsonSerializerOptions{IncludeFields=true}),new JsonSerializerOptions{IncludeFields=true}));
        Check(Deliveries(restore.State).ToJsonString()==first,"restore keeps identities without republishing");restore.State.SimulationSeconds+=2;Start(restore,restore.Player(player.Id),restore.State.Npcs.Find(n=>n.Id==npc.Id),"Talk");
        Check(Deliveries(restore.State).Last()["Sequence"].GetValue<long>()==14,"restore continues the original monotonic sequence");
    }
    static FestivalSimulation ConversationPair(out PlayerState player,out NpcState npc)
    {
        var game=Playing(out player,out npc);game.State.Npcs.Clear();npc=new NpcState{Id="goer_0",Role="Regular",CanTalk=true,X=0,Z=1.5f,Yaw=180};
        Check(DialogueGrammar.PersonaFor(npc.Id,npc.Twist)=="DraggedAlongDad","fixed public production persona keeps this fixture out of automatic trust dances");
        game.State.Npcs.Add(npc);player.X=0;player.Z=0;game.Player("p1").X=0;game.Player("p1").Z=0;return game;
    }
    static void AcceptedRepliesAndNpcTurnsSurviveOneSnapshot()
    {
        foreach(string kind in new[]{"Conversation","StartSale","StartBuy","Police"})
        {
            var game=ConversationPair(out var player,out var npc);if(kind=="Police")npc.Kind="Cop";if(kind=="StartSale"){npc.Role="Buyer";player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});}
            var started=game.Execute(player.Id,new GameCommand{Id="public_start",Kind=kind,TargetId=npc.Id,ItemId=kind=="StartSale"?"stock_lsd":kind=="StartBuy"?FestivalSimulation.SellerStock(npc.Id):""});
            Check(started.Accepted,"public conversation starts: "+started.Reason);var talk=game.Interaction(player.InteractionId);var spoken=new List<string>();
            for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
            {
                spoken.Add(talk.Turns[turn].Line);spoken.Add(talk.Turns[turn].Replies[0]);
                var command=new GameCommand{Id="public_reply_"+turn,Kind="Reply",Direction=turn,Amount=0};Check(game.Execute(player.Id,command).Accepted,"accepted reply publishes");
                string before=Deliveries(game.State).ToJsonString();Check(game.Execute(player.Id,command).Accepted&&Deliveries(game.State).ToJsonString()==before,"reply retransmission adds no public utterance");
            }
            var delivered=Deliveries(game.State);Check(delivered.Count==2*FestivalSimulation.ConversationTurns,"four replies and four NPC turns arrive together without an invented public grade reaction");
            Check(delivered.Select(d=>d["Text"].GetValue<string>()).SequenceEqual(spoken),"only actually spoken lines and accepted choices are public");
            Check(delivered.Select(d=>d["SpeakerId"].GetValue<string>()).SequenceEqual(Enumerable.Range(0,2*FestivalSimulation.ConversationTurns).Select(k=>k%2==0?npc.Id:player.Id)),"every line belongs to its actual speaker");
            Check(delivered.Select(d=>d["StartedAtWorldSeconds"].GetValue<double>()).Distinct().Count()==1,"equal timestamps do not collapse accepted utterances");
            Check(!game.Execute(player.Id,new GameCommand{Id="past_reply",Kind="Reply",Direction=FestivalSimulation.ConversationTurns-1,Amount=0}).Accepted&&Deliveries(game.State).Count==2*FestivalSimulation.ConversationTurns,"a refused late reply is never public");
            if(kind!="Police")Check(player.ConversationOutcomes.Count==1&&!delivered.Any(d=>d["Text"].GetValue<string>()==player.ConversationOutcomes[0].Reaction),"the authored final grade reaction remains owner-only");
        }
    }
    static void EqualLinesAndASharedNpcKeepTheirIdentities()
    {
        var game=ConversationPair(out var player,out var npc);Start(game,player,npc,"Talk");var first=Deliveries(game.State).Last();
        player.Dialogue=new DialogueHistory();npc.TalkCount=0;game.State.SimulationSeconds+=2;Start(game,player,npc,"Talk");var second=Deliveries(game.State).Last();
        Check(first["Text"].GetValue<string>()==second["Text"].GetValue<string>()&&first["DeliveryId"].GetValue<string>()!=second["DeliveryId"].GetValue<string>(),"equal authored text is a new delivery");
        Start(game,player,npc,"Conversation");var talk=game.Interaction(player.InteractionId);
        Check(game.Execute(player.Id,new GameCommand{Id="shared_end",Kind="EndChat",TargetId=talk.Id}).Accepted,"first owner leaves the NPC free");
        Start(game,game.Player("p1"),npc,"Conversation");var delivered=Deliveries(game.State);
        Check(delivered.Count==4&&delivered.Select(d=>d["DeliveryId"].GetValue<string>()).Distinct().Count()==4,"two players talking to one NPC retain separate delivery identities");
    }
    static void TrustAskAndFirstLineArePublicButTheDanceReactionIsPrivate()
    {
        var game=ConversationPair(out var player,out var npc);npc.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=40});game.HasLineOfSight=(ax,az,bx,bz)=>false;
        Start(game,player,npc,"Conversation");var talk=game.Interaction(player.InteractionId);
        Check(Deliveries(game.State).Count==1&&Deliveries(game.State)[0]["Text"].GetValue<string>()==FestivalSimulation.TrustAsk,"the actual NPC trust ask is spoken");
        game.Tick(talk.StartSeconds-game.State.SimulationSeconds+talk.DurationSeconds+.8);
        // A failed dance leads on to the talk. Their first line is public, as every line of theirs is; their reaction to
        // the dance, as every reaction, stays on the talker's card.
        var delivered=Deliveries(game.State);Check(delivered.Count==2&&delivered.Last()["SpeakerId"].GetValue<string>()==npc.Id&&delivered.Last()["Text"].GetValue<string>()==talk.Turns[0].Line,"after the dance their first line is public");
        Check(talk.Turns[0].Reaction!=""&&!delivered.Any(d=>d["Text"].GetValue<string>()==talk.Turns[0].Reaction)&&player.ConversationOutcomes.Count==0,"their reaction to the dance stays private, and nothing has ended");
    }
    static void SecurityRetractionsAndCancellationKeepDeliveryIds()
    {
        var game=ConversationPair(out var player,out var npc);npc.Kind="Cop";npc.Id="cop_0";npc.X=0;npc.Z=0;npc.Yaw=0;player.Z=5;
        game.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});player.EquippedItemId="stock_lsd";
        game.Tick(.1);Check(Deliveries(game.State).Count==1,"the actual security stop has a public bark delivery");var delivery=Deliveries(game.State).Single();Check(delivery["SpeakerId"].GetValue<string>()==npc.Id&&delivery["Text"].GetValue<string>()=="HEY! What's in the bag?","the real stop publishes the actual cop's call-out");
        string id=delivery["DeliveryId"].GetValue<string>();long sequence=delivery["Sequence"].GetValue<long>();
        Check(game.Execute(player.Id,new GameCommand{Id="public_stow",Kind="Equip"}).Accepted,"stock stows");game.Tick(.1);
        delivery=Deliveries(game.State).Single();Check(delivery["Retracted"].GetValue<bool>()&&delivery["Text"].GetValue<string>()==""&&delivery["DeliveryId"].GetValue<string>()==id&&delivery["Sequence"].GetValue<long>()==sequence,"hushing the real stop retracts without manufacturing a new identity");
        game=ConversationPair(out player,out npc);Start(game,player,npc,"Dance");var dance=game.Interaction(player.InteractionId);id=Deliveries(game.State).Single()["DeliveryId"].GetValue<string>();
        Check(game.Execute(player.Id,new GameCommand{Id="public_cancel",Kind="Cancel"}).Accepted&&dance.Status=="Cancelled","the actual Dance cancels");
        delivery=Deliveries(game.State).Single();Check(delivery["Retracted"].GetValue<bool>()&&delivery["DeliveryId"].GetValue<string>()==id&&player.NpcSpeech=="","a cancelled challenge cannot leave a queued line behind");
    }
    static void DitchingNeverLeavesAPublicWarningQueued()
    {
        var game=ConversationPair(out var player,out var npc);npc.Kind="Cop";npc.Id="cop_0";npc.X=0;npc.Z=0;npc.Yaw=0;player.Z=5;
        game.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});player.EquippedItemId="stock_lsd";
        game.Tick(.1);game.Tick(12.1);Check(Deliveries(game.State).Last()["Text"].GetValue<string>()=="Last chance. Empty your pockets.","the real stop warns");
        Check(game.Execute(player.Id,new GameCommand{Id="public_ditch",Kind="Drop",ItemId="stock_lsd",Amount=1}).Accepted,"the last stock unit is ditched");game.Tick(.1);
        var delivered=Deliveries(game.State);Check(delivered.Last()["Text"].GetValue<string>()=="Fine. Move along.","the real cop acknowledges ditching");
        Check(delivered.Take(delivered.Count-1).All(d=>d["Retracted"].GetValue<bool>()&&d["Text"].GetValue<string>()==""),"older queued warnings are withdrawn before the new move-along bark");
    }
    static void LegacyExpiryRemainsPauseSafeWithoutPublishingAnonymousSpeech()
    {
        var game=ConversationPair(out var player,out var npc);game.HasLineOfSight=(ax,az,bx,bz)=>false;game.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);
        game.State.Booth.Kind=FestivalSimulation.MysteryCupsGame;game.State.Booth.Spot=0;var spot=FestivalSimulation.BoothAt(game.State);
        player.X=spot.X;player.Z=spot.Z;game.Player("p1").X=spot.X+1;game.Player("p1").Z=spot.Z;npc.X=spot.X;npc.Z=spot.Z+1;Start(game,player,npc,"Talk");
        var options=new JsonSerializerOptions{IncludeFields=true};var legacy=JsonNode.Parse(JsonSerializer.Serialize(game.State,options));legacy.AsObject().Remove("PublicSpeech");legacy.AsObject().Remove("SpeechSequence");
        foreach(var saved in legacy["Players"].AsArray())foreach(string field in new[]{"SpeechById","SpeechDeliveryId","SpeechSequence","SpeechAtWorldSeconds","SpeechUntilWorldSeconds"})saved.AsObject().Remove(field);
        var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(legacy.ToJsonString(),options));player=restored.Player(player.Id);restored.HasLineOfSight=(ax,az,bx,bz)=>false;restored.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);
        Check(restored.State.PublicSpeech.Count==0&&player.SpeechUntilWorldSeconds==-1,"older snapshots restore without fabricated public deliveries");
        Check(FestivalSimulation.SpeechRemainingSeconds(restored.State,player)==7,"legacy own-local bark reads its original deadline");restored.Tick(3.1);
        Check(restored.State.Phase==FestivalSimulation.MinigamePhase,"the real booth pauses the legacy snapshot");double left=FestivalSimulation.SpeechRemainingSeconds(restored.State,player);restored.Tick(5);
        Check(Math.Abs(FestivalSimulation.SpeechRemainingSeconds(restored.State,player)-left)<.001,"legacy expiry freezes through the actual game");
        string up=FestivalSimulation.CupsTurn(restored.State);Check(restored.Execute(up,new GameCommand{Id="legacy_call",Kind=FestivalSimulation.MinigameKind,ItemId=FestivalSimulation.CallCups}).Accepted,"the actual cups game scores");
        restored.Tick(2);Check(Math.Abs(FestivalSimulation.SpeechRemainingSeconds(restored.State,player)-left)<.001,"legacy expiry also freezes through the result banner");restored.Tick(2.1);
        Check(restored.State.Phase=="Playing"&&Math.Abs(FestivalSimulation.SpeechRemainingSeconds(restored.State,player)-left)<.001,"the shifted legacy deadline resumes with the same remaining seconds");
        restored.Tick(left+.1);Check(FestivalSimulation.SpeechRemainingSeconds(restored.State,player)==0,"the legacy bark expires on resumed world time");
    }
    static void TalkNamesTheFestivalgoerWhoSpoke()
    {
        var s=Playing(out var p,out var n);Start(s,p,n,"Talk");Check(Speaker(p)==n.Id,"Talk follows the actual NPC");
    }
    static void ASoberChallengeLineIsSaidByTheNpc()
    {
        var s=Playing(out var p,out var n);Start(s,p,n,"Dance");var i=s.Interaction(p.InteractionId);
        Check(p.NpcSpeech==i.DialogueText&&p.NpcSpeech!="","dance publishes its generated line");Check(Speaker(p)==n.Id,"sober line belongs to NPC");
        Check(p.NpcSpeechUntil==i.StartSeconds+i.DurationSeconds,"speech expiry is challenge end");
    }
    static void ADosedChallengeLineIsSaidByThePlayer()
    {
        var s=Playing(out var p,out var n);p.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,RemainingSeconds=60});Start(s,p,n,"Dance");
        Check(p.NpcSpeech==s.Interaction(p.InteractionId).DialogueText&&p.NpcSpeech!="","dosed reply is public speech");Check(Speaker(p)==p.Id,"dosed reply belongs to the actual player");
    }
    static void AChatCheckIsNeverPutOnThePlayer()
    {
        var s=Playing(out var p,out var n);s.State.TripperId=p.Id;s.State.Visions.Add(new VisionState{Id="vision",NpcId=n.Id,Kind="Buyer"});Start(s,p,n,"ConfirmChat");
        Check(s.Interaction(p.InteractionId).Chat!=null,"private check has grammar");Check(p.NpcSpeech==""&&p.NpcSpeaker==""&&p.NpcSpeechUntil==0,"check does not publish private dialogue");
    }
}
