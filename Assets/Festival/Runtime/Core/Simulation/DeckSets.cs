using System;
using System.Collections.Generic;
namespace Festival.Core
{
    public sealed partial class FestivalSimulation
    {
        public const string DeckSetKind="DeckSet";
        /// <summary>The one action allowed while a living player owns a plain Ember Playa ride.</summary>
        public static bool CanPlayDeckSet(RoundState s,PlayerState p)
        {
            if(s==null||p==null||s.Phase!="Playing"||s.FestivalIndex!=Festivals.PlayaFestival||!p.Connected||p.Life!="Alive")return false;
            var ride=s.Interactions.Find(i=>i.Id==p.InteractionId&&i.PlayerId==p.Id&&i.Status=="Active"&&i.Kind==RideCarKind);
            return Car(ride)>=0&&!s.Interactions.Exists(i=>i.Status=="Active"&&i.Kind==DeckSetKind&&Car(i)==Car(ride));
        }
        CommandResult BeginDeckSet(PlayerState p)
        {
            if(State.FestivalIndex!=Festivals.PlayaFestival)return Reject("There are no art-car decks at this festival");
            var ride=Interaction(p.InteractionId);
            if(ride==null||ride.PlayerId!=p.Id||ride.Status!="Active"||ride.Kind!=RideCarKind||Car(ride)<0)return Reject("Climb aboard an art car first");
            if(!CanPlayDeckSet(State,p))return Reject("Someone's already on this deck");
            // Retiring a plain ride is not a Cancel: it costs no suspicion and spends no pass.
            ride.Status="Complete";var deck=NewInteraction(p,DeckSetKind,ride.TargetId,0);
            deck.NoteCount=Festivals.DeckSetNotes;deck.BeatSeconds=Festivals.DeckBeatSeconds;
            // One chart preparation window admits the longest authored lane; do not add the checks' external countdown too.
            double lead=2;foreach(var effect in Catalog.Effects)lead=Math.Max(lead,effect.LeadSeconds);
            RhythmChart.InitializeStage(deck,StageMusic.TrackAt(State,p.X,p.Z),WorldSeconds(State),lead);
            RideArtCar(p,Car(deck));CollectDeckWitnesses(deck,p);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RhythmDiagnostic?.Invoke("[Festival.DeckSet] start car="+deck.TargetId+" witnesses="+deck.WitnessIds.Count);
#endif
            return Ok("Play a deck set");
        }
        bool OwnsDeck(InteractionState deck,PlayerState p)=>State.Phase=="Playing"&&State.FestivalIndex==Festivals.PlayaFestival&&p!=null&&p.Connected&&p.Life=="Alive"&&p.InteractionId==deck.Id&&deck.PlayerId==p.Id&&Car(deck)>=0;
        void RetireDeck(InteractionState deck,PlayerState p)
        {
            deck.Status="Interrupted";if(p!=null&&p.InteractionId==deck.Id)p.InteractionId="";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RhythmDiagnostic?.Invoke("[Festival.DeckSet] interrupted");
#endif
        }
        void RestoreDeckSets()
        {
            var cars=new HashSet<int>();foreach(var deck in State.Interactions)if(deck.Kind==DeckSetKind&&deck.Status=="Active")
            {var p=Player(deck.PlayerId);if(!OwnsDeck(deck,p)||!cars.Add(Car(deck)))RetireDeck(deck,p);}
        }
        void CollectDeckWitnesses(InteractionState deck,PlayerState p)
        {
            double range=DustStorm(State)?Math.Min(Festivals.DeckFanRange,Festivals.DustStormSightRange):Festivals.DeckFanRange;
            // Deck spectators hear/see the set from every facing, through the same authoritative geometry as ordinary sight.
            foreach(var n in State.Npcs)if(n.Kind=="Wook"&&!deck.WitnessIds.Contains(n.Id)&&Distance(n.X,n.Z,p.X,p.Z)<=range&&(HasLineOfSight==null||HasLineOfSight(n.X,n.Z,p.X,p.Z)))deck.WitnessIds.Add(n.Id);
        }
        void FinishDeckSet(InteractionState deck,PlayerState p)
        {
            if(deck.Status!="Active"||!OwnsDeck(deck,p)){RetireDeck(deck,p);return;}
            deck.Score=Judge(deck);p.LastRhythmScore=deck.Score;p.Performances++;int delta=RhythmDelta(deck.Score),fresh=0;
            foreach(var n in State.Npcs)if(n.Kind=="Wook"&&deck.WitnessIds.Contains(n.Id)){Adjust(n,p,delta);if(!n.Tipped)fresh++;}
            // Danger has already run for this tick. Commit all three balances and the globally fresh fans exactly once.
            int paid=deck.Score>=.4?(int)Math.Round(Festivals.DeckTipPerHead*fresh*QuotaCrew(State)*Festivals.EncoreScale(State.EncoreTier)*(deck.Score>=.75?1:.5),MidpointRounding.AwayFromZero):0;
            if(paid>0)
            {
                foreach(var n in State.Npcs)if(n.Kind=="Wook"&&deck.WitnessIds.Contains(n.Id)&&!n.Tipped)n.Tipped=true;
                p.Cash+=paid;State.LevelSales+=paid;State.GrossSales+=paid;
                p.DeckTipAmount=paid;p.DeckTipDeliveryId=deck.Id+":tips";p.DeckTipAtWorldSeconds=WorldSeconds(State);p.DeckTipUntilWorldSeconds=p.DeckTipAtWorldSeconds+7;
            }
            deck.Status="Complete";NewInteraction(p,RideCarKind,deck.TargetId,State.DurationSeconds);RideArtCar(p,Car(deck));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RhythmDiagnostic?.Invoke("[Festival.DeckSet] complete score="+deck.Score.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" fresh="+fresh+" paid="+paid+" restoredRide=true");
#endif
        }
        /// <summary>Owner-private latest receipt text; no public speech ID or transcript is created.</summary>
        public static string DeckTipSpeech(RoundState s,PlayerState p)=>p!=null&&p.DeckTipAmount>0&&p.DeckTipDeliveryId!=""&&p.DeckTipAtWorldSeconds>=0&&WorldSeconds(s)<p.DeckTipUntilWorldSeconds?"The crowd tipped "+Festivals.CurrencyName(s.FestivalIndex,p.Ordinal,p.DeckTipAmount)+".":"";
    }
}
