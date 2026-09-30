using System;
namespace Festival.Core {
public sealed partial class FestivalSimulation {
    InteractionState NewInteraction(PlayerState p,string kind,string target,double duration){var i=new InteractionState{Id=Id("interaction"),PlayerId=p.Id,TargetId=target,Kind=kind,StartSeconds=State.SimulationSeconds,DurationSeconds=duration,ChartSeed=State.Seed+(int)State.EntitySequence};State.Interactions.Add(i);p.InteractionId=i.Id;return i;}
    CommandResult BeginTask(PlayerState p,string kind,string target,double duration){NewInteraction(p,kind,target,duration);return Ok();}
    CommandResult BeginChallenge(PlayerState p,GameCommand c,string kind){var npc=State.Npcs.Find(n=>n.Id==c.TargetId);if(npc==null||!Near(p,npc.X,npc.Z)||((kind=="Police")!=(npc.Kind=="Cop")))return Reject("Choose a nearby appropriate NPC");if(kind=="Conversation"&&!npc.CanTalk)return Reject("This person is keeping to themselves");if(kind=="Sale"&&(!Stock(c.ItemId)||Count(p,c.ItemId)<1||npc.Sales>=2))return Reject("Stock required; buyer accepts two sales per round");if(State.Interactions.Exists(x=>x.TargetId==npc.Id&&x.Status=="Active"))return Reject("NPC is busy");if(kind=="Sale"&&!Buys(npc))return RefuseSale(p,npc);var i=NewInteraction(p,kind,npc.Id,0);i.StartSeconds+=2;i.NoteCount=kind=="Conversation"&&npc.Suspicion>=70?16:8;i.DurationSeconds=RhythmChart.Create(i.ChartSeed,i.NoteCount,i.BeatSeconds).DurationSeconds;var reserved=new System.Collections.Generic.List<string>();foreach(var active in State.Interactions)if(active.Status=="Active"&&active.DialogueId!="")reserved.Add(active.DialogueId);var line=p.Dialogue.Select(kind=="Sale"?"sale":kind=="Police"?"police":kind=="Dance"?"dance":"suspicion",i.ChartSeed,reserved);i.DialogueId=line.Id;i.DialogueText=p.Effects.Count==0?line.Text:line.AwkwardText;foreach(var n in State.Npcs)if(n.Kind=="Wook"&&Sees(n,p))i.WitnessIds.Add(n.Id);if(kind=="Sale"){Take(p,c.ItemId,1);i.ReservedItemId=c.ItemId;foreach(var cop in State.Npcs)if(cop.Kind=="Cop"&&Sees(cop,p))RecordDeal(cop,p);}return Ok();}
    static readonly string[] FestivalBarks={
        "I came for one set and somehow found three sunrises.",
        "My friends said meet by the moon. There are four moons here.",
        "The bass sounds better when you stop looking for the stage.",
        "Someone traded me a glow stick for a life-changing sandwich.",
        "If you see a blue cooler, tell it I miss it.",
        "That shuttle driver knows everybody's name. How?",
        "I was saving this spot for a friend, but you're a friend now.",
        "The poi circle is friendly. Just mind the spinning lights.",
        "I think the left speaker is telling a different story.",
        "Did you see the sunrise mural behind the tents?"
    };
    CommandResult Talk(PlayerState p,GameCommand c)
    {
        var npc=State.Npcs.Find(n=>n.Id==c.TargetId);
        if(npc==null||npc.Kind!="Wook"||!npc.CanTalk||!Near(p,npc.X,npc.Z))return Reject("Find someone who wants to chat nearby");
        if(npc.Mode=="Swarming"||npc.Mode=="Accusing")return Reject("They're too agitated to chat");
        if(State.SimulationSeconds-npc.LastTalkSeconds<2)return Reject("Give them a moment");
        npc.LastTalkSeconds=State.SimulationSeconds;
        int hash=17;foreach(char ch in npc.Id)hash=unchecked(hash*31+ch);
        int index=(int)(((long)State.Seed*31+hash+npc.TalkCount*7)&0x7fffffff)%FestivalBarks.Length;
        npc.TalkCount++;
        p.NpcSpeaker="FESTIVALGOER";p.NpcSpeech=FestivalBarks[index];p.NpcSpeechUntil=State.SimulationSeconds+7;
        return Ok();
    }
    CommandResult BeginPoi(PlayerState p,GameCommand c){string item=c.ItemId==""?"poi_practice":c.ItemId;if((item!="poi_practice"&&item!="poi_led")||Count(p,item)<1)return Reject("Owned poi required");var i=NewInteraction(p,"Poi",p.Id,0);i.ReservedItemId=item;foreach(var n in State.Npcs)if(n.Kind=="Wook"&&Sees(n,p))i.WitnessIds.Add(n.Id);i.StartSeconds+=2;i.NoteCount=12;i.BeatSeconds=.4;i.GoodWindowSeconds=item=="poi_practice"?.1725:.15;i.DurationSeconds=RhythmChart.Create(i.ChartSeed,i.NoteCount,i.BeatSeconds).DurationSeconds;var partner=Player(c.TargetId);var other=partner==null?null:Interaction(partner.InteractionId);if(partner!=null&&partner!=p&&partner.Life=="Alive"&&Near(p,partner.X,partner.Z,4)&&other!=null&&other.Kind=="Poi"&&other.Status=="Active"&&other.PartnerId==""&&State.SimulationSeconds<other.StartSeconds){i.PartnerId=partner.Id;other.PartnerId=p.Id;i.StartSeconds=other.StartSeconds;i.ChartSeed=other.ChartSeed;i.DurationSeconds=other.DurationSeconds;}return Ok();}
    CommandResult BeginDj(PlayerState p,GameCommand c){if(!Near(p,Catalog.StageTakeoverX,Catalog.StageTakeoverZ,Catalog.StageTakeoverStartRange)||Count(p,"stage_pass")<1||State.Interactions.Exists(i=>i.Kind=="Dj"&&i.Status=="Active"))return Reject("Move up to the stage-front deck with a pass");Take(p,"stage_pass",1);p.X=Catalog.StageTakeoverX;p.Z=Catalog.StageTakeoverZ;p.Yaw=0;var i=NewInteraction(p,"Dj","stage",0);i.NoteCount=12;i.BeatSeconds=.4;i.StartSeconds+=2;i.DurationSeconds=RhythmChart.Create(i.ChartSeed,i.NoteCount,i.BeatSeconds).DurationSeconds;return Ok();}
    CommandResult Submit(PlayerState p,GameCommand c){var i=Interaction(p.InteractionId);if(i==null||i.Status!="Active"||!IsRhythm(i))return Reject("No active rhythm challenge");double now=State.SimulationSeconds-i.StartSeconds;if(c.Direction<0||c.Direction>3||c.TimeSeconds<0||c.TimeSeconds>i.DurationSeconds||c.TimeSeconds>now+.15||now-c.TimeSeconds>.75||i.Inputs.Count>=128)return Reject("Input outside supported timing envelope (connection issue)");i.Inputs.Add(new RhythmInput{Direction=c.Direction,TimeSeconds=c.TimeSeconds});if(i.Kind=="Dance"){p.VisualDanceStepDirection=c.Direction;p.VisualDanceStepSequence++;}return Ok();}
    static bool IsRhythm(InteractionState i){return i.Kind=="Dance"||i.Kind=="Conversation"||i.Kind=="Sale"||i.Kind=="Police"||i.Kind=="Poi"||i.Kind=="Dj";}
    void Cancel(PlayerState p,string reason){var i=Interaction(p.InteractionId);if(i==null||i.Status!="Active"){p.InteractionId="";return;}i.Status=reason;if(i.Kind=="Sale"&&i.ReservedItemId!=""){if(CanAdd(p,i.ReservedItemId,1))Add(p,i.ReservedItemId,1);else State.Drops.Add(new DropState{Id=Id("drop"),ItemId=i.ReservedItemId,Count=1,X=p.X,Z=p.Z});i.ReservedItemId="";}if(i.ReservedCash>0){p.Cash+=i.ReservedCash;i.ReservedCash=0;}if(IsRhythm(i)&&reason=="Cancelled")foreach(var n in State.Npcs)if(i.WitnessIds.Contains(n.Id))Adjust(n,p,20);p.InteractionId="";}
    void FinishInteraction(InteractionState i,PlayerState p){if(IsRhythm(i)){var chart=RhythmChart.Create(i.ChartSeed,i.NoteCount,i.BeatSeconds);var judge=new RhythmJudge(chart,i.GoodWindowSeconds);foreach(var input in i.Inputs)judge.Submit(input.Direction,input.TimeSeconds);judge.Advance(chart.DurationSeconds+.75);i.Score=judge.Score;p.LastRhythmScore=i.Score;p.Performances++;int delta=RhythmDelta(i.Score);if(i.Kind=="Poi"&&i.ReservedItemId=="poi_led"&&delta<0)delta=(int)Math.Round(delta*1.25);if(i.Kind=="Dance")JudgeGroupDance(i,p);else foreach(var n in State.Npcs)if(i.WitnessIds.Contains(n.Id))Adjust(n,p,delta);
        if(i.Kind=="Dance"&&i.Score>=.6&&State.CluesRead>=2)State.GateOpened=true;
        if(i.Kind=="Sale"){var npc=State.Npcs.Find(n=>n.Id==i.TargetId);if(i.Score>=.4){int payout=SalePayout(npc,i.Score>=.75?10:5);p.Cash+=payout;State.GrossSales+=payout;State.LevelSales+=payout;if(npc!=null)npc.Sales++;}else if(i.ReservedItemId!=""){if(CanAdd(p,i.ReservedItemId,1))Add(p,i.ReservedItemId,1);else State.Drops.Add(new DropState{Id=Id("drop"),ItemId=i.ReservedItemId,Count=1,X=p.X,Z=p.Z});}i.ReservedItemId="";}
        if(i.Kind=="Police"&&i.Score>=.75){var officer=State.Npcs.Find(n=>n.Id==i.TargetId);if(officer!=null)officer.Evidence.RemoveAll(e=>e.PlayerId==p.Id&&e.Kind=="VisibleStock");}
        if(i.Kind=="Poi"){Distraction(p,8,i.Score>=.75?4:1,false);var paired=State.Interactions.Find(x=>x.PlayerId==i.PartnerId&&x.PartnerId==p.Id&&x.Kind=="Poi"&&Math.Abs(x.StartSeconds-i.StartSeconds)<.01&&x.Status=="Complete");var partner=Player(i.PartnerId);if(i.Score>=.75&&paired!=null&&paired.Score>=.75&&partner!=null&&partner.Connected&&partner.Life=="Alive"&&Near(p,partner.X,partner.Z,4))Distraction(p,8,5,true);}
        if(i.Kind=="Dj"&&i.Score>=.75)Distraction(p,120,5,true);
        if(i.Kind=="Dj"&&++i.Phrase<5){i.ChartSeed++;i.Inputs.Clear();i.StartSeconds+=6;i.DurationSeconds=RhythmChart.Create(i.ChartSeed,i.NoteCount,i.BeatSeconds).DurationSeconds;return;}
    }else switch(i.Kind){
        case "ReadClue":if(i.TargetId==State.CluesRead.ToString())State.CluesRead++;break;
        case "FindFriend":if(!State.FriendFound){State.ObjectiveReward=20;State.StashCash+=20;}State.FriendFound=true;State.FriendLeaderId=p.Id;break;
        case "Extract":if(CanExtractNow(p))End("Success");break;
        case "LostProperty":if(i.TargetId==State.LostPropertyTask.ToString()){p.Cash+=5;State.LostPropertyTask++;}break;
        case "Medical":int treated=p.Effects.FindLastIndex(Treatable);if(Count(p,"medical_voucher")>0&&treated>=0){Take(p,"medical_voucher",1);p.Effects.RemoveAt(treated);}break;
        case "Rescue":var down=Player(i.TargetId);if(down!=null&&down.Life=="Downed"&&!AttackerNear(down,3)){down.Life="Alive";down.Health=40;down.RecoveryUntil=State.SimulationSeconds+5;down.DownedRemaining=0;}break;
        case "Release":var prisoner=Player(i.TargetId);if(prisoner!=null&&prisoner.Life=="Detained"){prisoner.Life="Alive";prisoner.X=24;prisoner.Z=2;prisoner.RecoveryUntil=State.SimulationSeconds+5;ClearEvidence(prisoner.Id);}break;
        case "Revival":var spirit=Player(i.TargetId);bool self=spirit==p&&State.Players.FindAll(x=>x.Connected).Count==1;if(spirit!=null&&spirit.Life=="Spirit"&&spirit.RevivalCount<2&&(self||p.Wristbands.Remove(spirit.Id))){spirit.Life="Alive";spirit.Health=40;spirit.RevivalCount++;spirit.Effects.Clear();spirit.X=24;spirit.Z=-20;spirit.RecoveryUntil=State.SimulationSeconds+5;}break;
    }
    i.ReservedCash=0;i.Status="Complete";p.InteractionId="";}
    bool TaskStillValid(InteractionState i,PlayerState p){switch(i.Kind){case "ReadClue":var point=CluePoint(State.Seed,State.CluesRead);return State.CluesRead<2&&i.TargetId==State.CluesRead.ToString()&&CanReadClues(p)&&Near(p,point.X,point.Z)&&(State.Players.FindAll(x=>x.Connected).Count==1||GroundingFriendNear(p,point));case "FindFriend":return Near(p,State.FriendPosition.X,State.FriendPosition.Z);case "Extract":return Near(p,Festivals.CampGateX,Festivals.CampGateZ);case "Medical":case "Revival":return Near(p,24,-20);case "LostProperty":return Near(p,-28,16);case "Release":return i.ReservedCash>0?Near(p,27,5):Near(p,-28,16);case "Rescue":var t=Player(i.TargetId);return t!=null&&t.Life=="Downed"&&Near(p,t.X,t.Z)&&!AttackerNear(t,3);case "Dj":return Near(p,Catalog.StageTakeoverX,Catalog.StageTakeoverZ);case "Dance":case "Conversation":case "Sale":case "Police":var n=State.Npcs.Find(x=>x.Id==i.TargetId);return n!=null&&Near(p,n.X,n.Z,4);default:return true;}}
    void Distraction(PlayerState p,double radius,double duration,bool strong){foreach(var n in State.Npcs)if(n.Kind=="Wook"&&Distance(p.X,p.Z,n.X,n.Z)<=radius&&(strong||n.Mode!="Swarming"&&n.Mode!="Accusing")){n.DistractedUntil=Math.Max(n.DistractedUntil,State.SimulationSeconds+duration);n.AttackAt=0;foreach(var o in n.Observers)o.Suspicion=Math.Max(0,o.Suspicion-10);}}
}
}
