using System;
using Festival.Core;

public static class DoseWalkTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("Dose walk: "+message);}
    static PlayerState With(string id,int dose,string substance,double start=3.7)
    {
        var p=new PlayerState();p.Effects.Add(new ActiveEffect{Id=id,Intensity=dose,Substance=substance,StartSeconds=start});return p;
    }
    public static void Run()
    {
        CouchLockAddsOnlyTheSpecifiedSlowdown();
        DoseSwayUsesEachEffectsOwnStartAndTheSharedClamp();
    }
    static void CouchLockAddsOnlyTheSpecifiedSlowdown()
    {
        var plain=new[]{.92f,.88f,.84f,.80f};var couch=new[]{.828f,.792f,.756f,.72f};
        for(int dose=1;dose<=4;dose++)foreach(string substance in FestivalSimulation.Substances)
        {
            var p=With(FestivalSimulation.DoseEffect,dose,substance);
            float expected=substance=="weed"?couch[dose-1]:plain[dose-1];
            Check(Math.Abs(Intoxication.MovementMultiplier(p)-expected)<1e-6,"dose "+dose+" "+substance+" walks at "+expected+", got "+Intoxication.MovementMultiplier(p));
        }
        var stacked=With(FestivalSimulation.DoseEffect,4,"weed");stacked.Effects.Add(new ActiveEffect{Id="weed"});
        Check(Intoxication.MovementMultiplier(stacked)==.65f,"shop and dose Couch Lock still share the .65 floor");
        foreach(int dose in new[]{0,-1})foreach(string substance in FestivalSimulation.Substances)
            Check(Intoxication.MovementMultiplier(With(FestivalSimulation.DoseEffect,dose,substance))==1,"nonpositive "+substance+" dose adds no slowdown");
        for(int dose=1;dose<=4;dose++)
            Check(Math.Abs(Intoxication.MovementMultiplier(With(FestivalSimulation.DoseEffect,dose,""))-plain[dose-1])<1e-6,"legacy unnamed dose keeps its speed");
    }
    static void DoseSwayUsesEachEffectsOwnStartAndTheSharedClamp()
    {
        for(int dose=1;dose<=4;dose++)foreach(string substance in new[]{"lsd","mushrooms"})
        {
            var dosed=With(FestivalSimulation.DoseEffect,dose,substance);var shop=With(substance,1,"");
            bool swayed=false;
            for(int i=0;i<10;i++)
            {
                double seconds=i*.31;float expected=Intoxication.LateralDrift(shop,seconds);swayed|=Math.Abs(expected)>.01f;
                Check(Intoxication.LateralDrift(dosed,seconds)==expected,substance+" dose "+dose+" matches shop sway at "+seconds);
                var differentStart=With(FestivalSimulation.DoseEffect,dose,substance,7.2);var shopStart=With(substance,1,"",7.2);
                Check(Intoxication.LateralDrift(differentStart,seconds)==Intoxication.LateralDrift(shopStart,seconds),"the dose owns its start time");
            }
            Check(swayed,"the shop comparison really sways");
        }
        // At time zero these two different starts put each Tongue Stamps term at its positive peak.
        var stacked=With(FestivalSimulation.DoseEffect,4,"lsd",Math.PI/2);stacked.Effects.Add(new ActiveEffect{Id="lsd",StartSeconds=5*Math.PI/2});
        Check(Intoxication.LateralDrift(stacked,0)==.24f,"shop and dose contribute separately, then clamp at +.24");
        stacked.Effects[0].StartSeconds=3*Math.PI/2;stacked.Effects[1].StartSeconds=7*Math.PI/2;
        Check(Intoxication.LateralDrift(stacked,0)==-.24f,"the lower clamp is -.24");
        foreach(string substance in new[]{"ecstasy","ketamine","weed",""})for(int dose=1;dose<=4;dose++)for(int i=0;i<10;i++)
            Check(Intoxication.LateralDrift(With(FestivalSimulation.DoseEffect,dose,substance),i*.31)==0,substance+" adds no sway");
        foreach(int dose in new[]{0,-1})foreach(string substance in FestivalSimulation.Substances)
            Check(Intoxication.LateralDrift(With(FestivalSimulation.DoseEffect,dose,substance),.7)==0,"nonpositive doses add no sway");
    }
}
