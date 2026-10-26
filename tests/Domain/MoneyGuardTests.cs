using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// No change to cash, the stash, SALES or carried items may be silent (Matt, 2026-10-23). Each Core write is listed in Announced with
// the message its players see; a write with no row fails, and so does a row whose write is gone. Paths are from the repo root.
public static class MoneyGuardTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("MoneyGuard: "+message);}
    // { file, the write as Scan prints it, who sees it: the message }. A file with the same write twice lists it twice.
    static readonly string[][] Announced={
        new[]{"CashFinds.cs","State.Drops.RemoveAll(d=>d.CashFind)","nobody: an unclaimed find vanishes at the level end and was never anyone's cash"},
        new[]{"CloudClue.cs","State.StashCash+=SecretStashCash","finder: CloudStashNotice; crew: 'STASH  •  +$15  •  Ash found the cloud's stash'"},
        new[]{"Confetti.cs","Take(p,ConfettiItem,1)","user: the reply 'Confetti! N festivalgoers look away for 4 s'"},
        new[]{"Conversations.cs","Take(p,c.ItemId,1)","seller: the unit sits under the sale card, and the sale result says where it went"},
        new[]{"Conversations.cs","p.Cash+=payout","seller: the sale result 'SOLD Tongue Stamps  •  +$10' with the stamp and the cash flash"},
        new[]{"Conversations.cs","State.LevelSales+=payout","seller: 'SOLD Tongue Stamps  •  +$10' and the SALES flash; crew: 'SALES  •  +$10  •  Ash sold Tongue Stamps'"},
        new[]{"Conversations.cs","Add(p,item,1)","seller: 'NO SALE  •  Tongue Stamps back in your hand' (or They walk off, You walked off, SALE OFF)"},
        new[]{"Danger.cs","p.Inventory.RemoveAll(x=>Stock(x.ItemId))","detainee: 'SECURITY FINE  •  −$10  •  SECURITY TOOK 2 Tongue Stamps'"},
        new[]{"Danger.cs","s.Count++","detainee: the unit under the sale card counts in 'SECURITY TOOK 2 Tongue Stamps' (a stack already taken, in the list the line reads)"},
        new[]{"Danger.cs","p.Cash-=fine","detainee: 'SECURITY FINE  •  −$10  •  SECURITY TOOK ...' (no line when the fine is 0 and nothing is taken)"},
        new[]{"Danger.cs","p.Cash=0","everyone: the death reel 'DROPPED $40'"},
        new[]{"Danger.cs","p.Inventory.Clear()","the dead: GearLossNotice 'Your gear dropped where you fell'"},
        new[]{"Danger.cs","p.Wristbands.Clear()","the dead: GearLossNotice, also when only wristbands drop"},
        new[]{"Danger.cs","p.Cash-=10","payer: 'RELEASE FOR Sam  •  −$10'"},
        new[]{"Danger.cs","p.Cash-=10","payer: 'REVIVAL FOR Sam  •  −$10' or 'YOUR REVIVAL  •  −$10'"},
        new[]{"Danger.cs","State.StashCash+=State.SurvivorBonus","crew: 'STASH  •  +$10  •  survivor bonus'"},
        new[]{"DeckSets.cs","p.Cash+=paid","rider: DeckTipSpeech 'The crowd tipped $X.' and the cash flash"},
        new[]{"DeckSets.cs","State.LevelSales+=paid","rider: DeckTipSpeech; crew: 'SALES  •  +$X  •  Ash's deck set'"},
        new[]{"Economy.cs","p.Cash-=item.Price","buyer: 'Oh, hey, thanks!  •  −$5  •  POCKET UPDATED' (PocketUpdated)"},
        new[]{"Economy.cs","Add(p,c.ItemId,1)","buyer: the reply 'X bought for $5' and the pocket toast"},
        new[]{"Economy.cs","Take(p,c.ItemId,1)","taker: 'TOOK Tongue Stamps  •  1 left'"},
        new[]{"Economy.cs","Take(p,c.ItemId,1)","placer: 'STASH BOX PLACED  •  the crew can stash here'"},
        new[]{"Economy.cs","Take(p,item,1)","dropper: the reply 'Dropped X', or in a security stop 'DITCHED 1 Tongue Stamps  •  1 left'"},
        new[]{"Economy.cs","Take(p,c.ItemId,1)","actor: the reply 'X left in the shared stash'"},
        new[]{"Economy.cs","stash.Items.Add(newItemStack{ItemId=c.ItemId,Count=1})","crew: 'STASH  •  +1 Tongue Stamps  •  Ash'"},
        new[]{"Economy.cs","item.Count++","crew: 'STASH  •  +1 Tongue Stamps  •  Ash'"},
        new[]{"Economy.cs","p.Cash+=d.Count","picker: FoundCashNotice for a find, 'PICKED UP CASH  •  +$40' for a pile"},
        new[]{"Economy.cs","p.Wristbands.Add(d.OwnerId)","picker: 'PICKED UP Sam's wristband'"},
        new[]{"Economy.cs","Add(p,d.ItemId,d.Count)","picker: 'PICKED UP 2 Fun Guys'"},
        new[]{"Economy.cs","State.Drops.Remove(d)","picker: the PICKED UP line of what it held"},
        new[]{"Economy.cs","Take(p,c.ItemId,c.Amount)","giver: the reply 'Recipient must accept handoff'"},
        new[]{"Economy.cs","Add(p,offer.ItemId,offer.Amount)","receiver: 'GOT 1 Tongue Stamps  •  from Ash'; giver: 'HANDED OVER Tongue Stamps  •  Sam took it'"},
        new[]{"Economy.cs","Add(p,t.ItemId,t.Amount)","giver: 'HANDOFF ENDED  •  Tongue Stamps back in your hand' (or dropped at your feet)"},
        new[]{"Economy.cs","p.Cash-=c.Amount","depositor: 'INTO THE STASH  •  −$5'"},
        new[]{"Economy.cs","State.StashCash+=c.Amount","crew: 'STASH  •  +$5  •  Ash'"},
        new[]{"Economy.cs","State.StashCash-=c.Amount","crew: 'STASH  •  −$5  •  Ash'"},
        new[]{"Economy.cs","p.Cash+=c.Amount","withdrawer: 'FROM THE STASH  •  +$5'"},
        new[]{"Economy.cs","Take(p,c.ItemId,c.Amount)","depositor: 'INTO THE STASH  •  1 Tongue Stamps'"},
        new[]{"Economy.cs","stash.Items.Add(newItemStack{ItemId=c.ItemId,Count=c.Amount})","crew: 'STASH  •  +1 Tongue Stamps  •  Ash'"},
        new[]{"Economy.cs","item.Count+=c.Amount","crew: 'STASH  •  +1 Tongue Stamps  •  Ash'"},
        new[]{"Economy.cs","item.Count-=c.Amount","crew: 'STASH  •  −1 Tongue Stamps  •  Ash'"},
        new[]{"Economy.cs","Add(p,c.ItemId,c.Amount)","withdrawer: 'FROM THE STASH  •  1 Tongue Stamps'"},
        new[]{"Economy.cs","stash.Items.Remove(item)","crew: 'STASH  •  −1 Tongue Stamps  •  Ash'"},
        new[]{"FestivalSimulation.cs","p.Inventory.Add(newItemStack{ItemId=item,Count=count})","nobody directly: the Add helper, each caller has its own row"},
        new[]{"FestivalSimulation.cs","s.Count+=count","nobody directly: the Add helper, each caller has its own row"},
        new[]{"FestivalSimulation.cs","s.Count-=count","nobody directly: the Take helper, each caller has its own row"},
        new[]{"FestivalSimulation.cs","p.Inventory.Remove(s)","nobody directly: the Take helper, each caller has its own row"},
        new[]{"FestivalTwists.cs","p.Cash-=band.Price","buyer: the reply 'VIP wristband bought for $X', then the pocket toast with −$X"},
        new[]{"FestivalTwists.cs","Add(p,VipWristband,1)","buyer: the reply 'VIP wristband bought for $X'"},
        new[]{"FestivalTwists.cs","Add(p,VipWristband,1)","guest: 'VIP WRISTBAND  •  you're on the list'"},
        new[]{"GiggleTanks.cs","Add(p,GiggleTankItem,1)","grabber: the reply 'Giggle Tank! Carry it home ...'; crew: GiggleTankGrabbed"},
        new[]{"GiggleTanks.cs","Take(p,GiggleTankItem,1)","carrier: GiggleTankHome"},
        new[]{"GiggleTanks.cs","State.StashCash+=Festivals.GiggleTankCash","carrier: GiggleTankHome; crew: 'STASH  •  +$25  •  Ash brought the Giggle Tank home'"},
        new[]{"GiggleTanks.cs","p.Inventory.RemoveAll(x=>x.ItemId==GiggleTankItem)","carrier: 'GIGGLE TANK LOST'"},
        new[]{"Interactions.cs","Take(p,\"stage_pass\",1)","DJ: 'STAGE PASS USED'"},
        new[]{"Interactions.cs","p.Cash+=5","doer: 'LOST PROPERTY RETURNED  •  +$5'"},
        new[]{"Interactions.cs","Take(p,\"medical_voucher\",1)","patient: 'MEDICAL VOUCHER USED  •  Pony Dust treated'"},
        new[]{"Interactions.cs","p.Wristbands.Remove(spirit.Id)","reviver: 'WRISTBAND USED  •  Sam is back' (none for a self-revival)"},
        new[]{"MoneyNews.cs","p.Cash+=i.ReservedCash","payer: 'NO DEAL  •  +$5', 'RELEASE CALLED OFF  •  +$10' or 'REVIVAL CALLED OFF  •  +$10'"},
        new[]{"MysteryCups.cs","State.LevelSales+=b.Pot","everyone: the minigame card '+$pot', counted as sales"},
        new[]{"MysteryCups.cs","p.Cash+=share","everyone: the minigame card 'Called it: $X each'"},
        new[]{"MysteryCups.cs","State.StashCash+=b.Pot-share*crew.Count","everyone: the minigame card '$Y to the stash'"},
        new[]{"SecurityStops.cs","State.Drops.Remove(d)","crew: 'SECURITY TOOK the ditched Tongue Stamps'"},
        new[]{"SplitObjective.cs","State.StashCash+=FriendReward","crew: 'STASH  •  +$20  •  Sam found a lost friend'"},
        new[]{"StockBuys.cs","p.Cash-=price","buyer: the price is held under the talk card; the end says BuyReply or 'NO DEAL  •  +$5'"},
        new[]{"StockBuys.cs","Add(p,item,1)","buyer: BuyReply 'Bought Tongue Stamps for $5' (full hands: 'HANDS FULL  •  ... dropped at your feet')"},
        new[]{"Visions.cs","State.StashCash+=SecretStashCash","finder: VisionStashNotice; crew: 'STASH  •  +$15  •  Ash found a hidden stash'"},
        new[]{"Weekend.cs","next.StashCash=old.StashCash","nobody: the next level carries the stash over unchanged"},
        new[]{"Weekend.cs","after.Cash=before.Cash","nobody: the next level carries cash over unchanged"},
        new[]{"Weekend.cs","after.Inventory=before.Inventory","nobody: the next level carries gear over unchanged"},
        new[]{"Weekend.cs","stash.Items.RemoveAll(x=>x.ItemId==GiggleTankItem)","crew: 'STASH  •  −1 Giggle Tank  •  tanks never leave the level'"},
    };
    // Cash, StashCash and LevelSales writes anywhere in Core, by any assignment operator or ++/-- either side; the inventory helpers
    // (Add/Take) in Simulation, whatever their arguments; carried items, wristbands and stash items changing in place, any stack's
    // Count included (List.Count is read-only, so each write is an ItemStack's); and a drop leaving the ground (a pickup, or
    // security taking ditched stock).
    static readonly Regex Money=new Regex(@"[\w.\[\]]*\.(?:Cash|StashCash|LevelSales)\s*(?:[-+*/%&|^]=|<<=|>>=|\?\?=|=(?!=)|\+\+|--)[^;{}]*|(?:\+\+|--)\s*(?:[\w.\[\]]*\.(?:Cash|StashCash|LevelSales)|\w+\.Count)\b");
    static readonly Regex Helper=new Regex(@"(?<![\w.])(?:Take|Add)\((?!PlayerState)(?:[^()]|\((?:[^()]|\([^()]*\))*\))*\)");
    static readonly Regex Lists=new Regex(@"[\w.]*\.(?:Inventory|Wristbands|Items)\.(?:Add|Remove|RemoveAll|Clear|Insert)\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*\)|[\w.]*\.Drops\.(?:Remove|RemoveAll|Clear)\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*\)|[\w.]*\.Inventory\s*=(?!=)[^;{}]*|\b\w+\.Count\s*(?:[-+*/]=|\+\+|--|=(?!=))[^;{}]*");
    // Known limit: a string literal that holds ";//" hides the rest of its line from the scan.
    static readonly Regex Comments=new Regex(@"/\*[\s\S]*?\*/|^\s*//.*$|(?<=[;{})])\s*//.*$",RegexOptions.Multiline);
    public static void Run()
    {
        ScanFindsEachKindOfWrite();
        EveryCoreWriteIsAnnounced();
    }
    // The scan itself: a source with one write of each kind yields each once, and reads, comments and the helper's own
    // signature yield nothing.
    static void ScanFindsEachKindOfWrite()
    {
        string source="static void Add(PlayerState p,string item,int count){}\n// p.Cash+=1; a comment\nvoid X(PlayerState p){if(p.Cash<5)return;p.Cash+=5;State.StashCash-=2;State.LevelSales=0;Take(p,\"a\",1);Add(p,i.ItemId,1);p.Inventory.RemoveAll(x=>Stock(x.ItemId));p.Wristbands.Remove(id);stash.Items.Add(new ItemStack{ItemId=\"a\",Count=1});item.Count++;State.Drops.Remove(d);after.Inventory=before.Inventory;} // p.Cash-=9;";
        var found=Scan("Assets/Festival/Runtime/Core/Simulation/X.cs",source);
        string[] expected={"p.Cash+=5","State.StashCash-=2","State.LevelSales=0","Take(p,\"a\",1)","Add(p,i.ItemId,1)","p.Inventory.RemoveAll(x=>Stock(x.ItemId))","p.Wristbands.Remove(id)","stash.Items.Add(newItemStack{ItemId=\"a\",Count=1})","item.Count++","State.Drops.Remove(d)","after.Inventory=before.Inventory"};
        Check(string.Join(" | ",found)==string.Join(" | ",expected),"the scan finds each kind of write once, got: "+string.Join(" | ",found));
        // Every compound operator and the prefix forms, any stack's count, and helper arguments that call a method.
        source="void Y(PlayerState p){if(p.Cash>=5&&p.Cash!=0&&p.Inventory.Count<3&&s.Count==0)return;State.StashCash/=2;p.Cash*=2;p.Cash%=3;++p.Cash;--State.LevelSales;s.Count+=count;stack.Count--;s.Count=0;++s.Count;Add(p,SellerStock(i.TargetId),1);Take(p,FirstStock(p),1);}";
        found=Scan("Assets/Festival/Runtime/Core/Simulation/Y.cs",source);
        expected=new[]{"State.StashCash/=2","p.Cash*=2","p.Cash%=3","++p.Cash","--State.LevelSales","s.Count+=count","stack.Count--","s.Count=0","++s.Count","Add(p,SellerStock(i.TargetId),1)","Take(p,FirstStock(p),1)"};
        Check(string.Join(" | ",found)==string.Join(" | ",expected),"the scan finds every form of write once, got: "+string.Join(" | ",found));
        Check(Scan("Assets/Festival/Runtime/Core/Content/X.cs","Add(chart,beat,0,lane,beatSeconds);").Count==0,"Add/Take outside Simulation are not the inventory helpers");
    }
    static void EveryCoreWriteIsAnnounced()
    {
        var found=new List<string>();
        var paths=Directory.GetFiles("Assets/Festival/Runtime/Core","*.cs",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);
        foreach(var path in paths)
            foreach(var write in Scan(path.Replace('\\','/'),File.ReadAllText(path)))found.Add(Path.GetFileName(path)+"|"+write);
        var listed=new List<string>();foreach(var entry in Announced)listed.Add(entry[0]+"|"+entry[1].Replace(" ",""));
        var silent=new List<string>(found);foreach(var key in listed)silent.Remove(key);
        var gone=new List<string>(listed);foreach(var key in found)gone.Remove(key);
        var report=new StringBuilder();
        foreach(var key in silent){var part=key.Split('|');report.Append("\n  new[]{\""+part[0]+"\",\""+part[1].Replace("\\","\\\\").Replace("\"","\\\"")+"\",\"<who sees it: the message>\"},");}
        string goneReport=gone.Count==0?"":"\nAnnounced lists "+gone.Count+" write(s) no longer in Core; remove or update: "+string.Join(", ",gone);
        Check(silent.Count==0,silent.Count+" Core write(s) change cash, the stash, SALES or carried items with no entry in MoneyGuardTests.Announced. Give each one a message the players it affects see, with the amount, then list it:"+report+goneReport);
        Check(gone.Count==0,goneReport.TrimStart('\n'));
    }
    // Every write in one file's source, whitespace removed, in source order.
    static List<string> Scan(string path,string source)
    {
        string code=Comments.Replace(source,"");
        var hits=new List<(int At,string Text)>();
        foreach(Match m in Money.Matches(code))hits.Add((m.Index,m.Value));
        foreach(Match m in Lists.Matches(code))hits.Add((m.Index,m.Value));
        if(path.Contains("/Simulation/"))foreach(Match m in Helper.Matches(code))hits.Add((m.Index,m.Value));
        hits.Sort((a,b)=>a.At.CompareTo(b.At));
        return hits.ConvertAll(h=>Regex.Replace(h.Text,@"\s",""));
    }
}
