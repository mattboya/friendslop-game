using System;
using System.Collections.Generic;
using Festival.Core;

public static class ContentTests
{
    private static void Check(bool condition,string message) { if(!condition) throw new Exception("Content: "+message); }
    public static void Run()
    {
        DoseFlight();
        Check(Catalog.Items.Length==13,"thirteen item definitions (POLO-1 added the VIP wristband)");
        Check(Catalog.Effects.Length==7,"seven effects (GAS-1 added camp's Giggle Gas)");
        var ids=new HashSet<string>();
        foreach(var item in Catalog.Items) { Check(ids.Add(item.Id),"unique item IDs"); Check(item.Price>=0&&item.StackLimit>0,"valid economics"); Check(!string.IsNullOrEmpty(item.CancellationRule),"use contract"); }
        for(int seed=-100;seed<100;seed++)
        {
            var first=Catalog.VendorOffers(seed); var second=Catalog.VendorOffers(seed);
            Check(first.Count==8&&new HashSet<string>(first).Count==8,"six gear offers and two distinct starters");
            Check(string.Join(",",first)==string.Join(",",second),"stable offers");
            foreach(var id in new[]{"little_spoon","medical_voucher","poi_practice"}) Check(first.Contains(id),"guaranteed offer");
            foreach(var id in first) Check(Catalog.FindItem(id).Purchasable,"purchasable only");
            // ECON-6: starters append to the existing gear slots; the night market remains gear only.
            for(int i=0;i<6;i++) Check(Catalog.FindItem(first[i]).Category!="Stock","first six offers remain gear");
            Check(first[6]=="stock_lsd"&&first[7]=="stock_mushrooms","starter slots follow the gear");
        }
        foreach(double offset in new[]{0,0.08,0.081,0.15,0.151}) foreach(int sign in new[]{-1,1})
        {
            var chart=RhythmChart.Create(17,1,.5,0); var judge=new RhythmJudge(chart);
            string grade=judge.Submit(chart.Notes[0].Direction,chart.Notes[0].TimeSeconds+offset*sign);
            Check(grade==(offset<=0.08?"Perfect":offset<=0.15?"Good":"Extra"),"exact grading boundary "+offset);
        }
        var baseline=RhythmChart.Create(42,8,.5,3); var perfect=new RhythmJudge(baseline);
        Check(baseline.Notes.Count>8,"a Night 2 chart mixes eighths, jumps and runs into its eight beats (DANCE-5)");
        foreach(var n in baseline.Notes) perfect.Submit(n.Direction,n.TimeSeconds);
        Check(perfect.Score==1,"perfect score"); Check(!perfect.Complete,"timer owns completion");
        perfect.Submit(baseline.Notes[0].Direction,baseline.Notes[0].TimeSeconds);
        Check(perfect.Hits==baseline.Notes.Count&&Math.Abs(perfect.Score-.9)<1e-9,"duplicate note never awards twice, full normalized extra penalty");
        var oneGood=RhythmChart.Create(11,1,.5,0);var goodJudge=new RhythmJudge(oneGood);
        goodJudge.Submit(oneGood.Notes[0].Direction,oneGood.Notes[0].TimeSeconds+.1);
        Check(Math.Abs(goodJudge.Score-.6)<1e-9,"good is worth 0.6 normalized points");
        perfect.Advance(baseline.DurationSeconds); Check(perfect.Complete,"timer completes");
        var silent=new RhythmJudge(baseline); silent.Advance(baseline.DurationSeconds+silent.GoodWindowSeconds); // a run can end a sixteenth before the chart does
        Check(silent.Complete&&silent.Misses==baseline.Notes.Count&&silent.Score==0,"all misses still complete once the last note's window has closed");
        var bad=new RhythmJudge(baseline); Check(bad.Submit(0,double.NaN)=="Miss"&&bad.Hits==0,"nonfinite rejected");
        foreach(var effect in Catalog.Effects) foreach(bool reduced in new[]{false,true})
        {
            var judge=new RhythmJudge(baseline);
            foreach(var note in baseline.Notes)
            {
                for(int i=0;i<=20;i++) { var p=EffectPresentation.Path(effect.Id,note.Id,i/20.0,reduced); Check(!double.IsNaN(p.X)&&Math.Abs(p.X)<=0.25&&p.Alpha>=0&&p.Alpha<=1,"bounded path"); }
                var end=EffectPresentation.Path(effect.Id,note.Id,1,reduced); Check(end.X==0&&end.Y==0,"receptor convergence");
                Check(judge.Submit(note.Direction,note.TimeSeconds)=="Perfect","presentation preserves scoring");
            }
            Check(judge.Score==1,"every effect preserves score");
        }
        // DIALOG-4: every challenge line is built from parts (FestivalLinesTests), so a context has thousands, not 8. A player's
        // history still steers the draw: a line they've seen is drawn again from the same stream, five times at most.
        foreach(var context in DialogueCatalog.Contexts) for(int festival=0;festival<Festivals.Count;festival++)
        {
            var history=new DialogueHistory(); var drawn=new HashSet<string>(); string first=null,last=null;
            for(int i=0;i<=DialogueHistory.Redraws;i++) { var line=history.Select(context,festival,7); Check(drawn.Add(line.Id),"unseen preference, from one seed"); if(i==0) first=line.Id; last=line.Id; Check(line.Status=="Draft"&&!string.IsNullOrWhiteSpace(line.AwkwardText),"draft metadata and awkward variant"); Check(history.Seen.Count==i,"selection has no mutation"); history.Acknowledge(line.Id); history.Acknowledge(line.Id); Check(history.Seen.Count==i+1,"ack deduplication"); }
            Check(history.Select(context,festival,7).Id==last,"after five redraws the last draw stands, seen or not");
            var restored=new DialogueHistory { Seen=new List<string>(history.Seen) }; Check(restored.Select(context,festival,7).Id==last,"restored history");
            var reserved=new HashSet<string>{first}; Check(new DialogueHistory().Select(context,festival,7,reserved).Id!=first,"group reservation avoids duplicate");
            history.Acknowledge(first,"new-delivery");
            Check(history.Seen[history.Seen.Count-1]==first,"new delivery updates LRU");
            var before=string.Join(",",history.Seen); history.Acknowledge(first,"new-delivery"); Check(string.Join(",",history.Seen)==before,"duplicate delivery is idempotent");
        }
        var bounded=new DialogueHistory(); for(int i=0;i<600;i++) bounded.Acknowledge("future_"+i); Check(bounded.Seen.Count==512&&bounded.Seen[0]=="future_88","bounded history supports revision IDs");
        Check(bounded.Select("invalid",0,0)==null,"unknown context explicit");
    }
    // TRIP-13: the four-argument caller and every shop look keep their original 21-point output.
    private static string OldFlightHash(int? dose=null)
    {
        var bytes=new List<byte>();
        foreach(var effect in Catalog.Effects)foreach(bool reduced in new[]{false,true})foreach(int id in new[]{0,1,7,23})for(int sample=0;sample<=20;sample++)
        {
            var v=dose.HasValue?EffectPresentation.Path(effect.Id,id,sample/20.0,reduced,dose.Value):EffectPresentation.Path(effect.Id,id,sample/20.0,reduced);
            foreach(double value in new[]{v.X,v.Y,v.Rotation,v.Alpha})bytes.AddRange(BitConverter.GetBytes(value));
        }
        using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-","");
    }
    private static void DoseFlight()
    {
        const string oldFlight="81075C4B8A7A465DA045CE5C5FF5B6D7E5756F2B15067ED4E78EE6DD8698E11B";
        Check(OldFlightHash()==oldFlight&&OldFlightHash(0)==oldFlight&&OldFlightHash(-1)==oldFlight,"default and nonpositive doses preserve the original 21-point output byte for byte");
        Check(Math.Abs(EffectPresentation.Path("mushrooms",7,.125,false,4).Y-.9307042300821634)<1e-12,"Fun Guys slow the first eighth of the flight");
        Check(Math.Abs(EffectPresentation.Path("ecstasy",7,.5,false,4).Y-.6701230223067764)<1e-12,"Rolly Pollies hang low before rushing the target");
        Check(Math.Abs(EffectPresentation.Path("ketamine",7,.4,false,4).Y-2.0/3)<1e-12&&EffectPresentation.Path("ketamine",7,.4,false,4).Y==EffectPresentation.Path("ketamine",7,.6,false,4).Y,"Pony Dust stands still a third of the way up");
        Check(Math.Abs(EffectPresentation.Path("weed",7,.5,false,4).Y-.32987697769322355)<1e-12,"Couch Lock creeps over the last stretch");
        Check(EffectPresentation.Strength(-1)==0&&EffectPresentation.Strength(0)==0&&EffectPresentation.Strength(1)==.4&&EffectPresentation.Strength(2)==.6&&EffectPresentation.Strength(3)==.8&&EffectPresentation.Strength(4)==1&&EffectPresentation.Strength(99)==1,"the shared strength scale is exactly 0/.4/.6/.8/1 and clamps to its ends");
        foreach(var effect in Catalog.Effects)foreach(int dose in new[]{0,1,2,3,4})foreach(bool reduced in new[]{false,true})foreach(int id in new[]{0,1,7,23})for(int lane=0;lane<4;lane++)
        {
            double previous=1;
            for(int sample=0;sample<=1000;sample++)
            {
                var v=EffectPresentation.Path(effect.Id,id,sample/1000.0,reduced,dose,lane);
                Check(v.Y>=0&&v.Y<=previous+1e-12&&Math.Abs(v.X)<=.25&&Math.Abs(v.Lane)<=1,"bounded monotonic flight: "+effect.Id+" dose "+dose);
                if(sample==0)Check(v.Y==1,"the arrow begins at the bottom");
                if(sample==1000)Check(v.X==0&&v.Y==0&&v.Lane==0,"the arrow lands exactly on its receptor");
                if(effect.Id=="lsd")Check(lane+v.Lane>=0&&lane+v.Lane<=3,"Tongue Stamps stays within the panel for the actual Direction");
                var full=EffectPresentation.Path(effect.Id,id,sample/1000.0,false,dose,lane);
                Check(v.Y==full.Y&&v.Lane==full.Lane,"reduced motion retains full gameplay twists");
                previous=v.Y;
            }
            var chart=RhythmChart.Create(42,1,.5,0);var judge=new RhythmJudge(chart);var note=chart.Notes[0];
            Check(judge.Submit(note.Direction,note.TimeSeconds)=="Perfect","dosed paths preserve the perfect hit time");
        }
        foreach(string effect in new[]{"lsd","mushrooms","ecstasy","ketamine","weed"})
        {
            double low=0,high=0;
            for(int sample=1;sample<100;sample++)
            {
                double u=sample/100.0;var plain=EffectPresentation.Path(effect,7,u,false);
                var one=EffectPresentation.Path(effect,7,u,false,1);var four=EffectPresentation.Path(effect,7,u,false,4);
                low=Math.Max(low,Math.Abs(one.Y-plain.Y)+Math.Abs(one.Lane));high=Math.Max(high,Math.Abs(four.Y-plain.Y)+Math.Abs(four.Lane));
            }
            Check(low>0&&high>low,"each substance bends the flight more at dose 4 than dose 1: "+effect);
        }
        foreach(bool reduced in new[]{false,true})foreach(int lane in new[]{0,3})
        {
            var half=EffectPresentation.Path("lsd",7,.5,reduced,4,lane);
            Check(Math.Abs(half.Lane)>=.5,"Tongue Stamps is still at least half a lane away halfway up");
            Check(EffectPresentation.Path("lsd",7,.75,reduced,4,lane).Lane==0,"Tongue Stamps reaches its own lane at 75 percent");
        }
        for(int dose=1;dose<=4;dose++)
        {
            // At .4 every dose is stalled. At .6 only the full dose remains stalled; smaller doses already caught up.
            Check(Math.Abs(EffectPresentation.Path("ketamine",7,.4,false,dose).Y-2.0/3)<1e-12,"every Pony Dust dose stalls at one third");
            double later=EffectPresentation.Path("ketamine",7,.6,false,dose).Y;
            Check(dose==4?Math.Abs(later-2.0/3)<1e-12:later<2.0/3,"Pony Dust stall duration grows with dose");
        }
        var defaultLane=EffectPresentation.Path("lsd",7,.5,false,4);var explicitLane=EffectPresentation.Path("lsd",7,.5,false,4,1);
        Check(defaultLane.Lane==explicitLane.Lane,"omitting Direction preserves the optional lane 1 default");
        foreach(string effect in new[]{"lsd","mushrooms","ecstasy","ketamine","weed"})
        {
            Check(EffectPresentation.Path(effect,7,double.NaN,false,4).Y==1,"NaN progress starts at the bottom");
            var end=EffectPresentation.Path(effect,7,double.PositiveInfinity,false,4);
            Check(end.X==0&&end.Y==0&&end.Lane==0,"overshoot clamps to the exact endpoint");
        }
        var start=EffectPresentation.Path("lsd",7,0,false,1,0);
        Check(Math.Abs(start.Lane-.4)<1e-12,"Tongue Stamps dose 1 starts .4 lanes inward on lane 0");
    }

}
