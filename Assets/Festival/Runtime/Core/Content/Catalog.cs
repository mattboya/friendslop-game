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
            Item("stock_lsd", "Prism tabs", "Sell for up to $10 or take: 60s of warped cues and slower, drifting steps.",5,5,"Stock","Consume/Sale","Self/Wook",0,60,3,true,"lsd"),
            Item("stock_mushrooms", "Moon caps", "Sell for up to $10 or take: 60s of curved cues and slower, drifting steps.",5,5,"Stock","Consume/Sale","Self/Wook",0,60,3,true,"mushrooms"),
            Item("stage_pass", "Stage pass", "At the stage, sustain a 30-second DJ routine to give friends rescue openings.",15,1,"Performance","Stage","StageConsole",0,30,3,true),
            Item("confetti", "Confetti cannon", "Redirect nearby idle or questioning wooks for 4 seconds; cannot stop a swarm.",5,1,"Distraction","World","NearbyWooks",0,4,8,true),
            Item("merch_bag", "Official merch bag", "Hide stock from casual inspection; witnessed sales and searches still count.",5,1,"Equipment","Passive","Self",0,0,0,false),
            Item("stash_box", "Lockable stash box", "Place shared storage; return within 2.5 metres to move cash or stock.",10,1,"Storage","Place","Ground",0,0,2.5,true),
            Item("medical_voucher", "Medical tent voucher", "At the tent, spend 3 seconds removing your newest impairment. Does not revive.",5,1,"Medical","Medical","Self",3,0,3,true),
            Item("map", "Festival map", "Free known-landmark navigation; never reveals concealed NPC intent.",0,1,"Navigation","HUD","Self",0,0,0,false,null,false),
            Item("wristband", "Recovery wristband", "Earned rescue token: carry a fallen friend's identity to the medical tent.",0,1,"RescueToken","Medical","Teammate",10,0,3,true,null,false),
            Item("poi_practice", "Practice poi", "Reusable dance tool: 15% wider good window, capped at 175 ms. Pair with a friend for rescue.",5,1,"Performance","Dance","NearbyWooks",0,0,8,false),
            Item("poi_led", "LED poi", "Reusable dance tool: successful performances reduce suspicion 25% more.",10,1,"Performance","Dance","NearbyWooks",0,0,8,false)
        };
        public static readonly EffectDefinition[] Effects = {
            Effect("lsd","LSD",2,true,"Deterministic irregular low-amplitude trails; fixed receptors."),
            Effect("mushrooms","Mushrooms",2,true,"Gentle curved paths and separate afterimages; fixed receptors."),
            Effect("ecstasy","Ecstasy",1,false,"Shorter visibility lead; bounded brightness; unchanged hit times."),
            Effect("ketamine","Ketamine",3,false,"Longer visibility lead; unchanged hit times."),
            Effect("alcohol","Alcohol",2,false,"Spinning notes converge at fixed receptors; optional camera roll off."),
            Effect("weed","Weed",2,false,"HUD-safe letterbox; host movement multiplier 0.8; same-BPM audio.",0.8)
        };
        public static ItemDefinition FindItem(string id) { foreach(var x in Items) if(x.Id==id) return x; return null; }
        public static EffectDefinition FindEffect(string id) { foreach(var x in Effects) if(x.Id==id) return x; return null; }
        public static bool RareShopItem(string id) => id=="stage_pass"||id=="poi_led"||id=="stash_box";
        public static string ShopTag(string id)
        {
            if(id=="medical_voucher")return "MED VOUCHER";
            if(id=="merch_bag")return "MERCH BAG";
            if(id=="stock_lsd")return "PRISM TABS";
            if(id=="stock_mushrooms")return "MOON CAPS";
            if(id=="poi_practice")return "PRACTICE POI";
            if(id=="poi_led")return "LED POI";
            return FindItem(id)?.Name.ToUpperInvariant()??id.ToUpperInvariant();
        }
        public static int ShopCopies(string id,int crewSize) => RareShopItem(id)?1:Math.Max(2,(crewSize+1)/2);
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
    internal sealed class ContentRandom
    {
        private uint state;
        public ContentRandom(int seed) { state=unchecked((uint)seed)^0x9e3779b9u; if(state==0) state=1; }
        public int Next(int maximum) { state^=state<<13; state^=state>>17; state^=state<<5; return (int)(state%(uint)maximum); }
    }
}
