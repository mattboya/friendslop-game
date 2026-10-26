using System.Collections.Generic;
using Festival.Core;

namespace Festival.Presentation
{
    /// <summary>
    /// HUD-8: what a gear key (1-3) or a notch of the mouse wheel does to what you hold, and the line that flashes over the gear bar
    /// for ToastSeconds to show it: "2  •  FUN GUYS", "3  •  EMPTY", or why nothing switches. FestivalHud.UpdateKeyboard reads the
    /// keys and the wheel (FestivalInput.Cycle) and sends the Equip.
    /// </summary>
    public static class FestivalHudGear
    {
        public const float ToastSeconds=1,RepeatSeconds=.12f;
        const string Dot="  •  ";

        /// <summary>Why p's hands can't switch now, or "": inside a tent, car or potty the host takes no Equip, and an unpaid shelf
        /// item in hand would hide the switch (FestivalHands shows it over what's equipped) until it's paid for or put back with G.</summary>
        public static string Blocked(PlayerState p,string drop="G")=>p.CampVisitId!=""?"Not inside the "+(CampFeatures.Find(p.CampVisitId)?.Kind.ToLowerInvariant()??"camp space")
            :p.HeldOfferId!=""?"Holding an unpaid item: pay or put it back ("+drop+")":"";

        /// <summary>The line over the gear bar as a key or the wheel picks slot `slot` (0-2) of `hand`, the gear bar's items: its key
        /// (the slot number when none is given) and what's in it, or why nothing switches (Blocked). "" for a player who isn't up and
        /// free: the host refuses them and says why.</summary>
        public static string Toast(PlayerState p,List<ItemStack> hand,int slot,string drop="G",string key=null)
        {
            if(p.Life!="Alive")return "";
            string blocked=Blocked(p,drop);if(blocked!="")return blocked;
            return (key??(slot+1).ToString())+Dot+(slot<hand.Count?Catalog.FindItem(hand[slot].ItemId)?.Name.ToUpperInvariant()??hand[slot].ItemId:"EMPTY");
        }

        /// <summary>Whether the wheel switches anything for p: all three slots, even empty, and neither a dance (FestivalHudText.StopsByHold)
        /// nor a chat whose questions take 1-3 under way (FestivalChatWindow.TakesReplies; a VIP rope chat has none, so there it does).</summary>
        public static bool Cycles(RoundState s,PlayerState p,int handItems)=>!FestivalHudText.StopsByHold(s,p)&&!FestivalChatWindow.TakesReplies(s,p);

        /// <summary>The step this frame's scroll makes: down +1 (the next slot), up -1 (the previous), else 0. One per notch: a scroll
        /// within RepeatSeconds of the last step (lastStep, updated on a step) is that notch's repeat.</summary>
        public static int Notch(float scroll,float now,ref float lastStep)
        {
            if(scroll==0||now-lastStep<RepeatSeconds)return 0;
            lastStep=now;return scroll<0?1:-1;
        }

        /// <summary>The hand slot a step lands on from slot `from` (-1 with nothing in hand), round and round `count` filled slots.</summary>
        public static int Next(int from,int step,int count)=>from<0?(step>0?0:count-1):((from+step)%count+count)%count;

        /// <summary>The local gear cursor always has three destinations, including empty slots.</summary>
        public static int NextSlot(int from,int step)=>Next(from,step,3);
    }
}
