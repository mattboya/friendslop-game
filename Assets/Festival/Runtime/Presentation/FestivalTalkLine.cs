using Festival.Core;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>
    /// DIALOG-6: the one-line chat the HUD's dialogue card shows (a festivalgoer's Talk, a guard's bark: PlayerState.NpcSpeech), kept
    /// up long enough to read. The host keeps a line until NpcSpeechUntil; the card also keeps it until it has been on screen for
    /// ReadFor(line), counted only while it shows. So a dance or sale starting at once doesn't cut it short (it takes the challenge
    /// card's dialogue until read, then the challenge's own line does), and the menu or the chat window covering the card doesn't use
    /// it up. A newer line waits until the shown one has been up HoldSeconds. DIALOG-3 retires the bark-only Talk; the guards' barks
    /// (SecurityStops) still come this way.
    /// </summary>
    public sealed class FestivalTalkLine
    {
        public const float HoldSeconds=1.5f,ShortestRead=3,LongestRead=7;
        public string Speaker{get;private set;}="";
        public string Line{get;private set;}="";
        // The newest line heard, known by its NpcSpeechUntil (every Talk or bark sets a later one); the shown line's end on the host,
        // and how long it has been on screen.
        string nextSpeaker="",next="";double heard,until;float seen;

        /// <summary>How long a one-line chat stays on screen at least: its reading time (FestivalChatWindow.ReadingSeconds), from
        /// ShortestRead up to the LongestRead the host gives a Talk.</summary>
        public static float ReadFor(string line)=>Mathf.Clamp(FestivalChatWindow.ReadingSeconds(line),ShortestRead,LongestRead);

        /// <summary>Each frame, from the player's own view at `simulationSeconds`: whether the card shows Speaker's Line now.
        /// `challenge`: a rhythm challenge is up, so the line shows only until read. `hidden`: something covers the card, so the line
        /// waits. `dt`: the frame's seconds. A line the host has already dropped never comes up.</summary>
        public bool Shows(PlayerState p,double simulationSeconds,bool challenge,bool hidden,float dt)
        {
            if(!string.IsNullOrEmpty(p.NpcSpeech)&&p.NpcSpeechUntil>simulationSeconds&&p.NpcSpeechUntil!=heard){heard=p.NpcSpeechUntil;nextSpeaker=p.NpcSpeaker;next=p.NpcSpeech;}
            bool unread=Line!=""&&seen<ReadFor(Line),said=Line!=""&&simulationSeconds<until;
            if(next!=""&&(!unread&&!said||seen>=HoldSeconds)){Speaker=nextSpeaker;Line=next;until=heard;next="";seen=0;unread=said=true;}
            bool shows=!hidden&&(unread||said&&!challenge);
            if(shows)seen+=dt;
            return shows;
        }
    }
}
