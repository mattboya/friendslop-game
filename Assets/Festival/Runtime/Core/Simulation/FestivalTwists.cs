using System;

namespace Festival.Core
{
    /// <summary>Each festival's twists. POLO-1: Palm Mirage's, off on every other festival. Tuning lives in Festivals.cs.
    /// Influencers film the crowd, and a player on camera puts the wooks around them on alert. VIP zones are roped off to anyone
    /// without a vip_wristband (bought at the night market, or talked out of the VIP guard), and inside them buyers pay double.
    /// The Ferris wheel is a lookout: one turn stuck up top shows the rider every cop and, at night, where the lost friend is.
    /// PLAYA-1: Ember Playa's, off everywhere else. Dust storms blow through, and while one does festivalgoers see only 5 m.</summary>
    public sealed partial class FestivalSimulation
    {
        public const string Influencer="Influencer",VipGuard="VipGuard",VipWristband="vip_wristband",RideWheelKind="RideWheel";

        // As the crew leaves camp, once DealRoles has dealt the level's roles: on Palm Mirage the regular festivalgoer nearest the
        // guard post takes it up, and a fresh few of the others film.
        void DealTwists()
        {
            if(State.FestivalIndex!=Festivals.PoloFestival)return;
            NpcState guard=null;
            foreach(var n in State.Npcs)if(n.Role=="Regular"&&(guard==null||Distance(n.X,n.Z,Festivals.VipGuardPostX,Festivals.VipGuardPostZ)<Distance(guard.X,guard.Z,Festivals.VipGuardPostX,Festivals.VipGuardPostZ)))guard=n;
            // ponytail: a crowd with no regulars (only in hand-built states) simply has no guard; the night market still sells wristbands.
            if(guard!=null){guard.Twist=VipGuard;guard.X=Festivals.VipGuardPostX;guard.Z=Festivals.VipGuardPostZ;guard.Yaw=Festivals.VipGuardPostYaw;guard.IdlePose="Watching";}
            var crowd=Shuffled(State.Npcs.FindAll(n=>n.Kind=="Wook"&&n!=guard),new ContentRandom(unchecked(State.SpinSeed*11+3)));
            for(int i=0;i<Festivals.Influencers&&i<crowd.Count;i++)crowd[i].Twist=Influencer;
        }
        // Every Playing step, after the crowd has looked around: a live player in an influencer's frame puts every wook within
        // FilmWitnessRadius of them on alert, whether or not that wook can see them (the stream shows their face). It is a passive
        // gain, so it scales with the level and the player's pack like sprinting in view, and it counts as being seen.
        void Film(double dt)
        {
            double heat=Festivals.For(State).SuspicionMultiplier;
            foreach(var cam in State.Npcs)if(cam.Twist==Influencer)foreach(var p in State.Players)
            {
                if(!p.Connected||!InFrame(cam,p))continue;
                double gain=Festivals.FilmSuspicionPerSecond*PassiveGain(p,dt,heat);
                foreach(var n in State.Npcs)if(n.Kind=="Wook"&&Distance(n.X,n.Z,p.X,p.Z)<=Festivals.FilmWitnessRadius){var o=Observe(n,p);o.Suspicion=Math.Min(100,o.Suspicion+gain);o.LastSeenSeconds=State.SimulationSeconds;}
            }
        }
        // The phone looks along the influencer's facing: FilmRange deep, FilmConeDegrees wide, blocked by walls.
        bool InFrame(NpcState cam,PlayerState p)
        {
            double d=Distance(cam.X,cam.Z,p.X,p.Z);if(p.Life!="Alive"||d>Festivals.FilmRange)return false;
            double yaw=cam.Yaw*Math.PI/180;
            if(d>.1&&(Math.Sin(yaw)*(p.X-cam.X)+Math.Cos(yaw)*(p.Z-cam.Z))/d<Math.Cos(Festivals.FilmConeDegrees*Math.PI/360))return false;
            return HasLineOfSight==null||HasLineOfSight(cam.X,cam.Z,p.X,p.Z);
        }

