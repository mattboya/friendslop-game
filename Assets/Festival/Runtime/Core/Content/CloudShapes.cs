using System;
using System.Collections.Generic;

namespace Festival.Core
{
    /// <summary>LIGHT-2: a day level's clouds and the funny shapes they now and then take. Like the dust storms and the art cars,
    /// the whole sky is a pure function of the level's spin seed and clock, so every player works out the same sky from their own
    /// view and sees the same duck at the same moment. Positions are metres from the middle of the sky (FestivalClouds keeps it
    /// over the viewer); FestivalClouds also decides when there are none (night, dust storms, camp).</summary>
    public static class CloudShapes
    {
        // 8-14 clouds a level. Their centres stay BandInner-BandOuter m out from the middle of the sky and MinAltitude-MaxAltitude m
        // up, so a cloud is never over your head and never past the camera's 130 m far clip.
        public const int MinClouds=8,MaxClouds=14;
        public const float BandInner=60,BandOuter=110,MinAltitude=45,MaxAltitude=70;
        // One wind a level, MinWind-MaxWind m/s. Each cloud runs down a lane with the wind, LaneInner-LaneOuter m to one side of
        // the middle so it never crosses the inner band, and wraps back to the lane's start where it leaves the outer one,
        // fading out over the lane's last FadeMetres and back in over its first.
        public const float MinWind=1,MaxWind=2,FadeMetres=15;
        const int LaneInner=60,LaneOuter=90;
        // Every puff of a cloud, ordinary or shaped, stays within ±HalfWidth across and ±HalfHeight up of its middle, and a cloud
        // has at most MaxPuffs of them. Shapes fill the footprint; an ordinary cloud is smaller, about 30 x 16 m.
        public const float HalfWidth=22,HalfHeight=13;
        public const int MaxPuffs=28;
        // Start to start, a shape follows MinGap-MaxGapSeconds after the last one began (the first after the level starts). It
        // morphs in over MorphSeconds, holds MinHold-MaxHoldSeconds and melts back over MorphSeconds.
        public const int MinGapSeconds=60,MaxGapSeconds=120,MinHoldSeconds=15,MaxHoldSeconds=20;
        public const double MorphSeconds=4;

