using Festival.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Festival.Presentation
{
    /// <summary>
    /// DIALOG-6: the one-line chat the HUD's dialogue card shows (a festivalgoer's Talk, a guard's bark: PlayerState.NpcSpeech), kept
    /// up long enough to read. The host keeps a line until NpcSpeechUntil; the card also keeps it until it has been on screen for
    /// ReadFor(line), counted only while it shows. So a dance or sale starting at once doesn't cut it short (it takes the challenge
    /// card's dialogue until read, then the challenge's own line does), and the menu or the chat window covering the card doesn't use
    /// it up. A newer line waits until the shown one has been up HoldSeconds. DIALOG-3 retires the bark-only Talk; the guards' barks
    /// (SecurityStops) still come this way. K2: a line the host takes back (it empties NpcSpeech: a security stop over) goes at
    /// once, shown or waiting, so "Last chance" never comes up after the guard has waved you on.
    /// </summary>
    public sealed class FestivalTalkLine
    {
        public const float HoldSeconds=1.5f,ShortestRead=3,LongestRead=7;
        FestivalSpeechDeliveryTracker.Delivery selected,challengeLine;
        string challengeId="";
        public FestivalSpeechDeliveryTracker.Delivery Delivery=>selected;
        public bool IsChallenge=>ReferenceEquals(selected,challengeLine);
        public void Reset(){selected=challengeLine=null;challengeId="";}
        /// <summary>The challenge reads accepted IDs in order. Covering it consumes unseen deliveries silently.</summary>
        public FestivalSpeechDeliveryTracker.Delivery Follow(FestivalSpeechDeliveryTracker tracker,PlayerState player,InteractionState challenge,bool hidden,float dt)
        {
            if(selected!=null&&!ReferenceEquals(tracker.Find(selected.Id),selected)||challengeLine!=null&&!ReferenceEquals(tracker.Find(challengeLine.Id),challengeLine))Reset();
            if(challenge==null)
            {
                if(challengeLine!=null&&!challengeLine.IsPublic)tracker.RetractLocal(challengeLine.Id);
                challengeId="";challengeLine=null;
                selected=tracker.Find(player.SpeechDeliveryId);
                if(selected!=null&&(selected.Retracted||!selected.WorldActive||hidden)){selected.HudConsumed=true;selected=null;}
                Observe(tracker,hidden);return null;
            }
            foreach(var delivery in tracker.Deliveries)
                if(delivery.IsPublic&&!delivery.HudObserved&&!delivery.HudConsumed&&delivery.WorldActive
                    &&(delivery.SpeakerId==challenge.TargetId||delivery.SpeakerId==player.Id||delivery.Id==player.SpeechDeliveryId))delivery.HudPending=true;
            if(challengeId!=challenge.Id)
            {
                if(challengeLine!=null&&!challengeLine.IsPublic)tracker.RetractLocal(challengeLine.Id);
                challengeId=challenge.Id;
                challengeLine=tracker.Find(player.SpeechDeliveryId);
                if(challengeLine==null||challengeLine.Text!=challenge.DialogueText)
                    challengeLine=string.IsNullOrEmpty(challenge.DialogueText)?null:tracker.RegisterLocal(tracker.RoundId+":challenge:"+challenge.Id,challenge.TargetId,challenge.Kind=="Dj"?"STAGE CREW":"FESTIVALGOER",challenge.DialogueText,tracker.WorldSeconds,tracker.WorldSeconds+Mathf.Max(ReadFor(challenge.DialogueText),(float)challenge.DurationSeconds));
                if(selected!=null&&(selected.Retracted||selected.HudConsumed||selected.SpeakerId!=challenge.TargetId&&selected.SpeakerId!=player.Id))selected=null;
                if(selected==null)foreach(var delivery in tracker.Deliveries)
                    if(delivery.IsPublic&&!delivery.HudObserved&&!delivery.HudConsumed&&delivery.WorldActive
                        &&(delivery.SpeakerId==challenge.TargetId||delivery.SpeakerId==player.Id)){selected=delivery;break;}
            }
            if(selected==challengeLine&&selected!=null&&selected.HudReadSeconds>=HoldSeconds)
                foreach(var delivery in tracker.Deliveries)
                    if(delivery!=challengeLine&&delivery.HudPending&&!delivery.Retracted&&delivery.WorldActive){selected=null;break;}
            if(selected!=null&&(selected.Retracted||selected.HudConsumed||selected!=challengeLine&&selected.HudReadSeconds>=ReadFor(selected.Text)))
            {selected.HudConsumed=true;selected=null;}
            if(hidden)
            {
                if(selected!=null&&!selected.WorldActive){selected.HudConsumed=true;selected=null;}
                Observe(tracker,true);return null;
            }
            if(selected==null)foreach(var delivery in tracker.Deliveries)
                if(delivery.HudPending&&!delivery.HudConsumed&&!delivery.Retracted&&delivery.WorldActive){selected=delivery;break;}
            if(selected==null&&challengeLine!=null&&!challengeLine.Retracted&&!challengeLine.HudConsumed)selected=challengeLine;
            if(selected!=null)selected.HudPending=false;
            Observe(tracker,false);
            if(selected!=null)selected.HudReadSeconds+=Mathf.Max(0,dt);
            return selected;
        }
        void Observe(FestivalSpeechDeliveryTracker tracker,bool hidden)
        {
            foreach(var delivery in tracker.Deliveries)
            {
                if(hidden&&delivery!=selected){delivery.HudConsumed=true;delivery.HudPending=false;}
                delivery.HudObserved=true;
            }
        }
        public string Speaker{get;private set;}="";
        public string Line{get;private set;}="";
        // The newest line heard, known by its NpcSpeechUntil (every Talk or bark sets a later one); the shown line's end on the host,
        // and how long it has been on screen; and the round they were said in.
        string nextSpeaker="",next="",round="";double heard,until;float seen;

        /// <summary>How long a one-line chat stays on screen at least: its reading time (FestivalChatWindow.ReadingSeconds), from
        /// ShortestRead up to the LongestRead the host gives a Talk.</summary>
        public static float ReadFor(string line)=>Mathf.Clamp(FestivalChatWindow.ReadingSeconds(line),ShortestRead,LongestRead);

        /// <summary>Each frame, from the player's own view of `round` (RoundState.RoundId) at `simulationSeconds`: whether the card
        /// shows Speaker's Line now. `challenge`: a rhythm challenge is up, so the line shows only until read. `hidden`: something
        /// covers the card, so the line waits. `dt`: the frame's seconds. A line the host has already dropped never comes up, nor
        /// does one from an earlier game.</summary>
        public bool Shows(string round,PlayerState p,double simulationSeconds,bool challenge,bool hidden,float dt)
        {
            // The HUD outlives a game: after Leave, a new Host or Join is a new round on a clock from 0, where the last game's
            // line would read as still said.
            if(round!=this.round){this.round=round;Speaker=Line=nextSpeaker=next="";heard=until=0;seen=0;}
            // Only the newest line heard can be taken back: the one waiting, or the one shown if none waits.
            if(string.IsNullOrEmpty(p.NpcSpeech)){if(next!="")next="";else if(until==heard)Line="";}
            else if(p.NpcSpeechUntil>simulationSeconds&&p.NpcSpeechUntil!=heard){heard=p.NpcSpeechUntil;nextSpeaker=p.NpcSpeaker;next=p.NpcSpeech;}
            bool unread=Line!=""&&seen<ReadFor(Line),said=Line!=""&&simulationSeconds<until;
            if(next!=""&&(!unread&&!said||seen>=HoldSeconds)){Speaker=nextSpeaker;Line=next;until=heard;next="";seen=0;unread=said=true;}
            bool shows=!hidden&&(unread||said&&!challenge);
            if(shows)seen+=dt;
            return shows;
        }
    }

    /// <summary>Keep the complete legacy UI literal laid out while the shared delivery controls visible glyph quads.</summary>
    public sealed class FestivalSpeechReveal : BaseMeshEffect
    {
        int visible=int.MaxValue;
        public int VisibleCharacters
        {
            get=>visible;
            set{if(visible==value)return;visible=value;graphic.SetVerticesDirty();}
        }
        public override void ModifyMesh(VertexHelper mesh)
        {
            if(!IsActive()||visible>=mesh.currentVertCount/4)return;
            var vertex=new UIVertex();
            for(int at=Mathf.Max(0,visible)*4;at<mesh.currentVertCount;at++)
            {mesh.PopulateUIVertex(ref vertex,at);vertex.color.a=0;mesh.SetUIVertex(vertex,at);}
        }
    }
}
