using System;
using System.Collections.Generic;

namespace Festival.Core
{
    /// <summary>Player-facing directions derived only from the viewer's filtered round state.</summary>
    public static class FestivalGuidance
    {
        public static string Headline(RoundState state, PlayerState player)
        {
            if(state==null||player==null)return "";
            if(state.Phase=="Shopping")return "GATHER AT CAMP • BUY GEAR • READY UP";
            if(state.Phase=="CampReview")return FestivalSimulation.ReviewRevealed(state)?"CAMP DEBRIEF • THE VERDICT":"CAMP DEBRIEF • VOTE BEFORE SHOPPING";
            if(state.Phase=="Spinning")return "SPINNING FOR THE TRIPPER";
            if(state.Phase=="Loading")return "HEADING TO THE FESTIVAL";
            if(state.Phase=="Results")return state.Result=="Success"?"FRIEND RESCUED • BACK TO CAMP":"ROUND OVER • BACK TO CAMP";
            if(player.Life=="Downed")return "DOWNED • CALL FOR HELP";
            if(player.Life=="Detained")return "DETAINED • ESCAPE OR GET RELEASED";
            if(player.Life=="Spirit")return "SPIRIT • REACH THE MEDICAL TENT";
            // CROWD-2: a big crew's night counts both lost friends.
            var second=state.SecondFriend;
            if(second.Active){int found=(state.FriendFound?1:0)+(second.Found?1:0);return found==2?"ESCORT BOTH FRIENDS BACK TO CAMP":"TWO FRIENDS LOST • "+found+" / 2 FOUND";}
            if(state.FriendFound)return "ESCORT FRIEND BACK TO CAMP";
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
            // HUD-3: once every vote is in (or solo practice drew none) the ballot is gone and the verdict plays; only the host acts.
            if(state.Phase=="CampReview")return !FestivalSimulation.ReviewRevealed(state)?"Click a friend for each award, or press 1, 2 or 3. Then the host opens the camp shop."
                :player.Id==state.HostPlayerId?"Once the verdict has played, press E to open the camp shop.":"Waiting for the host to open the camp shop.";
            if(state.Phase=="Results")return player.Id==state.HostPlayerId
                ?"Open Escape menu and choose NEXT ROUND to return to camp."
                :"Waiting for the host to start the next camp round.";
            if(player.Life=="Downed")return "Press E to distract attackers; a teammate can rescue or drag you.";
            if(player.Life=="Detained")return "Press E to work on escape, or ask a teammate for release.";
            if(player.Life=="Spirit")return "Medical tent "+Route(player,24,-20)+(CrewCount(state)==1?". Press E there for a 15-second self-revival task.":". Ask a teammate with your wristband to revive you.");
            if(FestivalSimulation.OnWheel(state,player.Id))return Lookout(state,player);
            var second=state.SecondFriend;
            if(state.FriendFound&&(!second.Active||second.Found))
            {
                // CROWD-2: a big crew's two friends may follow different escorts, and each needs one.
                var leader=Escort(state,state.FriendLeaderId);var other=second.Active?Escort(state,second.LeaderId):leader;
                if(leader==null||other==null)return "Friend needs an escort. Reach them and press E, then lead them to the way back to camp "+Route(player,0,-32)+".";
                return leader.Id==player.Id||other.Id==player.Id
                    ?"Lead your friend to the way back to camp "+Route(player,0,-32)+". Stay close."
                    :"Follow the escort to the way back to camp "+Route(player,0,-32)+".";
            }
            // A lost friend at the end of a finished trail (either of a big crew's two): head for one in sight, else search.
            string search="";
            foreach(var (trailDone,found,friend) in new[]{(state.GateOpened,state.FriendFound,state.FriendPosition),(second.Active&&second.GateOpened,second.Found,second.Position)})
            {
                if(!trailDone||found)continue;
                if(friend!=null&&(friend.X!=0||friend.Z!=0))return "Friend spotted "+Route(player,friend.X,friend.Z)+". Reach them and press E.";
                search="Search the north and side paths. The friend appears when nearby.";
            }
            if(search!="")return search;
            // Only the tripper sees the visions (buyers and narcs by day, the clue trail by night). The game's one hint that they lie
            // is the HUD's, once per level (FestivalHudText.TrustLine), so it is not repeated here.
            bool night=Festivals.For(state).Night;
            if(player.Id==state.TripperId)return night?"Your visions mark the next clue holder.":"Your visions mark buyers and narcs.";
            var tripper=state.Players.Find(p=>p.Id==state.TripperId);
            return "Stick with "+(tripper?.Name??"the tripper")+": only the tripper can see "+(night?"the clue trail.":"who is buying.");
        }