        bool InVip(float x,float z)=>Festivals.InVipZone(State.FestivalIndex,x,z);
        // TryMove's check as p steps to (x,z). A Ferris wheel rider only looks around. At the festival the VIP ropes keep out anyone
        // without a wristband; stepping out, or about inside, is always fine, so nobody handing theirs over inside is stranded,
        // and spirits float through.
        bool MayStep(PlayerState p,float x,float z)
        {
            if(OnWheel(State,p.Id))return Distance(p.X,p.Z,x,z)<=.001;
            return State.Phase!="Playing"||p.Life=="Spirit"||!InVip(x,z)||InVip(p.X,p.Z)||Count(p,VipWristband)>0;
        }
        // A sale made inside a VIP zone, to a buyer inside, pays VipPayoutFactor times: one over the rope pays as usual.
        int VipPayout(PlayerState seller,NpcState buyer)=>buyer!=null&&InVip(seller.X,seller.Z)&&InVip(buyer.X,buyer.Z)?Festivals.VipPayoutFactor:1;
        // Wristbands are sold only at the festival's night-market VIP stall, never at camp. One takes a slot like any gear, but
        // unlike a shelf buy it is not put in your hand.
        CommandResult BuyVipWristband(PlayerState p)
        {
            var band=Catalog.FindItem(VipWristband);
            if(State.Phase!="Playing"||State.FestivalIndex!=Festivals.PoloFestival||!Near(p,Festivals.VipStallX,Festivals.VipStallZ,Festivals.VipStallRange))return Reject("VIP wristbands are sold at the Palm Mirage night market's VIP stall");
            if(p.Cash<band.Price)return Reject("Insufficient cash");
            if(!CanAdd(p,VipWristband,1))return Reject(RopeRefusal(p));
            p.Cash-=band.Price;Add(p,VipWristband,1);return Ok(band.Name+" bought for $"+band.Price);
        }
        // TRIP-3's MayConfirm lets anyone chat with the VIP guard who has a hand free for the wristband; the chat finishing hands it over.
        bool TalksPastTheRope(PlayerState p,NpcState n)=>n.Twist==VipGuard&&CanAdd(p,VipWristband,1);
        string RopeRefusal(PlayerState p)=>Count(p,VipWristband)>0?"You already have a VIP wristband":"Free a hand for the VIP wristband";
        // ponytail: hands filled during the chat (a handoff accepted mid-chat) get nothing; the guard can be asked again.
        void PassTheRope(InteractionState i,NpcState n){var p=Player(i.PlayerId);if(i.Kind=="ConfirmChat"&&n.Twist==VipGuard&&p!=null&&CanAdd(p,VipWristband,1))Add(p,VipWristband,1);}

        /// <summary>Whether playerId is riding the Ferris wheel. Reads a client's view too, since a view carries its own player's
        /// interactions, so the rider's client can mark every cop (every view carries every cop) while this holds.</summary>
        public static bool OnWheel(RoundState s,string playerId)=>s.Interactions.Exists(i=>i.PlayerId==playerId&&i.Kind==RideWheelKind&&i.Status=="Active");
        /// <summary>At night a Ferris wheel rider sees where the lost friend is; FestivalSession.ViewFor sends it to them alone.</summary>
        public static bool WheelShowsFriend(RoundState s,string viewer)=>OnWheel(s,viewer)&&Festivals.For(s).Night;
        // Anyone at the base boards for one turn, any number at once. In the rules the rider stays at the base, where anything
        // that could reach them still can, and cannot step off or cancel until the turn ends (MayStep, Apply's Cancel).
        CommandResult RideWheel(PlayerState p)
        {
            if(State.FestivalIndex!=Festivals.PoloFestival)return Reject("There's no Ferris wheel at this festival");
            if(!Near(p,Festivals.WheelX,Festivals.WheelZ,Festivals.WheelReach))return Reject("Board the Ferris wheel at its base");
            NewInteraction(p,RideWheelKind,"wheel",Festivals.WheelRideSeconds);return Ok("One full turn: you're up there until it comes round");
        }

        // PLAYA-1: Ember Playa's twists, off on every other festival. Tuning lives in Festivals.cs; the odd-object money is
        // Festivals.CurrencyName.
        /// <summary>Whether a dust storm is blowing on Ember Playa. Each level's spin seeds its schedule: a calm of
        /// CalmMinSeconds-CalmMaxSeconds, a storm of StormMinSeconds-StormMaxSeconds, and so on. It reads only the spin seed and
        /// the level clock, so a client works it out from its own view exactly as the host does.</summary>
        public static bool DustStorm(RoundState s)
        {
            if(s.FestivalIndex!=Festivals.PlayaFestival)return false;
            var random=new ContentRandom(unchecked(s.SpinSeed*13+5));
            // Bounded by the longest level, so a clock from a bad snapshot or view can't spin this forever.
            for(double at=0;at<Festivals.NightSeconds;)
            {
                at+=Festivals.CalmMinSeconds+random.Next(Festivals.CalmMaxSeconds-Festivals.CalmMinSeconds+1);if(at>s.ElapsedSeconds)return false;
                at+=Festivals.StormMinSeconds+random.Next(Festivals.StormMaxSeconds-Festivals.StormMinSeconds+1);if(at>s.ElapsedSeconds)return true;
            }
            return false;
        }
        // Festivalgoers see SightRange, down to DustStormSightRange in a storm; cops keep their eyes.
        double SightRange(NpcState n)=>n.Kind=="Wook"&&DustStorm(State)?Festivals.DustStormSightRange:Festivals.SightRange;
    }
}
