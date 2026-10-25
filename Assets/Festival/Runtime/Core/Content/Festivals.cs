using System;

namespace Festival.Core
{
    /// <summary>Every weekend tuning number. A weekend is four levels: Day 1, Night 1, Day 2, Night 2.</summary>
    public static class Festivals
    {
        public sealed class LevelTuning
        {
            public string Name=""; public bool Night; public double DurationSeconds,SuspicionMultiplier; public int QuotaPerCrew,Narcs,ChainLength;
        }
        sealed class Row { public string Name=""; public int[] DayQuota=new int[0],Narcs=new int[0],NightChain=new int[0]; public double[] Suspicion=new double[0]; }
        // Fictional parodies only. Day columns: D1, D2. Level columns: D1, N1, D2, N2. Night columns: N1, N2.
        static readonly Row[] Table={
            new Row{Name="Palm Mirage",DayQuota=new[]{15,20},Narcs=new[]{2,2,3,3},Suspicion=new[]{1.0,1.1,1.2,1.3},NightChain=new[]{2,3}},
            new Row{Name="Ember Playa",DayQuota=new[]{22,30},Narcs=new[]{3,4,5,5},Suspicion=new[]{1.25,1.35,1.5,1.6},NightChain=new[]{3,4}},
        };
        public const int LevelCount=4;
        // Levels run Day 1, Night 1, Day 2, Night 2. Without building the level's tuning, so per-frame callers make no garbage.
        public static bool Night(int level)=>level%2==1;
        public const double DaySeconds=480,NightSeconds=600;
        // Each encore lap: quota and suspicion x(1 + .25 t), narcs and chain + t, each capped.
        public const double EncoreStep=.25,MaxSuspicionMultiplier=2.5;
        public const int MaxNarcs=8,MaxChainLength=5;
        public static double EncoreScale(int encoreTier)=>1+EncoreStep*encoreTier;
        // What a day's quota is paid from, for a crew of `crew`. ECON-2: each festivalgoer who sells has StockCopies units a level
        // (StockBuys.cs), and a buyer takes SalesPerBuyer sales a level, one more from a crew of five, so a crew of up to eight
        // can sell its quota. A sale pays x EncoreScale like the quota (FestivalSimulation.PayoutMultiplier), so every
        // encore lap can be won too, with no cap on the quota.
        public static int StockCopies(int crew)=>Math.Max(2,crew);
        public static int SalesPerBuyer(int crew)=>2+(crew-1)/4;
        // The old shuttle stop is the way back to camp.
        public const float CampGateX=0,CampGateZ=-32,CampGateRadius=5;
        // POLO-1: Palm Mirage's twists (FestivalTwists.cs). Influencers film along their facing: a live player within FilmRange,
        // inside the FilmConeDegrees cone and in line of sight, puts every wook within FilmWitnessRadius of them on alert at
        // FilmSuspicionPerSecond, a passive gain like sprinting in view.
        public const int PoloFestival=0,Influencers=3;
        public const float FilmRange=8,FilmConeDegrees=40,FilmWitnessRadius=10;
        public const double FilmSuspicionPerSecond=3;
        // Two VIP zones flank the main stage's aisle, roped off to anyone without a vip_wristband; a sale made inside one to a
        // buyer inside pays VipPayoutFactor times. The guard who talks people in stands at the west rope's aisle corner, facing
        // the path up from camp, and the night market sells wristbands at its east stall.
        // The security building's outer wall faces. A bust holds a player in it, and the host refuses a step closer than HoldingInset
        // to a wall: just past where a body meets the walls, so they slide it along first. The back stops at 12.2 to keep the Giggle
        // Tank at 30,14 clear; the pay desk (27,5) and the release spot (24,2) stay outside.
        public static readonly Area SecurityBuilding=new Area(24,5.5f,33,12.2f);
        public const float HoldingInset=.55f;
        public static bool InHolding(float x,float z)=>SecurityBuilding.Contains(x,z,-HoldingInset);
        public static readonly Area[] VipZones={new Area(-14,19.6f,-8,26),new Area(8,19.6f,14,26)};
        public const int VipPayoutFactor=2;
        public const float VipGuardPostX=-7.2f,VipGuardPostZ=19,VipGuardPostYaw=180;
        public const float VipStallX=-12,VipStallZ=-20.2f,VipStallRange=2.8f;
        // The Ferris wheel stands on the open lawn east of the way back to camp. Boarding at its base locks the rider in for one
        // WheelRideSeconds turn; up there they see every cop and, at night, roughly where each lost friend is: which square of a
        // WheelAreaSize grid, fixed to the map, they stand in (POLO-3).
        public const float WheelX=20,WheelZ=-28,WheelReach=2.5f,WheelAreaSize=20;
        public const double WheelRideSeconds=20;
        // PLAYA-1: Ember Playa's twists (FestivalTwists.cs). Money there reads as odd objects, a different kind for each crew
        // member by their PlayerState.Ordinal, over the same dollar economy.
        public const int PlayaFestival=1;
        // ponytail: every kind pluralises with an "s"; an irregular one would need its own plural.
        static readonly string[] OddObjects={"button","ramen packet","bottle cap","friendship bracelet","rubber duck","glitter sticker","kazoo","odd sock"};
        // Dust storms of StormMin-StormMaxSeconds blow through after calms of CalmMin-CalmMaxSeconds (the first calm opens the
        // level), on a schedule seeded by the level's spin. Festivalgoers and cops see SightRange, and only DustStormSightRange
        // in a storm.
        public const int StormMinSeconds=20,StormMaxSeconds=40,CalmMinSeconds=60,CalmMaxSeconds=120;
        public const float SightRange=12,DustStormSightRange=5;
        // Two art cars crawl round their loops at ArtCarSpeed, one on the lawn each side of the main path, between the bunting and
        // the crowd, clear of trees, totems and the crowd's standing spots. Corners as x, z pairs: car 0 heads north up its
        // inner side, car 1 south. RideCar boards the one within ArtCarReach. PLAYA-4: for ArtCarTrailSeconds after a rider
        // boards, festivalgoers already after them trail the car, holding off ArtCarReach from it (WookTick).
        public const int ArtCars=2;
        public const float ArtCarSpeed=1.2f,ArtCarReach=3;
        public const double ArtCarTrailSeconds=8;
        // A deck set keeps 16 logical beats on the shared stage grid; each fresh spectator tips the crew once per level.
        public const int DeckSetNotes=16;
        public const double DeckBeatSeconds=.4,DeckTipPerHead=.75;
        public const float DeckFanRange=6;
        static readonly float[][] ArtCarLoops={new float[]{-8,-10,-8,8,-13,8,-13,-10},new float[]{8,8,8,-10,13,-10,13,8}};
        /// <summary>Where art car `car` is `seconds` into the level. It depends on the level clock alone, so a client works it out
        /// from its own view exactly as the host does.</summary>
        public static WorldPoint ArtCarAt(int car,double seconds)
        {
            var loop=ArtCarLoops[car];int corners=loop.Length/2;double lap=0;
            for(int k=0;k<corners;k++)lap+=Leg(loop,k);
            double d=ArtCarSpeed*seconds%lap;int at=0;
            while(at<corners-1&&d>Leg(loop,at)){d-=Leg(loop,at);at++;}
            int next=(at+1)%corners;float t=(float)Math.Min(1,d/Leg(loop,at));
            return new WorldPoint(loop[2*at]+(loop[2*next]-loop[2*at])*t,loop[2*at+1]+(loop[2*next+1]-loop[2*at+1])*t);
        }
        static double Leg(float[] loop,int k){int next=(k+1)%(loop.Length/2);double dx=loop[2*next]-loop[2*k],dz=loop[2*next+1]-loop[2*k+1];return Math.Sqrt(dx*dx+dz*dz);}
        // The effigy stands on the main path between the art-car loops. For the last BurnSeconds of Night 2 it burns: the crowd
        // strolls over at BurnWalkSpeed to a ring BurnRingRadius round it, and within BurnRadius of it (the crush) every step covers
        // BurnCrushFactor of the way and suspicion cools BurnCalmFactor times as fast. There the fire's roar carries a festivalgoer's
        // accusation only BurnShoutRange, not the usual 10 m, so it reaches its neighbours in the ring rather than the whole ring.
        public const float EffigyX=0,EffigyZ=2,BurnRadius=12,BurnRingRadius=5,BurnWalkSpeed=1.5f;
        public const double BurnSeconds=180,BurnCrushFactor=.7,BurnCalmFactor=2,BurnShoutRange=3;
        // GAS-2: GiggleTankPercent of levels hide a Giggle Tank at one of these spots, the same on every festival: behind the stage,
        // behind the medical tent, behind security, the north-west grove, west of lost property and the east lawn. Each is off
        // the paths, the VIP ropes, the art cars' loops and the Ferris wheel, clear of walls, and more than 2.5 m from anywhere
        // players routinely stand. Any living, free player with a free hand slot within GiggleTankReach grabs it; carried home inside
        // the camp gate before the level ends, it banks GiggleTankCash in the crew stash (GiggleTanks.cs).
        public static readonly WorldPoint[] GiggleTankSpots={new WorldPoint(11,37.5f),new WorldPoint(27,-12.5f),new WorldPoint(30,14),new WorldPoint(-31,31),new WorldPoint(-34,15),new WorldPoint(29,-1)};
        public const int GiggleTankPercent=20,GiggleTankCash=25;
        public const float GiggleTankReach=1.5f;
        // MINI-1: each level's minigame booth stands at one of these, the same on every festival: the far end of the west crosspath
        // by the picnic table, the far end of the east crosspath, and south of the bunting by the east footpath. Each is on a crowd's
        // edge, its 4 m ring clear of anywhere players routinely stand, south of the backstage line and off the art cars' loops
        // (MinigameTests checks), so nobody starts a game just by going about the level.
        public static readonly WorldPoint[] BoothSpots={new WorldPoint(-30,3),new WorldPoint(24,-4),new WorldPoint(12,-18)};
        // Small windfalls on open lawns, outside every route/booth/car margin. Both maps currently share collision ground.
        public static readonly WorldPoint[][] CashFindSpots={
            new[]{new WorldPoint(-34,-32),new WorldPoint(-25,-32),new WorldPoint(-16,-35),new WorldPoint(-34,-22),new WorldPoint(-34,-12),new WorldPoint(-34,-5),new WorldPoint(-34,23),new WorldPoint(-20,-2),new WorldPoint(-29,-24),new WorldPoint(35,23),new WorldPoint(35,5),new WorldPoint(35,-5),new WorldPoint(35,-16),new WorldPoint(35,-20),new WorldPoint(33,-32),new WorldPoint(-29,-14)},
            new[]{new WorldPoint(-34,-32),new WorldPoint(-25,-32),new WorldPoint(-16,-35),new WorldPoint(-34,-22),new WorldPoint(-34,-12),new WorldPoint(-34,-5),new WorldPoint(-34,23),new WorldPoint(-20,-2),new WorldPoint(-29,-24),new WorldPoint(35,23),new WorldPoint(35,5),new WorldPoint(35,-5),new WorldPoint(35,-16),new WorldPoint(35,-20),new WorldPoint(33,-32),new WorldPoint(-29,-14)},
        };
        /// <summary>How an amount of money reads for crew member playerOrdinal: "$12" at Palm Mirage, "12 ramen packets" (etc.) on Ember Playa.</summary>
        public static string CurrencyName(int festival,int playerOrdinal,int amount)=>festival!=PlayaFestival?"$"+amount:amount+" "+OddObjects[(playerOrdinal%OddObjects.Length+OddObjects.Length)%OddObjects.Length]+(amount==1?"":"s");
        public static bool InVipZone(int festival,float x,float z){if(festival!=PoloFestival)return false;foreach(var zone in VipZones)if(zone.Contains(x,z))return true;return false;}
        /// <summary>An axis-aligned rectangle of festival ground, edges included.</summary>
        public readonly struct Area
        {
            public readonly float MinX,MinZ,MaxX,MaxZ;
            public Area(float minX,float minZ,float maxX,float maxZ){MinX=minX;MinZ=minZ;MaxX=maxX;MaxZ=maxZ;}
            /// <summary>Whether x,z is on it, or within margin of it; a negative margin keeps that far inside its edges.</summary>
            public bool Contains(float x,float z,float margin=0)=>x>=MinX-margin&&x<=MaxX+margin&&z>=MinZ-margin&&z<=MaxZ+margin;
            public float CentreX=>(MinX+MaxX)/2;
            public float CentreZ=>(MinZ+MaxZ)/2;
        }
        public static int Count=>Table.Length;
        public static string Name(int festival)=>Table[festival].Name;
        public static LevelTuning For(RoundState s)=>Level(s.FestivalIndex,s.LevelIndex,s.EncoreTier);
        public static LevelTuning Level(int festival,int level,int encoreTier)
        {
            if(festival<0||festival>=Table.Length||level<0||level>=LevelCount||encoreTier<0)throw new ArgumentOutOfRangeException(nameof(level),"No such festival level");
            var row=Table[festival];bool night=Night(level);double scale=EncoreScale(encoreTier);
            return new LevelTuning{Name=(night?"Night ":"Day ")+(level/2+1),Night=night,DurationSeconds=night?NightSeconds:DaySeconds,
                QuotaPerCrew=night?0:(int)Math.Round(row.DayQuota[level/2]*scale,MidpointRounding.AwayFromZero),
                Narcs=Math.Min(MaxNarcs,row.Narcs[level]+encoreTier),
                SuspicionMultiplier=Math.Min(MaxSuspicionMultiplier,row.Suspicion[level]*scale),
                ChainLength=night?Math.Min(MaxChainLength,row.NightChain[level/2]+encoreTier):0};
        }
    }
}
