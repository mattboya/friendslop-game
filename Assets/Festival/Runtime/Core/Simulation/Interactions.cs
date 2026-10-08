using System;
namespace Festival.Core {
public sealed partial class FestivalSimulation {
    InteractionState NewInteraction(PlayerState p,string kind,string target,double duration){var i=new InteractionState{Id=Id("interaction"),PlayerId=p.Id,TargetId=target,Kind=kind,StartSeconds=State.SimulationSeconds,DurationSeconds=duration,ChartSeed=State.Seed+(int)State.EntitySequence,ChartDifficulty=State.LevelIndex};State.Interactions.Add(i);p.InteractionId=i.Id;return i;}
    CommandResult BeginTask(PlayerState p,string kind,string target,double duration){NewInteraction(p,kind,target,duration);return Ok();}
    CommandResult BeginChallenge(PlayerState p,GameCommand c,string kind){var npc=State.Npcs.Find(n=>n.Id==c.TargetId);if(npc==null||!Near(p,npc.X,npc.Z)||((kind=="Police")!=(npc.Kind=="Cop")))return Reject("Choose a nearby appropriate NPC");if(kind=="Conversation"&&!npc.CanTalk)return Reject("This person is keeping to themselves");if(kind=="Sale"&&(!Stock(c.ItemId)||Count(p,c.ItemId)<1||npc.Sales>=SalesPerBuyer(State)))return Reject("Stock required; a buyer takes "+SalesPerBuyer(State)+" sales per round");if(State.Interactions.Exists(x=>x.TargetId==npc.Id&&x.Status=="Active"))return Reject("NPC is busy");if(Talks(kind)&&WorkedUp(npc))return Reject(TooAgitated);if(kind=="Sale"&&!Buys(npc))return RefuseSale(p,npc);if(kind=="Buy"){var refused=RefuseBuy(p,c,npc);if(refused!=null)return refused;}
        // DIALOG-3: a sale, a buy, a chat and a talk with security are conversations (Conversations.cs); only a Dance dances here.
        // K2: a talk roots you, so none starts with someone worked up (WorkedUp: accusing or swarming anyone), as Talk and the chat
        // check refuse them too; refused above, before a refused sale or buy could draw their look or a narc's bust.
        if(Talks(kind))return BeginTalk(p,c,npc,kind);
        GiveDanceRoom(p,npc);var i=NewInteraction(p,kind,npc.Id,0);ScheduleStageRhythm(i);var reserved=new System.Collections.Generic.List<string>();foreach(var active in State.Interactions)if(active.Status=="Active"&&active.DialogueId!="")reserved.Add(active.DialogueId);var line=p.Dialogue.Select("dance",State.FestivalIndex,i.ChartSeed,reserved);bool sober=p.Effects.Count==0;i.DialogueId=sober?line.Id:line.AwkwardId;i.DialogueText=sober?line.Text:line.AwkwardText;PublishSpeech(p,sober?npc.Id:p.Id,sober?"FESTIVALGOER":p.Name,i.DialogueText,i.StartSeconds+i.DurationSeconds-State.SimulationSeconds,i.Id);foreach(var n in State.Npcs)if(n.Kind=="Wook"&&Sees(n,p))i.WitnessIds.Add(n.Id);return Ok();}
    const string TooAgitated="They're too agitated to chat";
    CommandResult Talk(PlayerState p,GameCommand c)
    {
        var npc=State.Npcs.Find(n=>n.Id==c.TargetId);
        if(npc==null||npc.Kind!="Wook"||!npc.CanTalk||!Near(p,npc.X,npc.Z))return Reject("Find someone who wants to chat nearby");
        if(WorkedUp(npc))return Reject(TooAgitated);
        if(State.SimulationSeconds-npc.LastTalkSeconds<2)return Reject("Give them a moment");
        npc.LastTalkSeconds=State.SimulationSeconds;
        int hash=17;foreach(char ch in npc.Id)hash=unchecked(hash*31+ch);
        // DIALOG-4: a bark built by FestivalLines' grammar, seeded by the round, the festivalgoer and their talk count, and drawn
        // again past any line this player has heard lately.
        string line=p.Dialogue.Fresh(FestivalLines.Grammar,"bark",State.FestivalIndex,new ContentRandom((int)(((long)State.Seed*31+hash+npc.TalkCount*7)&0x7fffffff)),out var key);
        p.Dialogue.Acknowledge(key);
        npc.TalkCount++;
        PublishSpeech(p,npc.Id,"FESTIVALGOER",line,7);
        return Ok();
    }
    CommandResult BeginPoi(PlayerState p,GameCommand c){string item=c.ItemId==""?"poi_practice":c.ItemId;if((item!="poi_practice"&&item!="poi_led")||Count(p,item)<1)return Reject("Owned poi required");var i=NewInteraction(p,"Poi",p.Id,0);i.ReservedItemId=item;foreach(var n in State.Npcs)if(n.Kind=="Wook"&&Sees(n,p))i.WitnessIds.Add(n.Id);i.NoteCount=12;i.BeatSeconds=.4;i.GoodWindowSeconds=item=="poi_practice"?.1725:.15;ScheduleStageRhythm(i);var partner=Player(c.TargetId);var other=partner==null?null:Interaction(partner.InteractionId);if(partner!=null&&partner!=p&&partner.Life=="Alive"&&Near(p,partner.X,partner.Z,4)&&other!=null&&other.Kind=="Poi"&&other.Status=="Active"&&other.PartnerId==""&&State.SimulationSeconds<other.StartSeconds){i.PartnerId=partner.Id;other.PartnerId=p.Id;i.StartSeconds=other.StartSeconds;i.ChartSeed=other.ChartSeed;i.DurationSeconds=other.DurationSeconds;i.StageTrack=other.StageTrack;i.StageFirstHeadWorldSeconds=other.StageFirstHeadWorldSeconds;i.StageFirstHeadDelaySeconds=other.StageFirstHeadDelaySeconds;i.StageBeatStride=other.StageBeatStride;}return Ok();}
    CommandResult BeginDj(PlayerState p,GameCommand c){if(!Near(p,Catalog.StageTakeoverX,Catalog.StageTakeoverZ,Catalog.StageTakeoverStartRange)||Count(p,"stage_pass")<1||State.Interactions.Exists(i=>i.Kind=="Dj"&&i.Status=="Active"))return Reject("Move up to the stage-front deck with a pass");Take(p,"stage_pass",1);p.X=Catalog.StageTakeoverX;p.Z=Catalog.StageTakeoverZ;p.Yaw=0;var i=NewInteraction(p,"Dj","stage",0);i.NoteCount=12;i.BeatSeconds=.4;i.StartSeconds+=2;i.DurationSeconds=RhythmChart.For(i).DurationSeconds;return Ok();}
    // DIALOG-3: a talk is played on the lanes only while it is the trust dance a wary festivalgoer asked for.
    static bool IsRhythm(InteractionState i){return i.Kind=="Dance"||i.Kind=="Poi"||i.Kind=="Dj"||i.Kind=="ConfirmDance"||i.Kind==DeckSetKind||i.Kind==DrumPartKind||TrustDancing(i);}
    void Cancel(PlayerState p,string reason){var i=Interaction(p.InteractionId);if(i==null||i.Status!="Active"){p.InteractionId="";return;}i.Status=reason;RetractSpeech(p,i.Id);if(i.Kind=="Sale"&&i.ReservedItemId!=""){if(CanAdd(p,i.ReservedItemId,1))Add(p,i.ReservedItemId,1);else State.Drops.Add(new DropState{Id=Id("drop"),ItemId=i.ReservedItemId,Count=1,X=p.X,Z=p.Z});i.ReservedItemId="";}if(i.ReservedCash>0){p.Cash+=i.ReservedCash;i.ReservedCash=0;}if((IsRhythm(i)||Talks(i.Kind))&&reason=="Cancelled")foreach(var n in State.Npcs)if(i.WitnessIds.Contains(n.Id))Adjust(n,p,20);p.InteractionId="";}
    void FinishInteraction(InteractionState i,PlayerState p){if(Talks(i.Kind)){if(!TalkEnds(i,p))return;SettleTalk(i,p);}
    else if(i.Kind==DeckSetKind){FinishDeckSet(i,p);return;}
    else if(IsRhythm(i)){i.Score=Judge(i);p.LastRhythmScore=i.Score;p.Performances++;int delta=RhythmDelta(i.Score);if(i.Kind=="Poi"&&i.ReservedItemId=="poi_led"&&delta<0)delta=(int)Math.Round(delta*1.25);if(i.Kind=="Dance")JudgeGroupDance(i,p);else foreach(var n in State.Npcs)if(i.WitnessIds.Contains(n.Id))Adjust(n,p,delta);
        if(i.Kind=="Poi"){Distraction(p,8,i.Score>=.75?4:1,false);var paired=State.Interactions.Find(x=>x.PlayerId==i.PartnerId&&x.PartnerId==p.Id&&x.Kind=="Poi"&&Math.Abs(x.StartSeconds-i.StartSeconds)<.01&&x.Status=="Complete");var partner=Player(i.PartnerId);if(i.Score>=.75&&paired!=null&&paired.Score>=.75&&partner!=null&&partner.Connected&&partner.Life=="Alive"&&Near(p,partner.X,partner.Z,4))Distraction(p,8,5,true);}
        if(i.Kind=="Dj"&&i.Score>=.75)Distraction(p,120,5,true);
        if(i.Kind=="Dj"&&++i.Phrase<5){i.ChartSeed++;i.Inputs.Clear();i.StartSeconds+=i.DurationSeconds+.75;i.DurationSeconds=RhythmChart.For(i).DurationSeconds;return;}
    }else switch(i.Kind){
        case "FindFriend":Recruited(i,p);break;
        case "Extract":if(CanExtractNow(p))End("Success");break;
        case "LostProperty":if(i.TargetId==State.LostPropertyTask.ToString()){p.Cash+=5;State.LostPropertyTask++;}break;
        case "Medical":int treated=p.Effects.FindLastIndex(Treatable);if(Count(p,"medical_voucher")>0&&treated>=0){Take(p,"medical_voucher",1);p.Effects.RemoveAt(treated);}break;
        case "Rescue":var down=Player(i.TargetId);if(down!=null&&down.Life=="Downed"&&!AttackerNear(down,3)){down.Life="Alive";down.Health=40;down.RecoveryUntil=State.SimulationSeconds+5;down.DownedRemaining=0;}break;
        case "Release":var prisoner=Player(i.TargetId);if(prisoner!=null&&prisoner.Life=="Detained"){prisoner.Life="Alive";prisoner.X=24;prisoner.Z=2;prisoner.RecoveryUntil=State.SimulationSeconds+5;ClearEvidence(prisoner.Id);}break;
        case "Revival":var spirit=Player(i.TargetId);bool self=spirit==p&&State.Players.FindAll(x=>x.Connected).Count==1;if(spirit!=null&&spirit.Life=="Spirit"&&spirit.RevivalCount<2&&(self||p.Wristbands.Remove(spirit.Id))){spirit.Life="Alive";spirit.Health=40;spirit.RevivalCount++;spirit.Effects.Clear();spirit.X=24;spirit.Z=-20;spirit.RecoveryUntil=State.SimulationSeconds+5;}break;
    }
    // A narc's buy (SettleBuy) has already ended as a "Police bust".
    FinishConfirm(i);i.ReservedCash=0;if(i.Status=="Active")i.Status="Complete";p.InteractionId="";}
    // A rhythm challenge's score, with every note now due or missed.
    double Judge(InteractionState i){var chart=RhythmChart.For(i);var judge=ReplayRhythm(i,chart);judge.Advance(chart.DurationSeconds+.75);return judge.Score;}
    bool TaskStillValid(InteractionState i,PlayerState p){switch(i.Kind){case "FindFriend":var friend=FriendAt(i.TargetId);return Near(p,friend.X,friend.Z);case "Extract":return Near(p,Festivals.CampGateX,Festivals.CampGateZ);case "Medical":case "Revival":return Near(p,24,-20);case "LostProperty":return Near(p,-28,16);case "Release":return i.ReservedCash>0?Near(p,27,5):Near(p,-28,16);case "Rescue":var t=Player(i.TargetId);return t!=null&&t.Life=="Downed"&&Near(p,t.X,t.Z)&&!AttackerNear(t,3);case "Dj":return Near(p,Catalog.StageTakeoverX,Catalog.StageTakeoverZ);case DeckSetKind:return OwnsDeck(i,p);case "Dance":case "Conversation":case "Sale":case "Buy":case "Police":case "ConfirmChat":case "ConfirmDance":var n=State.Npcs.Find(x=>x.Id==i.TargetId);return n!=null&&Near(p,n.X,n.Z,4);default:return true;}}
    void Distraction(PlayerState p,double radius,double duration,bool strong){foreach(var n in State.Npcs)if(n.Kind=="Wook"&&Distance(p.X,p.Z,n.X,n.Z)<=radius&&(strong||n.Mode!="Swarming"&&n.Mode!="Accusing")){n.DistractedUntil=Math.Max(n.DistractedUntil,State.SimulationSeconds+duration);n.AttackAt=0;foreach(var o in n.Observers)o.Suspicion=Math.Max(0,o.Suspicion-10);}}
}
}
