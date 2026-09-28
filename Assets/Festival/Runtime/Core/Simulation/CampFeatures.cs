using System;
using System.Collections.Generic;

namespace Festival.Core
{
    /// <summary>One source of truth for camp dressing, interaction points and interior views.</summary>
    public static class CampFeatures
    {
        public const float DjX=8,DjZ=-3.5f;
        public const float CampHalfWidth=31,CampHalfDepth=30;
        // Keep interior cells beyond the 100 m decorative woodland rise. At
        // 70 m the hill rendered over their floors and furnishings.
        public const float InteriorX=125;
        public static float InteriorZ(string kind)=>kind=="Car"?-12:kind=="Tent"?0:12;
        public sealed class Site
        {
            public readonly string Id,Kind;
            public readonly float X,Z,Yaw;
            public Site(string id,string kind,float x,float z,float yaw=0){Id=id;Kind=kind;X=x;Z=z;Yaw=yaw;}
        }

        public static readonly Site[] Sites={
            new Site("tent_1","Tent",-15,8,20),new Site("tent_2","Tent",-15,-5,340),
            new Site("tent_3","Tent",15,12,25),new Site("tent_4","Tent",15,-5,330),
            new Site("tent_5","Tent",-19,15,65),new Site("tent_6","Tent",19,16,300),
            new Site("tent_7","Tent",-27,17,35),new Site("tent_8","Tent",27,17,325),
            new Site("car_1","Car",-14,-15,0),new Site("car_2","Car",14,-15,0),
            new Site("car_3","Car",-19,-15,8),new Site("car_4","Car",19,-15,352),
            new Site("car_5","Car",-8,-25,7),new Site("car_6","Car",8,-25,353),
            new Site("potty_1","Potty",19,6,0),new Site("potty_2","Potty",-19,1,0),
        };
        public static Site Find(string id){foreach(var site in Sites)if(site.Id==id)return site;return null;}
        // Every physical doorway owns a separate interior cell. Two players
        // entering different tents no longer appear in the same room.
        public static float InteriorSlotX(Site site)=>Array.IndexOf(Sites,site)<8?-InteriorX:InteriorX;
        public static float InteriorSlotZ(Site site)=>(Array.IndexOf(Sites,site)%8-3.5f)*9f;
        public static string Activity(string kind,int turn)
        {
            string[] choices=kind=="Car"
                ?new[]{"The horn plays a very apologetic honk.","You adjust the mirror until it frames only your hat.","The glove box contains one mysterious festival wristband."}
                :kind=="Tent"
                    ?new[]{"Your shadow puppet looks suspiciously like the missing friend.","You bounce on the sleeping bag. It squeaks like a duck.","You zip the tent door dramatically. Nobody saw it."}
                    :new[]{"The flush sounds like a tiny standing ovation.","You read graffiti: 'the real headliner was the queue'.","The hand dryer gives your hair an impossible new shape."};
            return choices[Math.Abs(turn-1)%choices.Length];
        }
        public static readonly string[] Tracks={"SILENCE","CAMPFIRE BOUNCE","MOONLIT DISCO","SNEAKY SKA"};
        public static readonly string[] ReviewAwards={"CHAOS MAGNET","UNLIKELY HERO","MOST COMMITTED TO THE BIT"};
        public static string ReviewWinner(List<CampReviewVote> votes)
        {
            var counts=new int[ReviewAwards.Length];
            if(votes!=null)foreach(var vote in votes)if(vote.Award>=0&&vote.Award<counts.Length)counts[vote.Award]++;
            int winner=0;
            for(int i=1;i<counts.Length;i++)if(counts[i]>counts[winner])winner=i;
            return ReviewAwards[winner];
        }
    }
}
