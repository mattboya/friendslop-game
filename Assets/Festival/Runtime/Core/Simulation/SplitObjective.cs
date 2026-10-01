using System;
using System.Collections.Generic;

namespace Festival.Core
{
    /// <summary>CROWD-2: the second lost friend of a big crew's night, with its own clue trail. Inactive on every other level.</summary>
    [Serializable] public sealed class LostFriendState { public bool Active,GateOpened,Found; public int CluesRead; public string LeaderId=""; public WorldPoint Position=new WorldPoint(); public List<string> ClueChain=new List<string>(); }

    /// <summary>CROWD-2: a crew of TwoFriendCrew or more at the start of a night looks for two lost friends, each at the end of
    /// its own clue trail of the night's length, lost at different spots. The night is won only with both back at camp.
    /// Trail 0 is the first friend (RoundState's Friend*, ClueChain, CluesRead and GateOpened), trail 1 the SecondFriend.</summary>
    public sealed partial class FestivalSimulation
    {
        public const int TwoFriendCrew=5;
        // Each friend rescued banks this in the crew's shared stash, once.
        const int FriendReward=20;
        // A clue the tripper finds says TRUE this long before it goes.
        const double FoundClueSeconds=3;
        // A FindFriend interaction's target: the first friend's is "friend".
        const string SecondFriendTarget="friend_2";
        // Friends get lost at one of these: the first at the one the round's seed and the level's DealSeed pick, a second friend at
        // either other one, 1 or 2 spots along from the first (TRIP-10: drawn from its own DealSeed-mixed stream, so the first no
        // longer gives it away).
        static readonly WorldPoint[] FriendSpots={new WorldPoint(-24,25),new WorldPoint(25,24),new WorldPoint(18,5)};
        static WorldPoint FriendSpot(int seed,int along){var at=FriendSpots[((seed&int.MaxValue)%FriendSpots.Length+along)%FriendSpots.Length];return new WorldPoint(at.X,at.Z);}

        // As the crew leaves camp: the friend is lost at the spot the round's seed and the host's DealSeed pick (TRIP-7; with no
        // secret it is CreateRound's spot). A night with TwoFriendCrew or more connected loses the second friend too, counted once,
        // here, so someone leaving mid-night does not call the search off: at either other spot, so finding the first leaves a
        // guess. Returns how many trails DealRoles lays.
        int LoseFriends()
        {
            var second=State.SecondFriend;second.Active=Festivals.For(State).Night&&State.Players.FindAll(p=>p.Connected).Count>=TwoFriendCrew;
            int seed=State.Seed^State.DealSeed;State.FriendPosition=FriendSpot(seed,0);
            if(second.Active){var random=new ContentRandom(unchecked((State.Seed*47+13)^State.DealSeed));random.Next(2);second.Position=FriendSpot(seed,1+random.Next(2));}
            return second.Active?2:1;
        }
        List<string> Chain(int trail)=>trail==0?State.ClueChain:State.SecondFriend.ClueChain;
        int CluesRead(int trail)=>trail==0?State.CluesRead:State.SecondFriend.CluesRead;
        // The id of the trail's next clue holder, or "" once it is followed to the end (or was never laid).
        string NextClue(int trail){var chain=Chain(trail);int read=CluesRead(trail);return read<chain.Count?chain[read]:"";}
        // Whether npc holds one of the trail's links still to find, its next one included.
        bool StillOnTrail(int trail,string npc)=>Chain(trail).IndexOf(npc)>=CluesRead(trail);
        // The tripper found trail's real next clue holder: that trail moves on and its next link's visions replace this link's,
        // but for the clue just found, which says TRUE for FoundClueSeconds first (VISION-3). After the last link the way to its
        // friend opens (the tripper's view then shows where that friend is).
        void FollowTrail(int trail)
        {
            var second=State.SecondFriend;int read=trail==0?++State.CluesRead:++second.CluesRead;
            if(read==Chain(trail).Count){if(trail==0)State.GateOpened=true;else second.GateOpened=true;}
            // The link's one true clue is the one just found; an earlier link's, still saying TRUE, keeps its own time.
            foreach(var v in State.Visions)if(v.Kind=="Clue"&&v.Trail==trail&&v.IsTrue&&v.ShowUntil==0)v.ShowUntil=State.SimulationSeconds+FoundClueSeconds;
            // Each trail draws its fakes from its own seeds, mixed with the DealSeed as DealRoles' is: the first trail's (and DealRoles')
            // offsets stay at or under MaxChainLength+1.
            State.Visions.RemoveAll(v=>v.Kind=="Clue"&&v.Trail==trail&&v.ShowUntil==0);var random=new ContentRandom(unchecked((State.SpinSeed*7+1+read+(Festivals.MaxChainLength+1)*trail)^State.DealSeed));
            Show(ClueVisions(random,TripperDose(State),trail,State.Visions),random);
        }

