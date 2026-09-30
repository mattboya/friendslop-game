using System;
using System.Collections.Generic;

namespace Festival.Core
{
    /// <summary>A sight only the tripper gets: a role over a festivalgoer (Buyer, Narc), the next clue holder (Clue), or a secret:
    /// a cash Stash or a Shortcut at a spot, a DoubleBuyer over a buyer. Tell is presentation-only and true on fakes, so they can
    /// show their tells; IsTrue reaches the tripper only once the vision is Confirmed (VisibleVisions). Trail says which lost
    /// friend's trail a Clue belongs to (0 the first, 1 the second; SplitObjective.cs) and stays on the host.</summary>
    [Serializable] public sealed class VisionState { public string Id="",Kind="",NpcId=""; public float X,Z; public bool IsTrue,Tell,Confirmed; public int Trail; }

    /// <summary>TRIP-2: each level deals the crowd hidden roles, and the spinner's tripper sees partly false visions of them.</summary>
    public sealed partial class FestivalSimulation
    {
        // Share of true visions for doses 1-4. By day the marks go on at most MaxDayMarks festivalgoers, one each.
        static readonly double[] Reliability={.75,.50,.25,.10};
        const int MaxDayMarks=12;
        // About this share of the crowd buys; the level table sets the narcs and, at night, the clue holders.
        const double BuyerShare=.4;
        // Every sale pays x(1 + PayoutStep per dose above the first) for the tripper's dose; a double buyer pays twice that.
        const double PayoutStep=.25;
        // A regular or clue holder turns a sale down with this much suspicion of the seller, however many friends stand by them.
        const double RefusedSaleSuspicion=10;
        // A secret stash banks this much in the crew's shared stash, once, for whoever reaches it.
        const int SecretStashCash=15;
        // Secret stashes and shortcuts sit at fixed reachable spots: the old totem nooks and the far corners of the grounds.
        static readonly WorldPoint[] SecretSpots={new WorldPoint(16,-4),new WorldPoint(-16,5),new WorldPoint(-24,25),new WorldPoint(25,24),new WorldPoint(18,5)};

        /// <summary>This level's sale payout multiplier for the tripper's dose: x1, x1.25, x1.5 or x1.75.</summary>
        public static double PayoutMultiplier(RoundState s)=>1+PayoutStep*(TripperDose(s)-1);
        /// <summary>Visions reach only the tripper, as copies. A vision's truth leaves the host only once it is confirmed; its tell always does.</summary>
        public static List<VisionState> VisibleVisions(RoundState s,string viewer)=>s.TripperId==""||viewer!=s.TripperId?new List<VisionState>():
            s.Visions.ConvertAll(v=>new VisionState{Id=v.Id,Kind=v.Kind,NpcId=v.NpcId,X=v.X,Z=v.Z,IsTrue=v.Confirmed&&v.IsTrue,Tell=v.Tell,Confirmed=v.Confirmed});
        // The tripper's dose from the public spin result, 1-4; a round with no spin counts as dose 1.
        public static int TripperDose(RoundState s)=>Math.Min(Reliability.Length,Math.Max(1,s.Doses.Find(d=>d.PlayerId==s.TripperId)?.Dose??1));

