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
        // A FindFriend interaction's target: the first friend's is "friend".
        const string SecondFriendTarget="friend_2";
        // Friends get lost at one of these, picked by the round's seed; a second friend at the next one along.
        static readonly WorldPoint[] FriendSpots={new WorldPoint(-24,25),new WorldPoint(25,24),new WorldPoint(18,5)};
        static WorldPoint FriendSpot(int seed,int friend){var at=FriendSpots[((seed&int.MaxValue)%FriendSpots.Length+friend)%FriendSpots.Length];return new WorldPoint(at.X,at.Z);}

        // As the crew leaves camp: a night with TwoFriendCrew or more connected loses the second friend too. Counted once, here,
        // so someone leaving mid-night does not call the search off. Returns how many trails DealRoles lays.
        int LoseFriends()
        {
            var second=State.SecondFriend;second.Active=Festivals.For(State).Night&&State.Players.FindAll(p=>p.Connected).Count>=TwoFriendCrew;
            if(second.Active)second.Position=FriendSpot(State.Seed,1);
            return second.Active?2:1;
        }
        List<string> Chain(int trail)=>trail==0?State.ClueChain:State.SecondFriend.ClueChain;
        // The id of the trail's next clue holder, or "" once it is followed to the end (or was never laid).
        string NextClue(int trail){var chain=Chain(trail);int read=trail==0?State.CluesRead:State.SecondFriend.CluesRead;return read<chain.Count?chain[read]:"";}
        // The tripper found trail's real next clue holder: that trail moves on and its next link's visions replace this link's.
        // After the last link the way to its friend opens (the tripper's view then shows where that friend is).
        void FollowTrail(int trail)
        {
            var second=State.SecondFriend;int read=trail==0?++State.CluesRead:++second.CluesRead;
            if(read==Chain(trail).Count){if(trail==0)State.GateOpened=true;else second.GateOpened=true;}
            // Each trail draws its fakes from its own seeds: the first trail's (and DealRoles') offsets stay at or under MaxChainLength+1.
            State.Visions.RemoveAll(v=>v.Kind=="Clue"&&v.Trail==trail);var random=new ContentRandom(unchecked(State.SpinSeed*7+1+read+(Festivals.MaxChainLength+1)*trail));
            Show(ClueVisions(random,TripperDose(State),trail,State.Visions),random);
        }

        // FindFriend goes to the nearer lost friend: once their trail is finished, anyone beside them recruits them or takes over the escort.
        CommandResult FindFriend(PlayerState p)
        {
            if(DayLevel)return Reject("Nobody is lost by day: sell the quota, then head back to camp");
            var second=State.SecondFriend;bool other=second.Active&&Distance(p.X,p.Z,second.Position.X,second.Position.Z)<Distance(p.X,p.Z,State.FriendPosition.X,State.FriendPosition.Z);
            if(!(other?second.GateOpened:State.GateOpened))return Reject("Follow the tripper's clue trail to its last link to find your friend");
            var at=other?second.Position:State.FriendPosition;if(!Near(p,at.X,at.Z))return Reject("Move closer to the missing friend");
            return BeginTask(p,"FindFriend",other?SecondFriendTarget:"friend",2);
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
        bool FriendsBack()=>Back(State.FriendFound,State.FriendPosition)&&(!State.SecondFriend.Active||Back(State.SecondFriend.Found,State.SecondFriend.Position));
        static bool Back(bool found,WorldPoint friend)=>found&&Distance(friend.X,friend.Z,Festivals.CampGateX,Festivals.CampGateZ)<=3;
        string NightExtractRefusal()=>!State.SecondFriend.Active?(Finale?FinaleExtractRefusal:"Bring the friend and a living survivor to the shuttle")
            :Finale?"Everyone goes home on Night 2: bring both friends, the whole crew and any bodies to the way back to camp":"Bring both lost friends and a living survivor to the way back to camp";
    }
}