        /// <summary>One soft puff, in metres in its cloud's own plane: X across, Y up, Radius how far it reaches.</summary>
        public readonly struct Puff { public readonly float X,Y,Radius; public Puff(float x,float y,float radius){X=x;Y=y;Radius=radius;} }
        /// <summary>Where a cloud is: X, Z across and Y up from the middle of the sky, and Alpha from 0 (faded out) to 1.</summary>
        public readonly struct Place { public readonly float X,Y,Z,Alpha; public Place(float x,float y,float z,float alpha){X=x;Y=y;Z=z;Alpha=alpha;} }
        /// <summary>A funny shape: a table of puffs that fits one cloud's footprint. The shapes mean nothing.</summary>
        public sealed class Shape { public readonly string Name; public readonly Puff[] Puffs; public Shape(string name,Puff[] puffs){Name=name;Puffs=puffs;} }
        /// <summary>A cloud: its lane (Offset to the side of the middle, Lane its half length), how far down the lane it starts
        /// (Phase), its height and its own puffs.</summary>
        public sealed class Cloud
        {
            public readonly float Offset,Lane,Phase,Altitude; public readonly Puff[] Puffs;
            public Cloud(float offset,float lane,float phase,float altitude,Puff[] puffs){Offset=offset;Lane=lane;Phase=phase;Altitude=altitude;Puffs=puffs;}
        }
        /// <summary>A shape on Cloud from Start: it morphs in, holds for Hold seconds and melts back by End.</summary>
        public readonly struct Show
        {
            public readonly int Cloud,Shape; public readonly double Start,Hold;
            public Show(int cloud,int shape,double start,double hold){Cloud=cloud;Shape=shape;Start=start;Hold=hold;}
            public double End=>Start+2*MorphSeconds+Hold;
            /// <summary>How far into its shape the cloud is: 0 its own puffs, 1 the full shape.</summary>
            public float Amount(double seconds){double into=seconds-Start,left=End-seconds;return into<=0||left<=0?0:(float)Math.Min(1,Math.Min(into,left)/MorphSeconds);}
        }
        /// <summary>A level's sky: the wind (m/s), the clouds and every shape they will take.</summary>
        public sealed class Sky
        {
            public readonly float WindX,WindZ; public readonly Cloud[] Clouds; public readonly Show[] Shows;
            readonly double alongX,alongZ,speed;
            public Sky(int spinSeed,double heading,double speed,Cloud[] clouds)
            {
                alongX=Math.Cos(heading);alongZ=Math.Sin(heading);this.speed=speed;WindX=(float)(alongX*speed);WindZ=(float)(alongZ*speed);Clouds=clouds;
                Shows=Schedule(spinSeed);
            }
            public Place At(int cloud,double seconds)
            {
                var c=Clouds[cloud];double along=Along(cloud,seconds);
                return new Place((float)(alongX*along-alongZ*c.Offset),c.Altitude,(float)(alongZ*along+alongX*c.Offset),(float)Math.Max(0,Math.Min(1,(c.Lane-Math.Abs(along))/FadeMetres)));
            }
            /// <summary>The show up at `seconds` (an index into Shows), or -1.</summary>
            public int ShowAt(double seconds){for(int k=0;k<Shows.Length;k++)if(seconds>Shows[k].Start&&seconds<Shows[k].End)return k;return -1;}
            // How far down its lane a cloud is, from -Lane (where it fades in) to Lane (where it wraps).
            double Along(int cloud,double seconds){var c=Clouds[cloud];double run=(c.Phase+speed*seconds)%(2*c.Lane);return (run<0?run+2*c.Lane:run)-c.Lane;}
            // A day level's shows, start to start MinGap-MaxGapSeconds apart, each a different shape from the one before.
            Show[] Schedule(int spinSeed)
            {
                var random=new ContentRandom(unchecked(spinSeed*43+19));var shows=new List<Show>();int shape=-1;
                for(double start=Gap(random);start<Festivals.DaySeconds;start+=Gap(random))
                {
                    double hold=MinHoldSeconds+random.Next(MaxHoldSeconds-MinHoldSeconds+1);
                    shape=shape<0?random.Next(Shapes.Length):(shape+1+random.Next(Shapes.Length-1))%Shapes.Length;
                    shows.Add(new Show(Overhead(start,start+2*MorphSeconds+hold),shape,start,hold));
                }
                return shows.ToArray();
            }
            static double Gap(ContentRandom random)=>MinGapSeconds+random.Next(MaxGapSeconds-MinGapSeconds+1);
            // The cloud nearest the zenith over the middle of the sky at `start`, out of those that stay fully in the sky until
            // `end` (else out of them all), so nobody sees half a duck fade away.
            int Overhead(double start,double end)
            {
                int best=-1;bool bestClear=false;double bestTilt=0;
                for(int c=0;c<Clouds.Length;c++)
                {
                    var at=At(c,start);double along=Along(c,start),tilt=Math.Sqrt(at.X*at.X+at.Z*at.Z)/at.Y;
                    bool clear=along>=FadeMetres-Clouds[c].Lane&&along+speed*(end-start)<=Clouds[c].Lane-FadeMetres;
                    if(best<0||clear&&!bestClear||clear==bestClear&&tilt<bestTilt){best=c;bestClear=clear;bestTilt=tilt;}
                }
                return best;
            }
        }

        /// <summary>The level's sky, from its spin seed alone.</summary>
        public static Sky For(int spinSeed)
        {
            var random=new ContentRandom(unchecked(spinSeed*23+7));
            double heading=random.Next(360)*Math.PI/180,speed=MinWind+random.Next(11)*(MaxWind-MinWind)/10.0;
            var clouds=new Cloud[MinClouds+random.Next(MaxClouds-MinClouds+1)];
            for(int c=0;c<clouds.Length;c++)
            {
                // Lanes alternate sides of the wind, so both halves of the sky get clouds, and the clouds start spread down them.
                float offset=(c%2==0?1:-1)*(LaneInner+random.Next(LaneOuter-LaneInner+1)),lane=(float)Math.Sqrt(BandOuter*BandOuter-offset*offset);
                float phase=(c+random.Next(50)/100f)/clouds.Length*2*lane,altitude=MinAltitude+random.Next((int)(MaxAltitude-MinAltitude)+1);
                clouds[c]=new Cloud(offset,lane,phase,altitude,Ordinary(random));
            }
            return new Sky(spinSeed,heading,speed,clouds);
        }
        // An ordinary cloud: 6-10 overlapping puffs in a row, bigger and higher in the middle, on a flat base.
        static Puff[] Ordinary(ContentRandom random)
        {
            var puffs=new Puff[6+random.Next(5)];
            for(int i=0;i<puffs.Length;i++)
            {
                float across=2f*i/(puffs.Length-1)-1,hump=1-across*across,radius=3.5f+3*hump+random.Next(16)/10f;
                puffs[i]=new Puff(across*11+(random.Next(31)-15)/10f,radius-10+hump*random.Next(41)/10f,radius);
            }
            return puffs;
        }

