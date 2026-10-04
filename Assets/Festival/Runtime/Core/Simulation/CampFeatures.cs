using System;

namespace Festival.Core
{
    /// <summary>One source of truth for camp dressing, interaction points and interior views.</summary>
    public static class CampFeatures
    {
        public const float DjX=4.3f,DjZ=-5.6f;
        // GAS-1: the Giggle Balloons stand 2 m off the gathering mat's west edge, clear of the picnic table, the cooler and tent 2's
        // door; a hit is taken within GiggleBalloonReach of them.
        public const float GiggleBalloonX=-10.5f,GiggleBalloonZ=-2,GiggleBalloonReach=2.5f;
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
        // CAMP-3: the public visit seed chooses a spot; consecutive visits never repeat it, including negative seeds.
        public static readonly WorldPoint[] GnomeSpots={new WorldPoint(-11,16),new WorldPoint(11,19)};
        public static WorldPoint GnomePoint(int seed)=>GnomeSpots[(seed%GnomeSpots.Length+GnomeSpots.Length)%GnomeSpots.Length];
        public sealed class Egg
        {
            public readonly string Id,Name;
            public readonly float X,Z;
            public readonly string[] Lines;
            public Egg(string id,string name,float x,float z,params string[] lines){Id=id;Name=name;X=x;Z=z;Lines=lines;}
        }
        // Array order is the discovery-mask contract. Keep these eight IDs stable across local profile saves.
        public static readonly Egg[] Eggs={
            new Egg("gnome","The gnome",-11,16,"The gnome whispers: the real festival was the snacks we hid along the way.","The gnome refuses to reveal its snack supplier.","The gnome claims it is on the lineup. Nobody can verify this."),
            new Egg("button","DO NOT PRESS",4.85f,DjZ,"DO NOT PRESS. You press it. Confetti and an air horn happen.","You were warned. Twice now.","The button's legal team would like a word."),
            new Egg("chair","The headliner's chair",-10,3.5f,"You feel famous for four seconds.","Your rider requests one crisp and absolute silence.","The imaginary crowd goes mild."),
            new Egg("lost_found","Lost and found",10,7,"Fourteen left sandals, one right sandal, and a glow stick still glowing out of spite.","Somebody has lost a single eyebrow. No questions asked.","The box contains a smaller box labelled LOSTER AND FOUNDER."),
            new Egg("hammock","The hammock sleeper",-23,-6,"five more minutes… the bass hasn't dropped yet","wake me when the opening act finishes… tomorrow","I wasn't asleep… I was buffering"),
            new Egg("flamingo","The inflatable flamingo",23,-6,"The flamingo lets out a long, sad squeak.","The flamingo is already having a flat day.","The flamingo has nothing left to give."),
            new Egg("cone","The cone shrine",-7,-19,"You pay your respects to Cone. Cone is pleased.","Cone blesses your extremely questionable itinerary.","Cone asks for no offerings. Cone has seen your budget."),
            // The standing point is north of car_1's back window, outside its collision box and door zone.
            new Egg("wash_me","WASH ME",-14,-11.2f,"You write {word} in the dust.","The dust now says {word}. Very professional.","{word}. Your finest work yet.")
        };
        public static Egg FindEgg(string id){foreach(var egg in Eggs)if(egg.Id==id)return egg;return null;}
        public static WorldPoint EggPoint(Egg egg,int seed)=>egg.Id=="gnome"?GnomePoint(seed):new WorldPoint(egg.X,egg.Z);
        public static readonly string[] DustWords={"BASS","HELP","NARC?","MOM SAYS HI"};
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
        // MUSIC-2: SILENCE, then Matt's five recorded tracks (Presentation's FestivalMusic.Tracks, in this order).
        public static readonly string[] Tracks={"SILENCE","TECH HOUSE","DISCO HOUSE","ORGANIC HOUSE","DRUM & BASS","DUB REGGAE"};
        // DEBRIEF-1 award pools: each debrief draws two worst awards, then one best award.
        public static readonly string[] WorstAwards={"Worst Dancer","Trusted the Narc","Got Lost","Blew Our Cover"};
        public static readonly string[] BestAwards={"MVP Tripper","Carried the Team","Best Moves"};
    }
}
