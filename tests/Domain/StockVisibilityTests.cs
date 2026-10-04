using System;
using System.Collections.Generic;
using Festival.Core;

public static class StockVisibilityTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("Stock visibility: "+message);}
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{PocketStockNeedsToBeOut,StowingEndsOnlyVisibilityStops,TheLastSaleUnitIsOutForTheWholeTalk,OnlyPositiveOwnedExposureIsVisible,StrongerEvidenceSurvivesConcealment})
            try{test();}catch(Exception e){failures.Add(e.Message);}
        if(failures.Count>0)throw new Exception(string.Join("\n",failures));
    }
    static FestivalSimulation Watched(out PlayerState player,out NpcState cop)
    {
        var game=new FestivalSimulation(1);player=game.AddPlayer("owner","Owner");game.State.Phase="Playing";
        game.State.Npcs.Clear();player.X=0;player.Z=5;player.Inventory.Clear();player.EquippedItemId="";
        cop=new NpcState{Id="cop_0",Kind="Cop",X=0,Z=0,Yaw=0};game.State.Npcs.Add(cop);
        game.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);return game;
    }
    static void PocketStockNeedsToBeOut()
    {
        var game=Watched(out var player,out var cop);player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
        game.Tick(.1);Check(cop.Evidence.Count==0&&cop.Mode=="Patrol","free hands conceal pocket stock from a cop facing the player");
        Check(game.Execute(player.Id,new GameCommand{Id="equip_stock",Kind="Equip",ItemId="stock_lsd"}).Accepted,"stock can be equipped");
        game.Tick(.1);Check(cop.Evidence.Exists(e=>e.Kind=="VisibleStock"&&e.PlayerId==player.Id)&&cop.Mode=="Stop","equipping positive stock starts a visibility stop");
    }
    static void StowingEndsOnlyVisibilityStops()
    {
        foreach(string concealment in new[]{"","poi_practice","merch_bag"})
        {
            var game=Watched(out var player,out var cop);player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});player.EquippedItemId="stock_lsd";
            game.Tick(.1);Check(cop.Mode=="Stop","setup: equipped stock starts a stop");
            var drop=new DropState{Id="unrelated_drop",ItemId="stock_mushrooms",Count=1,X=player.X,Z=player.Z};game.State.Drops.Add(drop);
            if(concealment!="")player.Inventory.Add(new ItemStack{ItemId=concealment,Count=1});
            if(concealment!="merch_bag")Check(game.Execute(player.Id,new GameCommand{Id="stow",Kind="Equip",ItemId=concealment}).Accepted,"stock can be stowed");
            game.Tick(.1);Check(cop.Evidence.Count==0&&cop.Mode=="Patrol"&&cop.TargetId=="","stowing stock or concealing it in a bag clears a visibility-only stop");
            Check(player.Inventory.Find(i=>i.ItemId=="stock_lsd").Count==1&&player.Life=="Alive","concealment preserves the stock and player");
            Check(player.NpcSpeech==""&&drop.ClaimedBy=="","stowing retracts the call-out without confiscating nearby unrelated stock");
        }
    }
    static void TheLastSaleUnitIsOutForTheWholeTalk()
    {
        var game=Watched(out var player,out var cop);game.State.Npcs.Remove(cop);
        var buyer=new NpcState{Id="buyer",X=player.X+1,Z=player.Z,Yaw=-90};game.State.Npcs.Add(buyer);
        player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
        Check(game.Execute(player.Id,new GameCommand{Id="sell_pocket_stock",Kind="StartSale",TargetId=buyer.Id,ItemId="stock_lsd"}).Accepted,"pocket stock can still start a sale");
        var sale=game.Interaction(player.InteractionId);Check(sale.ReservedItemId=="stock_lsd"&&player.Inventory.Count==0,"the last unit is reserved outside inventory");
        game.State.Npcs.Add(cop);game.Tick(.1);
        Check(cop.Evidence.Exists(e=>e.Kind=="VisibleStock")&&cop.Mode=="Stop","a cop arriving after a sale begins sees the held reservation");
        Check(game.Execute(player.Id,new GameCommand{Id="stow_during_sale",Kind="Equip"}).Accepted,"stowing is still accepted during a sale");
        game.Tick(1);Check(cop.Mode=="Stop"&&sale.Status=="Active","free hands do not conceal the unit reserved for an active sale");
        var drop=new DropState{Id="unrelated_sale_drop",ItemId="stock_mushrooms",Count=1,X=player.X,Z=player.Z};game.State.Drops.Add(drop);
        player.Inventory.Add(new ItemStack{ItemId="merch_bag",Count=1});game.Tick(.1);
        Check(cop.Evidence.Count==0&&player.NpcSpeech==""&&drop.ClaimedBy=="","a merch bag conceals a reserved unit without claiming unrelated drops");
        player.Inventory.Clear();cop.X=0;cop.Z=0;cop.Yaw=0;game.Tick(.1);
        Check(cop.Mode=="Stop","uncovering the active reservation starts a fresh stop");
        Check(game.Execute(player.Id,new GameCommand{Id="end_sale",Kind="EndChat",TargetId=sale.Id}).Accepted,"the owner can cancel an active sale");
        game.Tick(.1);Check(cop.Mode=="Patrol"&&cop.Evidence.Count==0&&player.Inventory.Find(i=>i.ItemId=="stock_lsd").Count==1,"a cancelled reservation returns to the pocket and is concealed");
    }
    static void OnlyPositiveOwnedExposureIsVisible()
    {
        foreach(int count in new[]{-1,0})
        {
            var game=Watched(out var player,out var cop);player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=count});player.EquippedItemId="stock_lsd";
            game.Tick(.1);Check(cop.Evidence.Count==0,"an equipped nonpositive stack cannot start a stop");
        }
        foreach(string invalid in new[]{"missing","foreign","complete","buy","empty","gear"})
        {
            var game=Watched(out var player,out var cop);
            var sale=new InteractionState{Id="sale",PlayerId=invalid=="foreign"?"other":player.Id,TargetId="buyer",Kind=invalid=="buy"?"Buy":"Sale",Status=invalid=="complete"?"Complete":"Active",ReservedItemId=invalid=="empty"?"":invalid=="gear"?"poi_practice":"stock_lsd"};
            game.State.Interactions.Add(sale);player.InteractionId=invalid=="missing"?"stale_id":sale.Id;
            game.Tick(.1);Check(cop.Evidence.Count==0,"a "+invalid+" sale reference is not the player's exposed stock");
        }
        foreach(string kind in new[]{"stock_lsd","stock_mushrooms"})
        {
            var game=Watched(out var player,out var cop);player.Inventory.Add(new ItemStack{ItemId=kind,Count=1});player.EquippedItemId=kind;
            player.Inventory.Add(new ItemStack{ItemId="merch_bag",Count=1});game.Tick(.1);
            Check(cop.Evidence.Count==0,"a merch bag conceals either equipped stock kind");
        }
    }
    static void StrongerEvidenceSurvivesConcealment()
    {
        foreach(string kind in new[]{"WitnessedDeal","IgnoredStop"})
        {
            var game=Watched(out var player,out var cop);player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});player.EquippedItemId="stock_lsd";
            if(kind=="IgnoredStop")game.Tick(20.2);
            else
            {
                var buyer=new NpcState{Id="buyer",X=player.X+1,Z=player.Z,Yaw=-90};game.State.Npcs.Add(buyer);
                Check(game.Execute(player.Id,new GameCommand{Id="witnessed_sale",Kind="StartSale",TargetId=buyer.Id,ItemId="stock_lsd"}).Accepted,"setup: a cop witnesses the sale");
                Check(game.Execute(player.Id,new GameCommand{Id="cancel_witnessed_sale",Kind="EndChat",TargetId=player.InteractionId}).Accepted,"setup: cancelling refunds the reserved unit");
            }
            var evidence=cop.Evidence.Find(e=>e.PlayerId==player.Id);Check(evidence!=null&&evidence.Kind==kind,"setup: "+kind+" evidence exists");double detainAt=evidence.DetainAt;
            Check(game.Execute(player.Id,new GameCommand{Id="conceal_after_evidence",Kind="Equip"}).Accepted,"stowing remains accepted");player.Inventory.Add(new ItemStack{ItemId="merch_bag",Count=1});
            game.Tick(.1);Check(cop.Evidence.Contains(evidence)&&evidence.Kind==kind&&evidence.DetainAt==detainAt&&cop.Mode=="ArrestWarning","concealment never clears "+kind+" or restarts its clock");
            game.Tick(4.1);Check(player.Life=="Detained"&&player.Cash==10&&!player.Inventory.Exists(i=>i.ItemId=="stock_lsd"),kind+" still detains, fines and confiscates on schedule");
        }
    }
}
