using System;
using System.Collections.Generic;
using System.Linq;
using Festival.Core;

// LIGHT-2: day levels get 8-14 soft clouds drifting with one seeded wind on a band 60-110 m out and 45-70 m up, and every 60-120 s
// (start to start) the cloud nearest the zenith morphs over about 4 s into a funny shape, holds it 15-20 s and melts back. The
// whole sky is a pure function of the level's spin seed and clock, so every player sees the same duck at the same moment.
public static class CloudShapesTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("CloudShapes: "+message);}

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{EachLevelHas8To14CloudsOnTheBand,OneWindCarriesEveryCloud,CloudsFadeOutAndBackInWhereTheyWrap,
            TheSameSpinGivesTheSameSky,AShapeShowsEvery60To120SAndHolds15To20S,TheCloudNearestTheZenithTakesTheShape,
            EveryShapeFitsOneCloud,EveryShapeIsOneBoldCloud,ACloudMorphsIntoItsShapeAndBack})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" cloud test(s) failed:\n"+string.Join("\n",failures));
    }

    // Spin seeds the way Spin() makes them: all over the int range, negatives included.
    static IEnumerable<int> Spins(){for(int seed=1;seed<=200;seed++)yield return unchecked(seed*7919*104729+13);}
    static double Out(CloudShapes.Place at)=>Math.Sqrt(at.X*at.X+at.Z*at.Z);

    static void EachLevelHas8To14CloudsOnTheBand()
    {
        var counts=new SortedSet<int>();
        foreach(int spin in Spins())
        {
            var sky=CloudShapes.For(spin);counts.Add(sky.Clouds.Length);
            Check(sky.Clouds.Length>=8&&sky.Clouds.Length<=14,"spin "+spin+": a day level has 8-14 clouds, got "+sky.Clouds.Length);
            for(double t=0;t<=Festivals.DaySeconds;t+=5)
                for(int c=0;c<sky.Clouds.Length;c++)
                {
                    var at=sky.At(c,t);
                    Check(Out(at)>=60-1e-3&&Out(at)<=110+1e-3,"spin "+spin+": cloud "+c+" stays 60-110 m out, got "+Out(at).ToString("0.0")+" at "+t+" s");
                    Check(at.Y>=45&&at.Y<=70,"spin "+spin+": cloud "+c+" floats 45-70 m up, got "+at.Y);
                    Check(at.Alpha>=0&&at.Alpha<=1,"spin "+spin+": cloud "+c+" is somewhere between gone and solid, got "+at.Alpha);
                }
        }
        Check(counts.SequenceEqual(Enumerable.Range(8,7)),"every count from 8 to 14 turns up, got "+string.Join(",",counts));
    }

    static void OneWindCarriesEveryCloud()
    {
        var headings=new HashSet<int>();
        foreach(int spin in Spins())
        {
            var sky=CloudShapes.For(spin);double speed=Math.Sqrt(sky.WindX*sky.WindX+sky.WindZ*sky.WindZ);
            Check(speed>=1-1e-6&&speed<=2+1e-6,"spin "+spin+": the wind blows 1-2 m/s, got "+speed);
            headings.Add((int)Math.Round(Math.Atan2(sky.WindZ,sky.WindX)*180/Math.PI));
            int carried=0;
            for(double t=0;t<=Festivals.DaySeconds;t+=11)
                for(int c=0;c<sky.Clouds.Length;c++)
                {
                    CloudShapes.Place a=sky.At(c,t),b=sky.At(c,t+1);
                    if(Math.Abs(b.X-a.X)+Math.Abs(b.Z-a.Z)>5)continue; // it wrapped round the band
                    carried++;
                    Check(Math.Abs(b.X-a.X-sky.WindX)<1e-3&&Math.Abs(b.Z-a.Z-sky.WindZ)<1e-3,"spin "+spin+": cloud "+c+" drifts with the wind ("+sky.WindX+", "+sky.WindZ+") a second, got ("+(b.X-a.X)+", "+(b.Z-a.Z)+")");
                    Check(a.Y==b.Y,"spin "+spin+": cloud "+c+" keeps its height");
                }
            Check(carried>0,"spin "+spin+": the wind carries the clouds");
        }
        Check(headings.Count>=100,"each level's spin seeds its own wind heading, got "+headings.Count+" of 200");
    }

    // A cloud runs down a lane with the wind, then wraps to the lane's start; it fades out before the wrap and back in after, so
    // nobody sees one blink out of the sky.
    static void CloudsFadeOutAndBackInWhereTheyWrap()
    {
        foreach(int spin in Spins().Take(40))
        {
            var sky=CloudShapes.For(spin);double speed=Math.Sqrt(sky.WindX*sky.WindX+sky.WindZ*sky.WindZ);const double step=.25;
            Check(sky.Clouds.Length>0,"setup: spin "+spin+" has clouds");
            for(int c=0;c<sky.Clouds.Length;c++)
            {
                int wraps=0;bool solid=false;var before=sky.At(c,0);
                for(double t=step;t<=Festivals.NightSeconds;t+=step)
                {
                    var at=sky.At(c,t);
                    if(Math.Abs(at.X-before.X)+Math.Abs(at.Z-before.Z)>5)
                    {
                        wraps++;
                        Check(before.Alpha<.05&&at.Alpha<.05,"spin "+spin+": cloud "+c+" is all but gone where it wraps, alpha "+before.Alpha+" then "+at.Alpha);
                    }
                    else Check(Math.Abs(at.Alpha-before.Alpha)<=speed*step/CloudShapes.FadeMetres+1e-4,"spin "+spin+": cloud "+c+" fades gently, alpha "+before.Alpha+" to "+at.Alpha+" in "+step+" s");
                    solid|=at.Alpha==1;before=at;
                }
                Check(wraps>0,"spin "+spin+": cloud "+c+" wraps round the band at least once a night");
                Check(solid,"spin "+spin+": cloud "+c+" spends time fully in the sky");
            }
        }
    }

    static void TheSameSpinGivesTheSameSky()
    {
        var skies=new HashSet<string>();
        foreach(int spin in Spins())
        {
            string first=Picture(CloudShapes.For(spin)),again=Picture(CloudShapes.For(spin));
            Check(first==again,"spin "+spin+": two players working out the same spin see the same sky");
            skies.Add(first);
        }
        Check(skies.Count==200,"each spin seeds its own sky, got "+skies.Count+" of 200");
    }
    // Everything a player could see of a sky: each cloud's puffs and where it is now and then, and every show.
    static string Picture(CloudShapes.Sky sky)
    {
        var parts=new List<string>{sky.WindX+","+sky.WindZ};
        for(int c=0;c<sky.Clouds.Length;c++)
        {
            parts.Add(string.Join(";",sky.Clouds[c].Puffs.Select(p=>p.X+","+p.Y+","+p.Radius)));
            foreach(double t in new[]{0,77.5,313.25}){var at=sky.At(c,t);parts.Add(at.X+","+at.Y+","+at.Z+","+at.Alpha);}
        }
        foreach(var show in sky.Shows)parts.Add(show.Cloud+","+show.Shape+","+show.Start+","+show.Hold);
        return string.Join("|",parts);
    }

    static void AShapeShowsEvery60To120SAndHolds15To20S()
    {
        var shapes=new HashSet<int>();
        foreach(int spin in Spins())
        {
            var sky=CloudShapes.For(spin);var shows=sky.Shows;
            Check(shows.Length>=Festivals.DaySeconds/120-1,"spin "+spin+": a day level brings a shape at least every two minutes, got "+shows.Length);
            Check(shows[0].Start>=60&&shows[0].Start<=120,"spin "+spin+": the first shape starts 60-120 s into the level, got "+shows[0].Start);
            Check(shows[shows.Length-1].Start+120>=Festivals.DaySeconds,"spin "+spin+": shapes keep coming to the end of the day, the last at "+shows[shows.Length-1].Start);
            for(int k=0;k<shows.Length;k++)
            {
                var show=shows[k];string which="spin "+spin+" show "+k+": ";
                Check(show.Hold>=15&&show.Hold<=20,which+"holds its shape 15-20 s, got "+show.Hold);
                Check(show.Cloud>=0&&show.Cloud<sky.Clouds.Length,which+"is one of the sky's clouds, got "+show.Cloud);
                Check(show.Shape>=0&&show.Shape<CloudShapes.Shapes.Length,which+"is one of the shapes, got "+show.Shape);
                shapes.Add(show.Shape);
                if(k>0)
                {
                    Check(show.Start-shows[k-1].Start>=60&&show.Start-shows[k-1].Start<=120,which+"starts 60-120 s after the last one began, got "+(show.Start-shows[k-1].Start));
                    Check(shows[k-1].End<show.Start,which+"waits for the last one to melt back");
                    Check(show.Shape!=shows[k-1].Shape,which+"is never the same shape twice running");
                }
                double morph=CloudShapes.MorphSeconds;
                Check(morph>=3&&morph<=5,"a cloud morphs over about 4 s, got "+morph);
                Check(Math.Abs(show.End-(show.Start+2*morph+show.Hold))<1e-9,which+"ends once it has melted back");
                Check(show.Amount(show.Start-.01)==0&&show.Amount(show.Start)==0,which+"nothing shows before it starts");
                Check(Math.Abs(show.Amount(show.Start+morph/2)-.5)<1e-4,which+"half way into its shape after half the morph, got "+show.Amount(show.Start+morph/2));
                Check(show.Amount(show.Start+morph)==1&&show.Amount(show.Start+morph+show.Hold)==1,which+"the full shape from the end of the morph");
                Check(Math.Abs(show.Amount(show.End-morph/2)-.5)<1e-4,which+"half melted half way through melting");
                Check(show.Amount(show.End)==0&&show.Amount(show.End+.01)==0,which+"melted back at the end");
                Check(sky.ShowAt(show.Start+.01)==k&&sky.ShowAt(show.End-.01)==k,which+"is the show on while it is up");
                Check(sky.ShowAt(show.Start-.01)==-1&&sky.ShowAt(show.End+.01)==-1,which+"no show is on either side of it");
            }
            Check(sky.ShowAt(0)==-1&&sky.ShowAt(shows[0].Start-1)==-1,"spin "+spin+": the level opens on an ordinary sky");
        }
        Check(shapes.Count==CloudShapes.Shapes.Length,"every shape turns up somewhere, got "+shapes.Count+" of "+CloudShapes.Shapes.Length);
    }

    // The shape goes on the cloud nearest the zenith over the middle of the sky when the show starts, out of those that stay fully
    // in the sky until it has melted back, so nobody sees half a duck fade away.
    static void TheCloudNearestTheZenithTakesTheShape()
    {
        int shows=0,unclear=0;
        foreach(int spin in Spins())
        {
            var sky=CloudShapes.For(spin);
            foreach(var show in sky.Shows)
            {
                shows++;
                var clear=Enumerable.Range(0,sky.Clouds.Length).Where(c=>Clear(sky,c,show)).ToList();
                if(clear.Count==0){unclear++;continue;}
                Check(clear.Contains(show.Cloud),"spin "+spin+" at "+show.Start+" s: the shape goes on a cloud that stays fully in the sky, not cloud "+show.Cloud);
                foreach(int c in clear)Check(Tilt(sky.At(c,show.Start))>=Tilt(sky.At(show.Cloud,show.Start)),"spin "+spin+" at "+show.Start+" s: cloud "+c+" is nearer the zenith than cloud "+show.Cloud);
            }
        }
        Check(shows>0,"setup: the skies have shows");
        Check(unclear*20<shows,"almost every show has a cloud that stays in the sky, got "+unclear+" of "+shows+" without one");
    }
    static bool Clear(CloudShapes.Sky sky,int c,CloudShapes.Show show)
    {
        for(int k=0;k<=100;k++)if(sky.At(c,show.Start+(show.End-show.Start)*k/100).Alpha<1)return false;
        return true;
    }
    // How far from straight up a cloud is, seen from the middle of the sky: smaller is nearer the zenith.
    static double Tilt(CloudShapes.Place at)=>Out(at)/at.Y;

    static void EveryShapeFitsOneCloud()
    {
        var names=CloudShapes.Shapes.Select(s=>s.Name).ToList();
        Check(names.Count>=8,"at least eight shapes, got "+names.Count);
        foreach(var wanted in new[]{"duck","rubber chicken","giant hand","face","dolphin","pizza slice","sneaker","UFO"})
            Check(names.Contains(wanted),"there is a "+wanted+" among "+string.Join(", ",names));
        Check(names.Distinct().Count()==names.Count,"every shape has its own name");
        foreach(var shape in CloudShapes.Shapes)Fits(shape.Puffs,shape.Name);
        foreach(int spin in Spins().Take(40))
        {
            var sky=CloudShapes.For(spin);
            for(int c=0;c<sky.Clouds.Length;c++)Fits(sky.Clouds[c].Puffs,"spin "+spin+" cloud "+c);
        }
    }
    static void Fits(CloudShapes.Puff[] puffs,string what)
    {
        Check(puffs.Length>0,what+" has puffs");
        Check(puffs.Length<=CloudShapes.MaxPuffs,what+" has at most "+CloudShapes.MaxPuffs+" puffs, got "+puffs.Length);
        foreach(var p in puffs)
        {
            Check(p.Radius>0,what+": every puff has some size");
            Check(Math.Abs(p.X)+p.Radius<=CloudShapes.HalfWidth+1e-4&&Math.Abs(p.Y)+p.Radius<=CloudShapes.HalfHeight+1e-4,
                what+": the puff at ("+p.X+", "+p.Y+") r "+p.Radius+" fits one cloud's "+2*CloudShapes.HalfWidth+" x "+2*CloudShapes.HalfHeight+" m footprint");
        }
    }

    // A shape has to read from the ground as one cloud at least the size of the others, not a string of beads. FestivalClouds' puff
    // texture is solid only in the middle half of a puff's reach, so two puffs read as one cloud when those middles touch (centres
    // within Touch of the sum of their reaches); and a puff under MinPuff m shows as a dot.
    const float Touch=.5f,MinPuff=3;
    static void EveryShapeIsOneBoldCloud()
    {
        foreach(var shape in CloudShapes.Shapes)
        {
            var puffs=shape.Puffs;
            foreach(var p in puffs)Check(p.Radius>=MinPuff,shape.Name+": no puff smaller than "+MinPuff+" m, got one of "+p.Radius+" at ("+p.X+", "+p.Y+")");
            var joined=new HashSet<int>{0};
            for(bool grew=true;grew;)
            {
                grew=false;
                for(int i=0;i<puffs.Length;i++)
                    if(!joined.Contains(i)&&joined.Any(j=>Math.Sqrt(Sq(puffs[i].X-puffs[j].X)+Sq(puffs[i].Y-puffs[j].Y))<=Touch*(puffs[i].Radius+puffs[j].Radius)))grew=joined.Add(i);
            }
            var loose=Enumerable.Range(0,puffs.Length).Where(i=>!joined.Contains(i)).Select(i=>"("+puffs[i].X+", "+puffs[i].Y+")");
            Check(joined.Count==puffs.Length,shape.Name+" is one cloud, but these puffs float apart: "+string.Join(" ",loose));
            float wide=puffs.Max(p=>p.X+p.Radius)-puffs.Min(p=>p.X-p.Radius),tall=puffs.Max(p=>p.Y+p.Radius)-puffs.Min(p=>p.Y-p.Radius);
            Check(wide>=1.5f*CloudShapes.HalfWidth||tall>=1.5f*CloudShapes.HalfHeight,shape.Name+" fills most of a cloud's footprint, got "+wide+" x "+tall+" m");
        }
    }
    static double Sq(double x)=>x*x;

    // Puff i of a cloud slides to puff i of the shape. A puff the shape lacks shrinks away where it is; one the cloud lacks grows
    // in place.
    static void ACloudMorphsIntoItsShapeAndBack()
    {
        var sky=CloudShapes.For(4242);
        Check(sky.Clouds.Length>0&&CloudShapes.Shapes.Length>0,"setup: clouds and shapes to morph between");
        foreach(var shape in CloudShapes.Shapes)
            for(int c=0;c<sky.Clouds.Length;c++)
            foreach(bool melting in new[]{false,true})
            {
                // Both ways round, so a puff only the cloud has (a shape morphing back into a cloud) is covered too.
                var from=melting?shape.Puffs:sky.Clouds[c].Puffs;var to=melting?sky.Clouds[c].Puffs:shape.Puffs;
                string what=melting?"cloud "+c+" from a "+shape.Name:shape.Name+" from cloud "+c;
                var half=new List<CloudShapes.Puff>();
                for(int i=0;i<CloudShapes.MaxPuffs;i++)
                {
                    CloudShapes.Puff start=CloudShapes.Morph(from,to,i,0),end=CloudShapes.Morph(from,to,i,1),mid=CloudShapes.Morph(from,to,i,.5f);
                    if(i<from.Length)Check(Same(start,from[i]),what+": puff "+i+" starts as the cloud's");
                    else Check(start.Radius==0,what+": puff "+i+" is not there before the morph");
                    if(i<to.Length)Check(Same(end,to[i]),what+": puff "+i+" ends as the shape's");
                    else Check(end.Radius==0,what+": puff "+i+" is gone once the shape shows");
                    if(i>=from.Length&&i<to.Length)Check(start.X==to[i].X&&start.Y==to[i].Y,what+": puff "+i+" grows where the shape needs it");
                    if(i<from.Length&&i>=to.Length)Check(end.X==from[i].X&&end.Y==from[i].Y,what+": puff "+i+" shrinks away where it was");
                    Check(Math.Abs(mid.Radius-(start.Radius+end.Radius)/2)<1e-4&&Math.Abs(mid.X-(start.X+end.X)/2)<1e-4&&Math.Abs(mid.Y-(start.Y+end.Y)/2)<1e-4,what+": puff "+i+" is half way there half way through");
                    if(mid.Radius>0)half.Add(mid);
                }
                Fits(half.ToArray(),what+" half way through");
            }
    }
    static bool Same(CloudShapes.Puff a,CloudShapes.Puff b)=>a.X==b.X&&a.Y==b.Y&&a.Radius==b.Radius;
}
