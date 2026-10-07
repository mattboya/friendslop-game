using System;
using System.Collections.Generic;
namespace Festival.Core
{
    /// <summary>Accepted public utterances, retained through coalesced snapshots. Private checks and outcomes never call this publisher.</summary>
    public sealed partial class FestivalSimulation
    {
        public const int PublicSpeechLimit=8;
        public static bool SpeechActive(RoundState state,PublicSpeechState delivery)=>delivery!=null&&!delivery.Retracted&&!string.IsNullOrEmpty(delivery.DeliveryId)&&!string.IsNullOrEmpty(delivery.Text)&&WorldSeconds(state)<delivery.ExpiresAtWorldSeconds;
        /// <summary>New public projections use world expiry; legacy owner-local lines still read their shifted simulation deadline.</summary>
        public static double SpeechRemainingSeconds(RoundState state,PlayerState player)=>player==null||string.IsNullOrEmpty(player.NpcSpeech)?0:Math.Max(0,(player.SpeechUntilWorldSeconds>=0?player.SpeechUntilWorldSeconds:WorldSeconds(state,player.NpcSpeechUntil))-WorldSeconds(state));
        void PublishSpeech(PlayerState owner,string speakerId,string speaker,string text,double seconds=7,string interactionId="")
        {
            if(string.IsNullOrEmpty(text)||string.IsNullOrEmpty(speakerId))return;
            if(State.PublicSpeech==null)State.PublicSpeech=new List<PublicSpeechState>();
            long sequence=++State.SpeechSequence;double now=WorldSeconds(State);
            var delivery=new PublicSpeechState{DeliveryId=State.RoundId+":speech:"+sequence,Sequence=sequence,SpeakerId=speakerId,Speaker=speaker,Text=text,StartedAtWorldSeconds=now,ExpiresAtWorldSeconds=now+seconds,OwnerId=owner.Id,InteractionId=interactionId};
            State.PublicSpeech.Add(delivery);while(State.PublicSpeech.Count>PublicSpeechLimit)State.PublicSpeech.RemoveAt(0);
            ProjectSpeech(owner,delivery,State.SimulationSeconds+seconds);
        }
        void RetractSpeech(PlayerState owner,string interactionId,bool securityOnly=false)
        {
            if(State.PublicSpeech==null)return;
            foreach(var delivery in State.PublicSpeech)
            {
                if(delivery.OwnerId!=owner.Id||securityOnly&&delivery.Speaker!="SECURITY"||!securityOnly&&delivery.InteractionId!=interactionId)continue;
                delivery.Retracted=true;delivery.Text="";
                if(owner.SpeechDeliveryId==delivery.DeliveryId){owner.NpcSpeech="";owner.NpcSpeaker="";owner.SpeechById="";}
            }
        }
        static void ProjectSpeech(PlayerState player,PublicSpeechState delivery,double legacyUntil)
        {
            player.NpcSpeaker=delivery.Speaker;player.NpcSpeech=delivery.Text;player.SpeechById=delivery.SpeakerId;
            player.SpeechDeliveryId=delivery.DeliveryId;player.SpeechSequence=delivery.Sequence;player.NpcSpeechUntil=legacyUntil;
            player.SpeechAtWorldSeconds=delivery.StartedAtWorldSeconds;player.SpeechUntilWorldSeconds=delivery.ExpiresAtWorldSeconds;
        }
    }
}