        /// <summary>Puff i of a cloud `amount` (0-1) of the way from its own puffs to a shape's. A puff only one side has grows from,
        /// or shrinks to, nothing where it stands.</summary>
        public static Puff Morph(Puff[] from,Puff[] to,int i,float amount)
        {
            Puff a=i<from.Length?from[i]:i<to.Length?new Puff(to[i].X,to[i].Y,0):default,b=i<to.Length?to[i]:new Puff(a.X,a.Y,0);float stay=1-amount;
            return new Puff(a.X*stay+b.X*amount,a.Y*stay+b.Y*amount,a.Radius*stay+b.Radius*amount);
        }

        /// <summary>The funny shapes, as seen from below: x across, y up and radius for each puff, in metres. Their puffs overlap
        /// as closely as an ordinary cloud's, so each reads as one cloud, not a string of beads.</summary>
        public static readonly Shape[] Shapes={
            Table("duck",-6,-6,6.5f, 0,-6,7, 6,-5.5f,6.5f, -10.5f,-4,4.5f, -12.3f,-2.5f,4.2f, -14.2f,-1,3.8f, -16,.5f,3.5f, -2,-1.5f,5, 8.5f,-1.5f,4.2f,
                9.5f,.8f,4.2f, 10.5f,3,4.2f, 11,6.5f,5.5f, 15.3f,6,3.2f, 17.1f,5.6f,3.2f, 18.8f,5.2f,3.2f),
            Table("rubber chicken",-11,2,6.5f, -5,1,6.5f, -14.5f,5.5f,3.5f, -16.2f,7,3.2f, -18,8.5f,3, -.5f,2,3.4f, 2.1f,2.3f,3.4f, 4.7f,2.6f,3.4f, 7.3f,2.9f,3.3f,
                9.9f,3.2f,3.3f, 12.5f,3.5f,3.3f, 15,4.5f,4, 18,3.4f,3, 14.8f,7.8f,3, 13.7f,8.7f,3, 12.5f,9.5f,3, -11,-2.5f,3, -11.2f,-5,3, -11.3f,-7.5f,3,
                -11.5f,-10,3, -14,-10,3, -4,-2.5f,3, -3.8f,-5,3, -3.7f,-7.5f,3, -3.5f,-10,3, -.5f,-10,3),
            Table("giant hand",-5.2f,0,5.5f, 0,.5f,5.5f, 5.2f,0,5.5f, -3,-5,5.5f, 3,-5,5.5f, 0,-8.5f,4, -7.8f,3.1f,3, -8.2f,5.6f,3, -8.5f,8,3, -2.6f,3.6f,3,
                -2.7f,5.7f,3, -2.7f,7.9f,3, -2.8f,10,3, 2.6f,3.6f,3, 2.7f,5.7f,3, 2.7f,7.9f,3, 2.8f,10,3, 7.8f,3.1f,3, 8,4.9f,3, 8.3f,6.7f,3, 8.5f,8.5f,3,
                -8.8f,-2.4f,3.5f, -10.9f,-1.3f,3.3f, -12.9f,-.1f,3.2f, -15,1,3),
            Table("face",-11,2,6.5f, -6,6,6.5f, -.5f,6.5f,5.5f, -6,-2,6.5f, -.5f,0,5.5f, 3.6f,5.2f,3.5f, 4,1.5f,4, 6.3f,.7f,3.8f, 8.7f,-.2f,3.7f, 11,-1,3.5f,
                2.8f,-3,3.5f, 2,-6.5f,4, -3.5f,-6.5f,4.5f),
            Table("dolphin",-13,-5,3.3f, -11,-3.7f,3.7f, -9,-2.3f,4.1f, -7,-1,4.5f, -3,1.5f,5.2f, .5f,1.8f,5.2f, 4,2,5.2f, 8,.5f,4.5f, 10.5f,-1,3.9f, 13,-2.5f,3.3f,
                15.5f,-3.3f,3, 17.1f,-3.8f,3, 18.8f,-4.3f,3, -14.5f,-3.5f,3, -16.5f,-1.8f,3, -18.5f,0,3, -15,-6.5f,3, -16.8f,-8,3, -18.5f,-9.5f,3, 0,6,3.5f,
                -1,7.8f,3.2f, -2,9.5f,3, 4,-2,3.2f),
            Table("pizza slice",-15,9.5f,3.5f, -12,9.5f,3.5f, -9,9.5f,3.5f, -6,9.5f,3.5f, -3,9.5f,3.5f, 0,9.5f,3.5f, 3,9.5f,3.5f, 6,9.5f,3.5f, 9,9.5f,3.5f,
                12,9.5f,3.5f, 15,9.5f,3.5f, -9,5.5f,4.5f, -4.5f,5.5f,5, 0,5.5f,5, 4.5f,5.5f,5, 9,5.5f,4.5f, -5,1,4.5f, 0,1,5, 5,1,4.5f, -2.2f,-2.8f,4, 2.2f,-2.8f,4,
                0,-5.8f,3.6f, 0,-7.9f,3.3f, 0,-10,3),
            Table("sneaker",-18.5f,-9.5f,3.5f, -15.4f,-9.5f,3.5f, -12.3f,-9.5f,3.5f, -9.2f,-9.5f,3.5f, -6.2f,-9.5f,3.5f, -3.1f,-9.5f,3.5f, 0,-9.5f,3.5f,
                3.1f,-9.5f,3.5f, 6.2f,-9.5f,3.5f, 9.2f,-9.5f,3.5f, 12.3f,-9.5f,3.5f, 15.4f,-9.5f,3.5f, 18.5f,-9.5f,3.5f, -13,-5.5f,5, -8,-5.5f,5, -3,-5.5f,5,
                2,-5.5f,5, 7,-5.5f,5, 12,-5.5f,5, 16.5f,-7,3.5f, -13,-.5f,5, -11,3,4, -6.5f,-1.8f,3.5f, -2,-1.5f,3.5f),
            Table("UFO",-18.5f,-1.5f,3.5f, -15.4f,-1.5f,3.5f, -12.3f,-1.5f,3.5f, -9.2f,-1.5f,3.5f, -6.2f,-1.5f,3.5f, -3.1f,-1.5f,3.5f, 0,-1.5f,3.5f,
                3.1f,-1.5f,3.5f, 6.2f,-1.5f,3.5f, 9.2f,-1.5f,3.5f, 12.3f,-1.5f,3.5f, 15.4f,-1.5f,3.5f, 18.5f,-1.5f,3.5f, -9,-2.5f,4.5f, 0,-3,5, 9,-2.5f,4.5f,
                0,3,5.5f, -4.5f,2.5f,4, 4.5f,2.5f,4, -8.5f,-6,3, -8.5f,-8,3, 0,-6.5f,3, 0,-9,3, 8.5f,-6,3, 8.5f,-8,3),
        };
        // TRIP-4: a day level's clue cloud (FestivalSimulation.CloudClueUp) comes up a whole ClueEarliest-ClueLatestSeconds into the
        // level and drifts through the sky for ClueSeconds.
        public const int ClueEarliestSeconds=60,ClueLatestSeconds=240;
        public const double ClueSeconds=90;
        /// <summary>A landmark the clue cloud can picture: where it stands, and the spot 5-8 m off, away from the paths and wherever
        /// players routinely stand, where reading the cloud leaves a cash stash.</summary>
        public sealed class Landmark { public readonly string Name; public readonly WorldPoint At,Stash; public Landmark(string name,WorldPoint at,WorldPoint stash){Name=name;At=at;Stash=stash;} }
        /// <summary>The landmarks a clue cloud pictures. The last, the Ferris wheel, stands only on Palm Mirage (LandmarkCount).
        /// The stage's stash is behind it, backstage, where being seen draws suspicion.</summary>
        public static readonly Landmark[] Landmarks={
            new Landmark("stage",new WorldPoint(0,32),new WorldPoint(-5,38)),
            new Landmark("night market",new WorldPoint(-18,-22),new WorldPoint(-18,-28)),
            new Landmark("medical tent",new WorldPoint(24,-20),new WorldPoint(29,-25)),
            new Landmark("security",new WorldPoint(27,5),new WorldPoint(21,9)),
            new Landmark("shuttle",new WorldPoint(0,-36),new WorldPoint(-7,-37)),
            new Landmark("lost property",new WorldPoint(-28,16),new WorldPoint(-23,20)),
            new Landmark("Ferris wheel",new WorldPoint(Festivals.WheelX,Festivals.WheelZ),new WorldPoint(14,-32)),
        };
        /// <summary>How many of Landmarks a festival's clue cloud can picture: all of them where there is a Ferris wheel.</summary>
        public static int LandmarkCount(int festival)=>festival==Festivals.PoloFestival?Landmarks.Length:Landmarks.Length-1;
        static Shape Table(string name,params float[] xyr){var puffs=new Puff[xyr.Length/3];for(int i=0;i<puffs.Length;i++)puffs[i]=new Puff(xyr[3*i],xyr[3*i+1],xyr[3*i+2]);return new Shape(name,puffs);}
    }
}