        // FindFriend goes to FriendToFind's friend: once their trail is finished, anyone beside them recruits them or takes over the escort.
        CommandResult FindFriend(PlayerState p)
        {
            if(DayLevel)return Reject("Nobody is lost by day: sell the quota, then head back to camp");
            string target=FriendToFind(State,p);if(target!="")return BeginTask(p,"FindFriend",target,2);
            // Nobody to recruit or take over in reach: the nearer friend says why.
            var second=State.SecondFriend;bool other=second.Active&&Distance(p.X,p.Z,second.Position.X,second.Position.Z)<Distance(p.X,p.Z,State.FriendPosition.X,State.FriendPosition.Z);
            if(!(other?second.GateOpened:State.GateOpened))return Reject("Follow the tripper's clue trail to its last link to find your friend");
            var at=other?second.Position:State.FriendPosition;return Reject(Near(p,at.X,at.Z)?"You are already escorting this friend":"Move closer to the missing friend");
        }
        /// <summary>The lost friend p's FindFriend goes to, "friend" or "friend_2", or "" with none in reach: within 2.5 m, trail
        /// finished and not already following p; one still lost before one already escorted, then the nearer. The HUD asks with
        /// the client's view, so it offers the same friend; there a friend out of the viewer's sight sits at the origin.</summary>
        public static string FriendToFind(RoundState s,PlayerState p)
        {
            var second=s.SecondFriend;
            bool first=InReach(s.GateOpened,s.FriendFound,s.FriendLeaderId,s.FriendPosition),other=second.Active&&InReach(second.GateOpened,second.Found,second.LeaderId,second.Position);
            if(first&&other)other=s.FriendFound!=second.Found?!second.Found:Distance(p.X,p.Z,second.Position.X,second.Position.Z)<Distance(p.X,p.Z,s.FriendPosition.X,s.FriendPosition.Z);
            return other?SecondFriendTarget:first?"friend":"";
            bool InReach(bool trailDone,bool found,string leader,WorldPoint at)=>trailDone&&!(found&&leader==p.Id)&&(at.X!=0||at.Z!=0)&&Near(p,at.X,at.Z);
        }
        WorldPoint FriendAt(string target)=>target==SecondFriendTarget?State.SecondFriend.Position:State.FriendPosition;
        // A recruit lands: each friend's first rescue banks FriendReward, and the friend follows whoever recruited them last.
        void Recruited(InteractionState i,PlayerState p)
        {
            var second=State.SecondFriend;bool other=i.TargetId==SecondFriendTarget;
            if(!(other?second.Found:State.FriendFound)){State.ObjectiveReward+=FriendReward;State.StashCash+=FriendReward;}
            if(other){second.Found=true;second.LeaderId=p.Id;}else{State.FriendFound=true;State.FriendLeaderId=p.Id;}
        }
        // Every Playing step: a found friend walks after their escort at 4 m/s until 1.5 m away. An escort who falls, leaves or is caught lets go.
        void Escort(bool found,WorldPoint friend,ref string leaderId,double dt)
        {
            var leader=Player(leaderId);if(!found||leader==null||!leader.Connected||leader.Life!="Alive"){leaderId="";return;}
            if(Distance(friend.X,friend.Z,leader.X,leader.Z)>1.5){var point=Move(friend.X,friend.Z,leader.X,leader.Z,4*dt);friend.X=point.X;friend.Z=point.Z;}
        }
        // A night's extract needs every lost friend found and within 3 m of the way back to camp.
        static bool FriendsBack(RoundState s)=>Back(s.FriendFound,s.FriendPosition)&&(!s.SecondFriend.Active||Back(s.SecondFriend.Found,s.SecondFriend.Position));
        static bool Back(bool found,WorldPoint friend)=>found&&Distance(friend.X,friend.Z,Festivals.CampGateX,Festivals.CampGateZ)<=3;
        string NightExtractRefusal()=>!State.SecondFriend.Active?(Finale?FinaleExtractRefusal:"Bring the friend and a living survivor to the way back to camp")
            :Finale?"Everyone goes home on Night 2: bring both friends, the whole crew and any bodies to the way back to camp":"Bring both lost friends and a living survivor to the way back to camp";
    }
}
