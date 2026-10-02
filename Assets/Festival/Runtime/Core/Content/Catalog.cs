using System;
using System.Collections.Generic;

namespace Festival.Core
{
    [Serializable] public sealed class ItemDefinition
    {
        public string Id, Name, Description, EffectId, Category, Context, TargetType;
        public int Price, StackLimit, Charges;
        public double SetupSeconds, DurationSeconds, Range;
        public bool Purchasable, ConsumedOnActivation;
        public string AllowedLife = "Alive";
        public string CancellationRule = "Rejected setup consumes nothing; started effects are not refunded.";
    }
    [Serializable] public sealed class EffectDefinition
    {
        public string Id, Name, PresentationContract;
        public double DurationSeconds = 60, LeadSeconds = 2;
        public bool Playable;
        public double MovementMultiplier = 1;
    }
    public static class Catalog
    {
        public const float StageTakeoverX=0f,StageTakeoverZ=26f;
        public const float StageTakeoverStartRange=1.25f;
        public static readonly ItemDefinition[] Items = {
            Item("little_spoon", "Little Spoon", "A tiny spoon on a necklace cord.",1,1,"Equipment","Passive","Self",0,0,0,false),
            // POLO-2, ECON-1: a clean sale pays $10 (FestivalSimulation.PerfectSalePay) at dose 1, and dose, VIP zones, double
            // buyers and encores raise it, so the text names no ceiling; a sloppy sale that lands pays less ($5 at dose 1), so the
            // floor is a clean sale's. "$N" stays a token Ember Playa's odd-object money converts.
            Item("stock_lsd", "Tongue Stamps", "Clean sales pay $10 and up, or take: 60s of warped cues and slower, drifting steps.",5,5,"Stock","Consume/Sale","Self/Wook",0,60,3,true,"lsd"),
            Item("stock_mushrooms", "Fun Guys", "Clean sales pay $10 and up, or take: 60s of curved cues and slower, drifting steps.",5,5,"Stock","Consume/Sale","Self/Wook",0,60,3,true,"mushrooms"),
            Item("stage_pass", "Stage pass", "At the stage, sustain a 30-second DJ routine to give friends rescue openings.",15,1,"Performance","Stage","StageConsole",0,30,3,true),
            Item("confetti", "Confetti cannon", "Redirect nearby idle or questioning wooks for 4 seconds; cannot stop a swarm.",5,1,"Distraction","World","NearbyWooks",0,4,8,true),
            Item("merch_bag", "Official merch bag", "Hide stock from casual inspection; witnessed sales and searches still count.",5,1,"Equipment","Passive","Self",0,0,0,false),
            Item("stash_box", "Lockable stash box", "Place shared storage; return within 2.5 metres to move cash or stock.",10,1,"Storage","Place","Ground",0,0,2.5,true),
            Item("medical_voucher", "Medical tent voucher", "At the tent, spend 3 seconds removing your newest impairment. Does not revive.",5,1,"Medical","Medical","Self",3,0,3,true),
            Item("map", "Festival map", "Free known-landmark navigation; never reveals concealed NPC intent.",0,1,"Navigation","HUD","Self",0,0,0,false,null,false),
            Item("wristband", "Recovery wristband", "Earned rescue token: carry a fallen friend's identity to the medical tent.",0,1,"RescueToken","Medical","Teammate",10,0,3,true,null,false),
            Item("poi_practice", "Practice poi", "Reusable dance tool: 15% wider good window, capped at 175 ms. Pair with a friend for rescue.",5,1,"Performance","Dance","NearbyWooks",0,0,8,false),
            Item("poi_led", "LED poi", "Reusable dance tool: successful performances reduce suspicion 25% more.",10,1,"Performance","Dance","NearbyWooks",0,0,8,false),
            // POLO-1: sold only at Palm Mirage's night-market VIP stall, or given by the VIP guard; never on a shelf.
            Item("vip_wristband", "VIP wristband", "Gets you past Palm Mirage's VIP ropes, where buyers pay double.",15,1,"Access","Passive","Self",0,0,0,false)
        };
        public static readonly EffectDefinition[] Effects = {
            Effect("lsd","Tongue Stamps",2,true,"Deterministic irregular low-amplitude trails; fixed receptors."),
            Effect("mushrooms","Fun Guys",2,true,"Gentle curved paths and separate afterimages; fixed receptors."),
            // TRIP-5: the substance wheel's look-ahead stays mild and fixed whatever the dose: Rolly Pollies' arrows show 1.6 s ahead,
            // Pony Dust's 2.4 s, the rest the usual 2 s.
            Effect("ecstasy","Rolly Pollies",1.6,false,"Shorter visibility lead; bounded brightness; unchanged hit times."),
            Effect("ketamine","Pony Dust",2.4,false,"Longer visibility lead; unchanged hit times."),
            Effect("alcohol","Shot",2,false,"Spinning notes converge at fixed receptors; optional camera roll off."),
            Effect("weed","Couch Lock",2,false,"HUD-safe letterbox; host movement multiplier 0.8; same-BPM audio.",0.8),
            // GAS-1: a Giggle Balloon's gas, taken at camp and gone before any level, so it never meets a rhythm lane.
            Effect("giggle_gas","Giggle Gas",2,false,"Camp only; the hitter's own view and sound pulse; no rule changes.")
        };
        public static ItemDefinition FindItem(string id) { foreach(var x in Items) if(x.Id==id) return x; return null; }
        public static EffectDefinition FindEffect(string id) { foreach(var x in Effects) if(x.Id==id) return x; return null; }
        // The HUD's effects readout: players read display names, never internal ids.
        public static string EffectsLine(List<ActiveEffect> effects) => effects==null||effects.Count==0?"clear":string.Join(", ",effects.ConvertAll(e=>(FindEffect(e.Id)?.Name??e.Id)+" "+e.RemainingSeconds.ToString("0")+"s"));
        public static bool RareShopItem(string id) => id=="stage_pass"||id=="poi_led"||id=="stash_box";
        public static string ShopTag(string id)
        {
            if(id=="medical_voucher")return "MED VOUCHER";
            if(id=="merch_bag")return "MERCH BAG";
            if(id=="poi_practice")return "PRACTICE POI";
            if(id=="poi_led")return "LED POI";
            return FindItem(id)?.Name.ToUpperInvariant()??id.ToUpperInvariant();
        }
        // Stock comes as the weekend table sets it (Festivals.StockCopies), so the day's quota has stock to sell.
        public static int ShopCopies(string id,int crewSize) => RareShopItem(id)?1:FindItem(id)?.Category=="Stock"?Festivals.StockCopies(crewSize):Math.Max(2,(crewSize+1)/2);
        public static WorldPoint ShopPoint(bool camp,int index)
        {
            int column=index%4;
            if(camp)return new WorldPoint(column<2?-5.3f+column*1.4f:3.9f+(column-2)*1.4f,7.8f);
            return new WorldPoint(-20.25f+column*1.5f,index<4?-21.15f:-20.2f);
        }
        public static List<string> VendorOffers(int seed)
        {
            var offers = new List<string> { "little_spoon", "stock_lsd", "stock_mushrooms", "medical_voucher", "poi_practice" };
            var extras = new[] { "stage_pass", "confetti", "merch_bag", "stash_box", "poi_led" };
            var random = new ContentRandom(seed);
            for(int i=extras.Length-1;i>0;i--) { int j=random.Next(i+1); var t=extras[i]; extras[i]=extras[j]; extras[j]=t; }
            for(int i=0;i<3;i++) offers.Add(extras[i]);
            return offers;
        }
        private static ItemDefinition Item(string id,string name,string text,int price,int stack,string category,string context,string target,double setup,double duration,double range,bool consumed,string effect=null,bool purchasable=true)
        { return new ItemDefinition { Id=id,Name=name,Description=text,Price=price,StackLimit=stack,Category=category,Context=context,TargetType=target,SetupSeconds=setup,DurationSeconds=duration,Range=range,ConsumedOnActivation=consumed,Charges=consumed?1:0,EffectId=effect,Purchasable=purchasable }; }
        private static EffectDefinition Effect(string id,string name,double lead,bool playable,string contract,double speed=1)
        { return new EffectDefinition { Id=id,Name=name,LeadSeconds=lead,Playable=playable,PresentationContract=contract,MovementMultiplier=speed }; }
    }
    // Fixed integer algorithm: stable across Unity/.NET versions and machines.
    public sealed class ContentRandom
    {
        private uint state;
        public ContentRandom(int seed) { state=unchecked((uint)seed)^0x9e3779b9u; if(state==0) state=1; }
        public int Next(int maximum) { state^=state<<13; state^=state>>17; state^=state<<5; return (int)(state%(uint)maximum); }
    }
}
