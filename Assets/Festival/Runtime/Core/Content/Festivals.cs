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
        public const double DaySeconds=480,NightSeconds=600;
        // Each encore lap: quota and suspicion x(1 + .25 t), narcs and chain + t, each capped.
        public const double EncoreStep=.25,MaxSuspicionMultiplier=2.5;
        public const int MaxNarcs=8,MaxChainLength=5;
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
        public static readonly Area[] VipZones={new Area(-14,19.6f,-8,26),new Area(8,19.6f,14,26)};
        public const int VipPayoutFactor=2;
        public const float VipGuardPostX=-7.2f,VipGuardPostZ=19,VipGuardPostYaw=180;
        public const float VipStallX=-12,VipStallZ=-20.2f,VipStallRange=2.8f;
        // The Ferris wheel stands on the open lawn east of the way back to camp. Boarding at its base locks the rider in for one
        // WheelRideSeconds turn; up there they see every cop and, at night, where the lost friend is.
        public const float WheelX=20,WheelZ=-28,WheelReach=2.5f;
        public const double WheelRideSeconds=20;
        // PLAYA-1: Ember Playa's twists (FestivalTwists.cs). Money there reads as odd objects, a different kind for each crew
        // member by their PlayerState.Ordinal, over the same dollar economy.
        public const int PlayaFestival=1;
        // ponytail: every kind pluralises with an "s"; an irregular one would need its own plural.
        static readonly string[] OddObjects={"button","ramen packet","bottle cap","friendship bracelet","rubber duck","glitter sticker","kazoo","odd sock"};
        // Dust storms of StormMin-StormMaxSeconds blow through after calms of CalmMin-CalmMaxSeconds (the first calm opens the
        // level), on a schedule seeded by the level's spin. Festivalgoers see SightRange, and only DustStormSightRange in a storm.
        public const int StormMinSeconds=20,StormMaxSeconds=40,CalmMinSeconds=60,CalmMaxSeconds=120;
        public const float SightRange=12,DustStormSightRange=5;
        /// <summary>How an amount of money reads for crew member playerOrdinal: "$12" at Palm Mirage, "12 ramen packets" (etc.) on Ember Playa.</summary>
        public static string CurrencyName(int festival,int playerOrdinal,int amount)=>festival!=PlayaFestival?"$"+amount:amount+" "+OddObjects[(playerOrdinal%OddObjects.Length+OddObjects.Length)%OddObjects.Length]+(amount==1?"":"s");
        public static bool InVipZone(int festival,float x,float z){if(festival!=PoloFestival)return false;foreach(var zone in VipZones)if(zone.Contains(x,z))return true;return false;}
        /// <summary>An axis-aligned rectangle of festival ground, edges included.</summary>
        public readonly struct Area
        {
            public readonly float MinX,MinZ,MaxX,MaxZ;
            public Area(float minX,float minZ,float maxX,float maxZ){MinX=minX;MinZ=minZ;MaxX=maxX;MaxZ=maxZ;}
            public bool Contains(float x,float z)=>x>=MinX&&x<=MaxX&&z>=MinZ&&z<=MaxZ;
        }
        public static int Count=>Table.Length;
        public static string Name(int festival)=>Table[festival].Name;
        public static LevelTuning For(RoundState s)=>Level(s.FestivalIndex,s.LevelIndex,s.EncoreTier);
        public static LevelTuning Level(int festival,int level,int encoreTier)
        {
            if(festival<0||festival>=Table.Length||level<0||level>=LevelCount||encoreTier<0)throw new ArgumentOutOfRangeException(nameof(level),"No such festival level");
            var row=Table[festival];bool night=level%2==1;double scale=1+EncoreStep*encoreTier;
            return new LevelTuning{Name=(night?"Night ":"Day ")+(level/2+1),Night=night,DurationSeconds=night?NightSeconds:DaySeconds,
                QuotaPerCrew=night?0:(int)Math.Round(row.DayQuota[level/2]*scale,MidpointRounding.AwayFromZero),
                Narcs=Math.Min(MaxNarcs,row.Narcs[level]+encoreTier),
                SuspicionMultiplier=Math.Min(MaxSuspicionMultiplier,row.Suspicion[level]*scale),
                ChainLength=night?Math.Min(MaxChainLength,row.NightChain[level/2]+encoreTier):0};
        }
    }
}