        // As the crew leaves camp, once the spinners have picked the tripper: deal this level's roles, then show the visions.
        void DealRoles()
        {
            var level=Festivals.For(State);var random=new ContentRandom(unchecked(State.SpinSeed*7+1));
            var crowd=Shuffled(State.Npcs.FindAll(n=>n.Kind=="Wook"),random);int trails=LoseFriends();
            int narcs=Math.Min(level.Narcs,crowd.Count),chain=Math.Min(level.ChainLength,(crowd.Count-narcs)/trails),holders=chain*trails;
            int buyers=Buyers(crowd.Count,narcs+holders);
            State.ClueChain.Clear();State.SecondFriend.ClueChain.Clear();
            for(int i=0;i<crowd.Count;i++)
            {
                var n=crowd[i];n.Role=i<narcs?"Narc":i<narcs+holders?"ClueHolder":i<narcs+holders+buyers?"Buyer":"Regular";
                if(n.Role=="ClueHolder")Chain((i-narcs)/chain).Add(n.Id);
            }
            int dose=TripperDose(State);bool night=level.Night;State.Visions.Clear();
            var seen=night?ClueVisions(random,dose,0,State.Visions):DayMarks(random,dose);if(trails>1)seen.AddRange(ClueVisions(random,dose,1,seen));
            seen.AddRange(SecretSights(random,dose,night));Show(seen,random);
        }
        // About BuyerShare of the crowd buys, out of those not dealt as narcs or clue holders. A day's quota counts them (DaySupply).
        static int Buyers(int crowd,int others)=>Math.Min((int)Math.Round(BuyerShare*crowd,MidpointRounding.AwayFromZero),crowd-others);
        // Day: marks on up to MaxDayMarks festivalgoers, one each. The dose's share of them label real buyers and narcs truly; the
        // rest put a wrong label on someone else (a regular or a narc as a buyer, a buyer as a narc). From dose 2 one fake is always
        // a narc passing as a buyer, so trusting blindly gets someone busted. The marks stay put for the level.
        List<VisionState> DayMarks(ContentRandom random,int dose)
        {
            var crowd=Shuffled(State.Npcs.FindAll(n=>n.Kind=="Wook"),random);var marks=new List<VisionState>();
            int count=Math.Min(MaxDayMarks,crowd.Count),truths=Math.Min(count,Math.Max(1,(int)Math.Round(Reliability[dose-1]*count,MidpointRounding.AwayFromZero)));
            var decoy=dose>=2&&truths<count?crowd.Find(n=>n.Role=="Narc"):null;
            if(decoy!=null){crowd.Remove(decoy);marks.Add(Sight(decoy,"Buyer",false));}
            var honest=crowd.FindAll(n=>n.Role=="Buyer"||n.Role=="Narc");
            for(int i=0;i<truths&&i<honest.Count;i++){marks.Add(Sight(honest[i],honest[i].Role,true));crowd.Remove(honest[i]);}
            for(int i=0;marks.Count<count&&i<crowd.Count;i++)marks.Add(Sight(crowd[i],crowd[i].Role=="Buyer"?"Narc":"Buyer",false));
            return marks;
        }
        // Night: a trail's next clue holder, always shown, among round(1/r) - 1 fakes (0, 1, 3 or 9 for doses 1-4) on festivalgoers
        // who hold neither this trail's next clue nor any link still ahead on the other trail, and carry no clue vision already
        // shown. Keeping clear of the other trail's later links means that trail's next holder never already wears one of these fakes.
        List<VisionState> ClueVisions(ContentRandom random,int dose,int trail,List<VisionState> shown)
        {
            var seen=new List<VisionState>();var holder=State.Npcs.Find(n=>n.Id==NextClue(trail));
            if(holder==null)return seen;
            seen.Add(Sight(holder,"Clue",true));
            var others=Shuffled(State.Npcs.FindAll(n=>n.Kind=="Wook"&&n!=holder&&!StillOnTrail(1-trail,n.Id)&&!shown.Exists(v=>v.Kind=="Clue"&&v.NpcId==n.Id)),random);
            for(int i=0,fakes=(int)Math.Round(1/Reliability[dose-1])-1;i<fakes&&i<others.Count;i++)seen.Add(Sight(others[i],"Clue",false));
            foreach(var v in seen)v.Trail=trail;
            return seen;
        }
        // Doses 3 and 4 also see real secrets, looking like any true vision until found: one at dose 3, two at dose 4.
        // By day a cash stash or a buyer who pays double; by night a cash stash or a shortcut (presentation only for now).
        List<VisionState> SecretSights(ContentRandom random,int dose,bool night)
        {
            var kinds=Shuffled(new List<string>{"Stash",night?"Shortcut":"DoubleBuyer"},random);var spots=Shuffled(new List<WorldPoint>(SecretSpots),random);
            var buyers=State.Npcs.FindAll(n=>n.Role=="Buyer");var seen=new List<VisionState>();
            for(int i=0;i<dose-2;i++)
            {
                // ponytail: a crowd with no buyers (only in hand-built states) simply has no double buyer.
                if(kinds[i]!="DoubleBuyer")seen.Add(new VisionState{Kind=kinds[i],X=spots[i].X,Z=spots[i].Z,IsTrue=true});
                else if(buyers.Count>0)seen.Add(Sight(buyers[random.Next(buyers.Count)],"DoubleBuyer",true));
            }
            return seen;
        }
        static VisionState Sight(NpcState n,string kind,bool truth)=>new VisionState{Kind=kind,NpcId=n.Id,IsTrue=truth,Tell=!truth};
        // Shuffled, then numbered, so neither the order nor the ids say which visions are true.
        void Show(List<VisionState> seen,ContentRandom random){foreach(var v in Shuffled(seen,random)){v.Id=Id("vision");State.Visions.Add(v);}}
        static List<T> Shuffled<T>(List<T> items,ContentRandom random){for(int i=items.Count-1;i>0;i--){int j=random.Next(i+1);var t=items[i];items[i]=items[j];items[j]=t;}return items;}

        // TRIP-3's checks land here. Every vision about npc is confirmed, so its truth reaches the tripper. Finding a trail's real
        // next clue holder moves that trail on (FollowTrail): after its last link the way to its lost friend opens.
        internal void ConfirmVisionsOf(NpcState npc)
        {
            foreach(var v in State.Visions)if(v.NpcId==npc.Id)v.Confirmed=true;
            for(int trail=0;trail<2;trail++)if(NextClue(trail)==npc.Id)FollowTrail(trail);
        }

        // Selling: a buyer (or a festivalgoer not dealt a role yet) plays the sale out; a regular or a clue holder turns it down
        // with a little suspicion; a narc busts the seller on the spot: detained, stock confiscated.
        static bool Buys(NpcState n)=>n.Role==""||n.Role=="Buyer";
        CommandResult RefuseSale(PlayerState p,NpcState npc)
        {
            if(npc.Role=="Narc"){Detain(p);return Ok("Busted! That was an undercover narc.");}
            Adjust(npc,p,RefusedSaleSuspicion,packed:false);return Reject("Not buying. They give you a funny look.");
        }
        int SalePayout(NpcState buyer,int pay)=>(int)Math.Round(pay*PayoutMultiplier(State)*(buyer!=null&&State.Visions.Exists(v=>v.Kind=="DoubleBuyer"&&v.NpcId==buyer.Id)?2:1),MidpointRounding.AwayFromZero);
        // Whoever reaches a secret stash first banks it for the crew, and the tripper sees it confirmed.
        void FindStashes(){foreach(var v in State.Visions)if(v.Kind=="Stash"&&!v.Confirmed&&State.Players.Exists(p=>p.Connected&&p.Life=="Alive"&&Near(p,v.X,v.Z))){v.Confirmed=true;State.StashCash+=SecretStashCash;}}
    }
}
