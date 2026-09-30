using System;
using System.Collections.Generic;

namespace Festival.Core
{
    /// <summary>A crew member who died on Night 2 lies here until revived. The crew carries it back to camp.</summary>
    [Serializable] public sealed class BodyState { public string PlayerId=""; public float X,Z; }

    /// <summary>Night 2 finale: nobody is left behind. Bodies, carrying them, and who counts as home.</summary>
    public sealed partial class FestivalSimulation
    {
        // One carrier drags at 1.5 m/s, a pair carries at 1.2 m/s, and a carrier stays within 2 m of the body.
        public const double SoloCarrySpeed=1.5,PairCarrySpeed=1.2,CarryReach=2;
        const string FinaleExtractRefusal="Everyone goes home on Night 2: bring the friend, the whole crew and any bodies to the way back to camp";
        bool Finale=>State.LevelIndex==Festivals.LevelCount-1;
        BodyState Body(string playerId)=>State.Bodies.Find(b=>b.PlayerId==playerId);
        BodyState Carried(PlayerState p)=>p.Connected&&p.Life=="Alive"&&p.CarryBodyId!=""?Body(p.CarryBodyId):null;
        List<PlayerState> Carriers(BodyState b)=>State.Players.FindAll(p=>Carried(p)==b);
        // Bodies are kept in death order, so the first one is the earliest death still lying out there.
        // It moves with one carrier; every later body needs two. Public and static so the HUD's carry prompts read the same rule from a view.
        public static int CarrierCount(RoundState s,BodyState b)=>s.Players.FindAll(p=>p.Connected&&p.Life=="Alive"&&p.CarryBodyId==b.PlayerId).Count;
        public static bool BodyMoves(RoundState s,BodyState b){int carriers=CarrierCount(s,b);return carriers>=2||carriers==1&&s.Bodies.IndexOf(b)==0;}
        /// <summary>Top speed for p in m/s while carrying a body; infinite with empty hands. The session moves players at this cap too.</summary>
        public double CarrySpeed(PlayerState p){var b=Carried(p);return b==null?double.PositiveInfinity:Carriers(b).Count>1?PairCarrySpeed:SoloCarrySpeed;}
        /// <summary>Home: inside the camp gate alive or downed, or dead with the body carried inside. Detained is never home.</summary>
        public static bool Home(RoundState s,PlayerState p)
        {
            if(p.Life=="Spirit"){var b=s.Bodies.Find(x=>x.PlayerId==p.Id);return b!=null&&AtCampGate(b.X,b.Z);}
            return (p.Life=="Alive"||p.Life=="Downed")&&AtCampGate(p.X,p.Z);
        }
        static bool AtCampGate(float x,float z)=>Distance(x,z,Festivals.CampGateX,Festivals.CampGateZ)<=Festivals.CampGateRadius;
        bool EveryoneHome()=>State.Players.TrueForAll(p=>!p.Connected||Home(State,p));
        void LeaveBody(PlayerState p){if(Finale)State.Bodies.Add(new BodyState{PlayerId=p.Id,X=p.X,Z=p.Z});}
        // A body lies only while its owner is a spirit, so a revival lifts it. Carriers who fall, are detained, leave or lose the body let go.
        void BodiesTick(){State.Bodies.RemoveAll(b=>Player(b.PlayerId)?.Life!="Spirit");foreach(var p in State.Players)if(p.CarryBodyId!=""&&Carried(p)==null)p.CarryBodyId="";}
        // TryMove's last check as p steps to (x,z). False refuses the step: a carrier stays within reach of the body.
        // Otherwise a body that can move goes to its carriers' centre (a pair's midpoint, so it keeps pace with the slower one).
        bool CarryTo(PlayerState p,float x,float z)
        {
            var b=Carried(p);if(b==null)return true;
            var carriers=Carriers(b);float bx=b.X,bz=b.Z;
            if(BodyMoves(State,b)){bx=bz=0;foreach(var c in carriers){bx+=c==p?x:c.X;bz+=c==p?z:c.Z;}bx/=carriers.Count;bz/=carriers.Count;}
            if(Distance(x,z,bx,bz)>CarryReach)return false;
            b.X=bx;b.Z=bz;return true;
        }
        CommandResult CarryBody(PlayerState p,GameCommand c)
        {
            var b=Body(c.TargetId);
            if(b!=null&&p.CarryBodyId==b.PlayerId){p.CarryBodyId="";return Ok("Put the body down");}
            if(b==null||p.DragTargetId!=""||Distance(p.X,p.Z,b.X,b.Z)>CarryReach||Carriers(b).Count>=2)return Reject("Reach a body with free hands; two can carry one");
            p.CarryBodyId=b.PlayerId;return Ok(BodyMoves(State,b)?"Carrying them home":"Too heavy alone: someone has to take the other end");
        }
        CommandResult DropBody(PlayerState p){if(p.CarryBodyId=="")return Reject("You are not carrying anyone");p.CarryBodyId="";return Ok("Put the body down");}
    }
}
