using System;
using System.Collections.Generic;
using Festival.Core;

// POLO-2: Palm Mirage polish. The stock's text names what a clean sale pays and no ceiling, since dose, VIP zones, double
// buyers and encores all raise it.
public static class PoloPolishTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("PoloPolish: "+message);}

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{StockSaysASaleStartsAtACleanSalesPay})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" polo polish test(s) failed:\n"+string.Join("\n",failures));
    }

    // A clean sale at dose 1 pays PerfectSalePay; a VIP sale pays double that and more with dose, a double buyer and an encore.
    // The "$N" token stays, so Ember Playa's odd-object money (FestivalHudText.MoneyText) converts it.
    static void StockSaysASaleStartsAtACleanSalesPay()
    {
        foreach(var id in new[]{"stock_lsd","stock_mushrooms"})
        {
            string text=Catalog.FindItem(id).Description;
            Check(text.StartsWith("Sells for $"+FestivalSimulation.PerfectSalePay+" and up, or take: "),id+" names a clean sale's pay as the floor: "+text);
            Check(!text.Contains("up to"),id+" names no ceiling: "+text);
        }
    }
}
