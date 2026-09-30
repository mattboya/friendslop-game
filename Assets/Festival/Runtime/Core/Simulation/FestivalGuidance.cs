using System;

namespace Festival.Core
{
    /// <summary>Player-facing directions derived only from the viewer's filtered round state.</summary>
    public static class FestivalGuidance
    {
        public static string Headline(RoundState state, PlayerState player)
        {
            if(state==null||player==null)return "";
            if(state.Phase=="Shopping")return "GATHER AT CAMP • BUY GEAR • READY UP";
            if(state.Phase=="CampReview")return "CAMP DEBRIEF • VOTE BEFORE SHOPPING";
            if(state.Phase=="Spinning")return "SPINNING FOR THE TRIPPER";
            if(state.Phase=="Loading")return "HEADING TO THE FESTIVAL";
            if(state.Phase=="Results")return state.Result=="Success"?"FRIEND RESCUED • BACK TO CAMP":"ROUND OVER • BACK TO CAMP";
            if(player.Life=="Downed")return "DOWNED • CALL FOR HELP";
            if(player.Life=="Detained")return "DETAINED • ESCAPE OR GET RELEASED";
            if(player.Life=="Spirit")return "SPIRIT • REACH THE MEDICAL TENT";
            if(state.FriendFound)return "ESCORT FRIEND TO SHUTTLE";
            if(state.GateOpened)return "FIND THE MISSING FRIEND";
            int trail=Festivals.For(state).ChainLength;
            return trail>0?"FOLLOW THE CLUE TRAIL "+state.CluesRead+" / "+trail:"SELL THE DAY'S QUOTA";
        }

        public static string Hint(RoundState state, PlayerState player)
        {
            if(state==null||player==null)return "";
            if(state.Phase=="Shopping")
            {
                if(player.Ready)
                {
                    bool allReady=state.Players.FindAll(p=>p.Connected).TrueForAll(p=>p.Ready);
                    return allReady
                        ?"Everyone is dancing. Festival opens in "+Math.Max(0,Math.Ceiling(state.LaunchAtSeconds-state.SimulationSeconds)).ToString("0")+" seconds. E to unready."
                        :"Dancing at the trailhead. Wait for the crew, or press E to unready.";
                }
                if(player.HeldOfferId!="")return "Carry your unpaid item to the seller "+Route(player,0,7)+". Press E to pay, or G to return it.";
                return Distance(player,0,19)<4
                    ?"Press E at the lit trailhead to ready up and dance."
                    :"Camp supplies "+Route(player,0,8)+": look at shelf props and press E. Ready at lit trailhead "+Route(player,0,19)+".";
            }
            if(state.Phase=="Spinning")return "The wheels pick who trips this level and how many doses they take.";
            if(state.Phase=="Loading")return "Waiting for the crew to enter the festival.";
            if(state.Phase=="CampReview")return "Press 1, 2 or 3 to pick a friend for each award. Then the host opens the camp shop.";
            if(state.Phase=="Results")return player.Id==state.HostPlayerId
                ?"Open Escape menu and choose NEXT ROUND to return to camp."
                :"Waiting for the host to start the next camp round.";
            if(player.Life=="Downed")return "Press E to distract attackers; a teammate can rescue or drag you.";
            if(player.Life=="Detained")return "Press E to work on escape, or ask a teammate for release.";
            if(player.Life=="Spirit")return "Medical tent "+Route(player,24,-20)+(CrewCount(state)==1?". Press E there for a 15-second self-revival task.":". Ask a teammate with your wristband to revive you.");
            if(state.FriendFound)
            {
                var leader=state.Players.Find(p=>p.Id==state.FriendLeaderId&&p.Connected&&p.Life=="Alive");
                if(leader==null)return "Friend needs an escort. Reach them and press E; shuttle "+Route(player,0,-32)+".";
                return leader.Id==player.Id
                    ?"Lead your friend to the shuttle "+Route(player,0,-32)+". Stay close."
                    :"Follow the escort to the shuttle "+Route(player,0,-32)+".";
            }
            if(state.GateOpened)
            {
                var friend=state.FriendPosition;
                return friend!=null&&(friend.X!=0||friend.Z!=0)
                    ?"Friend spotted "+Route(player,friend.X,friend.Z)+". Reach them and press E."
                    :"Search the north and side paths. The friend appears when nearby.";
            }
            // Only the tripper sees the visions (buyers and narcs by day, the clue trail by night); the game's one hint is theirs.
            bool night=Festivals.For(state).Night;
            if(player.Id==state.TripperId)return (night?"Your visions mark the next clue holder.":"Your visions mark buyers and narcs.")+" Trust, but verify.";
            var tripper=state.Players.Find(p=>p.Id==state.TripperId);
            return "Stick with "+(tripper?.Name??"the tripper")+": only the tripper can see "+(night?"the clue trail.":"who is buying.");
        }

        private static double Distance(PlayerState player,float x,float z)
        {
            double dx=x-player.X,dz=z-player.Z;
            return Math.Sqrt(dx*dx+dz*dz);
        }

        private static int CrewCount(RoundState state) => state.ConnectedCrewCount>0?state.ConnectedCrewCount:state.Players.FindAll(p=>p.Connected).Count;

        private static string Route(PlayerState player,float x,float z)
        {
            double distance=Distance(player,x,z);
            if(distance<3)return "here";
            double dx=x-player.X,dz=z-player.Z;
            string northSouth=dz>3?"N":dz< -3?"S":"";
            string eastWest=dx>3?"E":dx< -3?"W":"";
            return Math.Ceiling(distance).ToString("0")+" m "+northSouth+eastWest;
        }
    }
}