        /// <summary>
        /// The festival twists player can use where they stand, as the HUD lists them: on Palm Mirage the Ferris wheel at its base
        /// and a VIP wristband at the night market's VIP stall (POLO-1), on Ember Playa an art car rolling past (PLAYA-1), and on
        /// either a Giggle Tank (GAS-2). Each is offered exactly where the rules take it (FestivalSimulation.AtWheel, AtVipStall,
        /// ArtCarBeside, AtGiggleTank). The VIP guard's chat is
        /// offered like a check (FestivalHudText.CheckTarget), since F starts it too.
        /// </summary>
        public static List<(string Label,GameCommand Command)> TwistActions(RoundState state,PlayerState player)
        {
            var actions=new List<(string Label,GameCommand Command)>();
            if(state==null||player==null||state.Phase!="Playing"||player.Life!="Alive"||player.InteractionId!="")return actions;
            if(FestivalSimulation.AtWheel(state,player))actions.Add(("Ride the Ferris wheel ("+Festivals.WheelRideSeconds+" s)",new GameCommand{Kind=FestivalSimulation.RideWheelKind}));
            if(FestivalSimulation.ArtCarBeside(state,player)>=0)actions.Add(("Climb aboard the art car",new GameCommand{Kind=FestivalSimulation.RideCarKind}));
            var band=Catalog.FindItem(FestivalSimulation.VipWristband);
            if(FestivalSimulation.AtVipStall(state,player)&&!player.Inventory.Exists(i=>i.ItemId==band.Id))actions.Add(("Buy "+band.Name+"  •  $"+band.Price,new GameCommand{Kind="Buy",ItemId=band.Id}));
            // GAS-2: a Giggle Tank, on either festival.
            if(FestivalSimulation.AtGiggleTank(state,player))actions.Add(("Grab the Giggle Tank",new GameCommand{Kind=FestivalSimulation.GrabGiggleTankKind}));
            return actions;
        }

        // POLO-1: one turn up the Ferris wheel looks out over the grounds. The rider spots every cop (every view has them) and each
        // lost friend whose spot their view holds, which at night FestivalSession.ViewFor sends to the rider alone.
        // ponytail: every level has its two patrol cops, so there is no "nobody in sight" wording.
        private static string Lookout(RoundState state,PlayerState player)
        {
            var friends=new List<string>();var cops=new List<string>();
            foreach(var (lost,at) in new[]{(!state.FriendFound,state.FriendPosition),(state.SecondFriend.Active&&!state.SecondFriend.Found,state.SecondFriend.Position)})
                if(lost&&at!=null&&(at.X!=0||at.Z!=0))friends.Add(Route(player,at.X,at.Z));
            foreach(var n in state.Npcs)if(n.Kind=="Cop")cops.Add(Route(player,n.X,n.Z));
            var seen=new List<string>();
            if(friends.Count>0)seen.Add((friends.Count==1?"your friend ":"your friends ")+string.Join(", ",friends));
            if(cops.Count>0)seen.Add("security "+string.Join(", ",cops));
            return "From the wheel you spot "+string.Join(" and ",seen)+".";
        }

        private static double Distance(PlayerState player,float x,float z)
        {
            double dx=x-player.X,dz=z-player.Z;
            return Math.Sqrt(dx*dx+dz*dz);
        }

        private static PlayerState Escort(RoundState state,string id) => state.Players.Find(p=>p.Id==id&&p.Connected&&p.Life=="Alive");

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
