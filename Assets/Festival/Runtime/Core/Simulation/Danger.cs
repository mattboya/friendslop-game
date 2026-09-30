using System;
using System.Collections.Generic;
namespace Festival.Core {
public sealed partial class FestivalSimulation {
    public Func<float,float,float,float,double,WorldPoint> Navigate;
    public void Tick(double deltaSeconds){if(!Finite(deltaSeconds)||deltaSeconds<0||deltaSeconds>60)throw new ArgumentOutOfRangeException(nameof(deltaSeconds));while(deltaSeconds>0){double dt=Math.Min(.1,deltaSeconds);Step(dt);deltaSeconds-=dt;}}
    void Step(double dt){State.SimulationSeconds+=dt;State.Tick++;
        foreach(var offer in State.Transfers.ToArray())if(offer.ExpiresAt<=State.SimulationSeconds)ReturnOffer(offer);
        if(State.Phase=="Shopping")
        {
            var connected=State.Players.FindAll(p=>p.Connected);
            if(connected.Count<1||connected.Exists(p=>!p.Ready))State.LaunchAtSeconds=0;
            else if(State.LaunchAtSeconds<=0)State.LaunchAtSeconds=State.SimulationSeconds+5;
            else if(State.SimulationSeconds>=State.LaunchAtSeconds)StartRound(connected);
            return;
        }
        if(State.Phase=="Spinning"&&State.SimulationSeconds>=State.SpinEndsAt)State.Phase="Loading";
        if(State.Phase!="Playing")return;State.ElapsedSeconds+=dt;if(EndIfLevelOver())return;KeepTripper();
        foreach(var p in State.Players){foreach(var e in p.Effects.ToArray()){e.RemainingSeconds-=dt;if(e.RemainingSeconds<=0)p.Effects.Remove(e);}if(p.Life=="Downed"){p.DownedRemaining-=dt;if(p.DownedRemaining<=0)Die(p);}if(p.DragTargetId!=""){var target=Player(p.DragTargetId);if(p.Life!="Alive"||target==null||target.Life!="Downed")p.DragTargetId="";}}
        // Evidence/arrests precede all financial settlement within a simulation tick.
        RideArtCars();foreach(var n in State.Npcs)if(n.Kind=="Cop")PoliceTick(n,dt);else WookTick(n,dt);Film(dt);
        foreach(var i in State.Interactions.ToArray())if(i.Status=="Active"){var p=Player(i.PlayerId);if(p==null||!p.Connected||(p.Life!="Alive"&&!(i.Kind=="Revival"&&p.Life=="Spirit"&&i.TargetId==p.Id))){if(p!=null)Cancel(p,"Interrupted");continue;}if(!TaskStillValid(i,p)){Cancel(p,"Moved away or target changed");continue;}if(State.SimulationSeconds>=i.StartSeconds+i.DurationSeconds+(IsRhythm(i)?.75:0))FinishInteraction(i,p);}
        Escort(State.FriendFound,State.FriendPosition,ref State.FriendLeaderId,dt);Escort(State.SecondFriend.Found,State.SecondFriend.Position,ref State.SecondFriend.LeaderId,dt);
        FindStashes();BodiesTick();if(State.Phase=="Playing")EndIfLevelOver();
    }
    // PLAYA-1: a dust storm cuts a festivalgoer's sight, and they never see an art car's rider (FestivalTwists.cs).
    bool Sees(NpcState n,PlayerState p){double dist=Distance(n.X,n.Z,p.X,p.Z);if(dist>SightRange(n)||p.Life!="Alive"||Hidden(n,p))return false;if(dist>.1){double angle=n.Yaw*Math.PI/180;double dot=(Math.Sin(angle)*(p.X-n.X)+Math.Cos(angle)*(p.Z-n.Z))/dist;if(dot<.5)return false;}return HasLineOfSight==null||HasLineOfSight(n.X,n.Z,p.X,p.Z);}
    ObserverState Observe(NpcState n,PlayerState p){var o=n.Observers.Find(x=>x.PlayerId==p.Id);if(o==null){o=new ObserverState{PlayerId=p.Id};n.Observers.Add(o);}return o;}
    // Little Spoon has no active use. Its modest social benefit stays in host
    // rules and is intentionally absent from the public item description.
    static double WookTrustFactor(PlayerState p)=>Count(p,"little_spoon")>0 ? .9 : 1;
    void Adjust(NpcState n,PlayerState p,double delta){var o=Observe(n,p);o.Suspicion=Math.Max(0,Math.Min(100,o.Suspicion+(delta>0?delta*WookTrustFactor(p)*PackFactor(p):delta)));o.LastSeenSeconds=State.SimulationSeconds;}
    // ESC-1: passive gains (sprinting in view, standing backstage) scale by the level's suspicion multiplier; cooling off does not.
    // CROWD-1: they also scale by the seen player's pack, the two together capped at MaxCrowdHeat.
    // POLO-1: an influencer's frame is a passive gain too (FestivalTwists.cs).
    double PassiveGain(PlayerState p,double dt,double heat)=>dt*WookTrustFactor(p)*Math.Min(MaxCrowdHeat,heat*PackFactor(p));
    void WookTick(NpcState n,double dt){if(n.DistractedUntil>State.SimulationSeconds){n.Mode="Distracted";n.AttackAt=0;return;}double heat=Festivals.For(State).SuspicionMultiplier;ObserverState highest=null;foreach(var p in State.Players){if(!p.Connected||(p.Life!="Alive"&&p.Life!="Downed"))continue;var o=Observe(n,p);bool seen=Sees(n,p);if(seen){o.LastSeenSeconds=State.SimulationSeconds;double gain=PassiveGain(p,dt,heat);if(p.SprintUntil>State.SimulationSeconds)o.Suspicion=Math.Min(100,o.Suspicion+4*gain);if(p.Z>30)o.Suspicion=Math.Min(100,o.Suspicion+8*gain);}else if(State.SimulationSeconds-o.LastSeenSeconds>5&&o.Suspicion<70)o.Suspicion=Math.Max(0,o.Suspicion-2*dt);if(highest==null||o.Suspicion>highest.Suspicion)highest=o;}
        if(highest==null){n.Mode="Blending";n.TargetId="";n.Suspicion=0;return;}n.Suspicion=highest.Suspicion;n.TargetId=highest.PlayerId;var target=Player(n.TargetId);if(highest.Suspicion<70)highest.AccusationSeconds=-1;
        if(highest.Suspicion>=70&&highest.AccusationSeconds<0){highest.AccusationSeconds=State.SimulationSeconds;foreach(var other in State.Npcs)if(other!=n&&other.Kind=="Wook"&&Distance(n.X,n.Z,other.X,other.Z)<=10){var heard=Observe(other,target);heard.Suspicion=Math.Max(heard.Suspicion,70);if(heard.AccusationSeconds<0)heard.AccusationSeconds=State.SimulationSeconds;}}
        bool swarm=highest.Suspicion>=70&&((highest.Suspicion>=90&&State.SimulationSeconds-highest.AccusationSeconds>=4)||State.SimulationSeconds-highest.AccusationSeconds>=8);
        n.Mode=swarm?"Swarming":highest.Suspicion>=70?"Accusing":highest.Suspicion>=45?"Questioning":highest.Suspicion>=25?"Watching":"Blending";
        if((swarm||n.Mode=="Questioning"||n.Mode=="Accusing")&&target!=null){double dist=Distance(n.X,n.Z,target.X,target.Z);if(dist>1.4){var point=Move(n.X,n.Z,target.X,target.Z,(swarm?3.6:1.7)*dt);n.X=point.X;n.Z=point.Z;}n.Yaw=(float)(Math.Atan2(target.X-n.X,target.Z-n.Z)*180/Math.PI);
            if(swarm&&target.Life=="Alive"&&target.RecoveryUntil<=State.SimulationSeconds&&dist<=1.5&&ActiveAttacker(n,target)){if(n.AttackAt==0&&State.SimulationSeconds>=n.AttackCooldownUntil)n.AttackAt=State.SimulationSeconds+.75;if(n.AttackAt>0&&State.SimulationSeconds>=n.AttackAt){if(HasLineOfSight==null||HasLineOfSight(n.X,n.Z,target.X,target.Z)){target.Health=Math.Max(0,target.Health-20);if(target.Health==0){Cancel(target,"Downed");ReturnOffers(target.Id);target.Life="Downed";target.DownedRemaining=35;}}n.AttackAt=0;n.AttackCooldownUntil=State.SimulationSeconds+2;}}else n.AttackAt=0;
            if(dist>16){highest.Suspicion=Math.Max(0,highest.Suspicion-8*dt);if(highest.Suspicion<70)highest.AccusationSeconds=-1;}
        }
    }
    bool ActiveAttacker(NpcState n,PlayerState target){int rank=0;foreach(var other in State.Npcs){if(other.Kind=="Wook"&&other.TargetId==target.Id&&other.Mode=="Swarming"&&other.DistractedUntil<=State.SimulationSeconds&&Distance(other.X,other.Z,target.X,target.Z)<=1.5){if(other==n)return rank<6;rank++;}}return false;}
    WorldPoint Move(float x,float z,float tx,float tz,double step){if(Navigate!=null){var result=Navigate(x,z,tx,tz,step);if(result!=null&&Finite(result.X)&&Finite(result.Z)&&Distance(x,z,result.X,result.Z)<=step+.05)return result;return new WorldPoint(x,z);}double d=Distance(x,z,tx,tz);if(d<=step)return new WorldPoint(tx,tz);return new WorldPoint(x+(float)((tx-x)*step/d),z+(float)((tz-z)*step/d));}
    void RecordDeal(NpcState cop,PlayerState p){var e=cop.Evidence.Find(x=>x.PlayerId==p.Id);if(e==null){e=new EvidenceState{PlayerId=p.Id};cop.Evidence.Add(e);}e.Kind="WitnessedDeal";e.DetainAt=State.SimulationSeconds+4;}
    void PoliceTick(NpcState cop,double dt){foreach(var p in State.Players){if(!p.Connected||p.Life!="Alive")continue;var e=cop.Evidence.Find(x=>x.PlayerId==p.Id);if(e==null&&Count(p,"merch_bag")==0&&p.Inventory.Exists(i=>Stock(i.ItemId))&&Sees(cop,p)){e=new EvidenceState{PlayerId=p.Id,Kind="VisibleStock"};cop.Evidence.Add(e);}if(e==null)continue;cop.TargetId=p.Id;cop.Mode=e.Kind=="WitnessedDeal"?"ArrestWarning":"Stop";cop.Yaw=(float)(Math.Atan2(p.X-cop.X,p.Z-cop.Z)*180/Math.PI);if(e.Kind=="WitnessedDeal"&&State.SimulationSeconds>=e.DetainAt){Detain(p);cop.Mode="Patrol";return;}if(Distance(cop.X,cop.Z,p.X,p.Z)>2){var point=Move(cop.X,cop.Z,p.X,p.Z,3*dt);cop.X=point.X;cop.Z=point.Z;}return;}Patrol(cop,dt);}
    void Detain(PlayerState p){if(p.Life!="Alive")return;var i=Interaction(p.InteractionId);if(i!=null&&i.Kind=="Sale")i.ReservedItemId="";Cancel(p,"Police bust");ReturnOffers(p.Id);p.Inventory.RemoveAll(x=>Stock(x.ItemId));if(!p.Inventory.Exists(x=>x.ItemId==p.EquippedItemId))p.EquippedItemId="";p.Cash-=Math.Min(p.Cash,10);p.Life="Detained";p.EscapeProgress=0;p.X=27;p.Z=5;p.DragTargetId="";}
    void ClearEvidence(string id){foreach(var n in State.Npcs)n.Evidence.RemoveAll(e=>e.PlayerId==id);}
    void Die(PlayerState p){if(p.Life!="Downed")return;Cancel(p,"Death");ReturnOffers(p.Id);foreach(var stack in p.Inventory)State.Drops.Add(new DropState{Id=Id("drop"),ItemId=stack.ItemId,Count=stack.Count,X=p.X,Z=p.Z});p.Inventory.Clear();p.EquippedItemId="";foreach(var band in p.Wristbands)State.Drops.Add(new DropState{Id=Id("band"),ItemId="wristband",OwnerId=band,Count=1,X=p.X,Z=p.Z});p.Wristbands.Clear();p.Cash=0;p.Effects.Clear();p.Life="Spirit";LeaveBody(p);ClearEvidence(p.Id);if(!State.Drops.Exists(d=>d.ItemId=="wristband"&&d.OwnerId==p.Id)&&!State.Players.Exists(x=>x.Wristbands.Contains(p.Id)))State.Drops.Add(new DropState{Id=Id("band"),ItemId="wristband",OwnerId=p.Id,Count=1,X=p.X,Z=p.Z});}
    bool AttackerNear(PlayerState p,double range){return State.Npcs.Exists(n=>n.Kind=="Wook"&&n.Mode=="Swarming"&&n.DistractedUntil<=State.SimulationSeconds&&Distance(n.X,n.Z,p.X,p.Z)<=range);}
    CommandResult Drag(PlayerState p,GameCommand c){var t=Player(c.TargetId);if(t==null||t.Life!="Downed"||p.CarryBodyId!=""||!Near(p,t.X,t.Z)||State.Players.Exists(x=>x!=p&&x.DragTargetId==t.Id))return Reject("Choose nearby unclaimed downed friend");p.DragTargetId=p.DragTargetId==t.Id?"":t.Id;return Ok();}
    CommandResult Rescue(PlayerState p,GameCommand c){var t=Player(c.TargetId);if(t==null||t.Life!="Downed"||!Near(p,t.X,t.Z)||AttackerNear(t,3)||RecoveryClaimed(t.Id))return Reject("Distract attackers and reach downed friend");return BeginTask(p,"Rescue",t.Id,3);}
    bool RecoveryClaimed(string id){return State.Interactions.Exists(i=>i.Status=="Active"&&i.TargetId==id&&(i.Kind=="Rescue"||i.Kind=="Revival"||i.Kind=="Release"));}
    CommandResult Release(PlayerState p,GameCommand c){var t=Player(c.TargetId);bool paid=c.Amount==1;if(t==null||t.Life!="Detained"||RecoveryClaimed(t.Id)||!Near(p,paid?27:-28,paid?5:16)||(paid&&p.Cash<10))return Reject("Release: $10 at holding desk or free task at lost property");var i=NewInteraction(p,"Release",t.Id,paid?3:10);if(paid){p.Cash-=10;i.ReservedCash=10;}return Ok();}
    CommandResult Revive(PlayerState p,GameCommand c){var t=Player(c.TargetId);bool paid=c.Amount==1;bool self=t==p&&State.Players.FindAll(x=>x.Connected).Count==1;if(State.Phase!="Playing"||t==null||t.Life!="Spirit"||t.RevivalCount>=2||(!self&&!p.Wristbands.Contains(t.Id))||!Near(p,24,-20)||RecoveryClaimed(t.Id)||(paid&&p.Cash<10)||p.Life!=(self?"Spirit":"Alive"))return Reject("Bring a wristband to medical; a lone spirit can complete a longer self-revival task");var i=NewInteraction(p,"Revival",t.Id,self?15:paid?3:10);if(paid){p.Cash-=10;i.ReservedCash=10;}return Ok();}
    void End(string result){if(State.Phase=="Results")return;State.Phase="Results";State.Result=result;if(result=="Success"&&!State.RewardCommitted){State.RewardCommitted=true;State.Survivors=State.Players.FindAll(p=>p.Connected&&p.Life=="Alive"&&Near(p,Festivals.CampGateX,Festivals.CampGateZ,Festivals.CampGateRadius)).Count;State.SurvivorBonus=Math.Max(0,State.Survivors-1)*10;State.StashCash+=State.SurvivorBonus;foreach(var p in State.Players)p.HasCosmetic=true;UnlockNextFestival(State);}foreach(var p in State.Players){Cancel(p,"Round ended");p.Badge="";}foreach(var t in State.Transfers.ToArray())ReturnOffer(t);}
}
}
