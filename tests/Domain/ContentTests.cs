using System;
using System.Collections.Generic;
using Festival.Core;

public static class ContentTests
{
    private static void Check(bool condition,string message) { if(!condition) throw new Exception("Content: "+message); }
    public static void Run()
    {
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
}
