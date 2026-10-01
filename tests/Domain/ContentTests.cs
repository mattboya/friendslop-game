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
            Check(first.Count==8&&new HashSet<string>(first).Count==8,"eight distinct offers");
            Check(string.Join(",",first)==string.Join(",",second),"stable offers");
            foreach(var id in new[]{"little_spoon","stock_lsd","stock_mushrooms","medical_voucher","poi_practice"}) Check(first.Contains(id),"guaranteed offer");
            foreach(var id in first) Check(Catalog.FindItem(id).Purchasable,"purchasable only");
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
        Check(DialogueCatalog.Lines.Length>=48,"dialogue count"); var texts=new HashSet<string>(); var lineIds=new HashSet<string>();
        foreach(var line in DialogueCatalog.Lines) { Check(texts.Add(line.Text)&&lineIds.Add(line.Id),"original distinct lines"); Check(line.Status=="Draft"&&!string.IsNullOrWhiteSpace(line.AwkwardText),"draft metadata and awkward variant"); }
        foreach(var context in new[]{"greeting","dance","suspicion","sale","police","medical"})
        {
            var history=new DialogueHistory(); var drawn=new HashSet<string>(); string oldest=null;
            for(int i=0;i<8;i++) { var line=history.Select(context,7); Check(drawn.Add(line.Id),"unseen preference"); if(i==0) oldest=line.Id; Check(history.Seen.Count==i,"selection has no mutation"); history.Acknowledge(line.Id); history.Acknowledge(line.Id); Check(history.Seen.Count==i+1,"ack deduplication"); }
            Check(history.Select(context,7).Id==oldest,"least recently seen fallback");
            var restored=new DialogueHistory { Seen=new List<string>(history.Seen) }; Check(restored.Select(context,7).Id==oldest,"restored history");
            var reserved=new HashSet<string>{oldest}; Check(history.Select(context,7,reserved).Id!=oldest,"group reservation avoids duplicate");
            history.Acknowledge(oldest,"new-delivery");
            Check(history.Seen[history.Seen.Count-1]==oldest,"new delivery updates LRU");
            string next=history.Select(context,7).Id; Check(next!=oldest,"exhausted pool rotates");
            history.Acknowledge(oldest,"new-delivery"); Check(history.Select(context,7).Id==next,"duplicate delivery is idempotent");
        }
        var bounded=new DialogueHistory(); for(int i=0;i<600;i++) bounded.Acknowledge("future_"+i); Check(bounded.Seen.Count==512&&bounded.Seen[0]=="future_88","bounded history supports revision IDs");
        Check(bounded.Select("invalid",0)==null,"unknown context explicit");
    }
}
