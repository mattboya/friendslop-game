using System;
using System.Collections.Generic;

namespace Festival.Core
{
    [Serializable] public sealed class DialogueLine
    {
        // AwkwardId is the awkward twin's key: an affected player is shown the twin, so that is what they acknowledge.
        public string Id, Context, Text, AwkwardText, AwkwardId;
        public string Speaker = "Wook", Tone = "Playful", Prerequisites = "Context eligible", Locale = "en", Status = "Draft";
        public int Difficulty = 1, ContentRevision = 1;
    }
    // DIALOG-4: a challenge's line is built by FestivalLines' grammar from its context's kind, and its awkward twin (what the player
    // blurts instead while affected) from the context's "_awkward" kind. Thousands of each, and DialogueHistory draws each fresh.
    public static class DialogueCatalog
    {
        public static readonly string[] Contexts={"greeting","dance","suspicion","sale","buy","police","medical"};
    }
    [Serializable] public sealed class DialogueHistory
    {
        // Oldest first. Selection never changes history; only acknowledged displayed lines do.
        public List<string> Seen=new List<string>();
        public List<string> AcknowledgedDeliveries=new List<string>();
        // DIALOG-4: a draw this player has seen (Seen holds the last 512 lines shown to them), or that another live challenge holds
        // (reserved), is drawn again from the same stream, up to Redraws more times; then the last draw stands.
        public const int Redraws=5;
        // A line's key: its kind and an FNV-1a hash of its words, short enough for the profile and the session's 64-letter limit.
        public static string Key(string kind,string text) { ulong hash=14695981039346656037UL; foreach(char c in text??"") hash=unchecked((hash^c)*1099511628211UL); return kind+"_"+hash.ToString("x16"); }
        public string Fresh(LineGrammar grammar,string kind,int festival,ContentRandom random,ICollection<string> reserved=null)
        {
            string line="";
            for(int draw=0;draw<=Redraws;draw++) { line=grammar.Line(kind,festival,random); string key=Key(kind,line); if((Seen==null||!Seen.Contains(key))&&(reserved==null||!reserved.Contains(key))) break; }
            return line;
        }
        public DialogueLine Select(string context,int festival,int seed,ICollection<string> reserved=null)
        {
            if(Array.IndexOf(DialogueCatalog.Contexts,context)<0) return null;
            var random=new ContentRandom(seed); string text=Fresh(FestivalLines.Grammar,context,festival,random,reserved),awkward=Fresh(FestivalLines.Grammar,context+"_awkward",festival,random,reserved);
            return new DialogueLine { Id=Key(context,text),Context=context,Text=text,AwkwardText=awkward,AwkwardId=Key(context+"_awkward",awkward),Speaker=context=="police"?"Cop":context=="medical"?"Medic":"Wook",Difficulty=context=="suspicion"||context=="police"?2:1 };
        }
        public void Acknowledge(string id)
        {
            if(string.IsNullOrWhiteSpace(id)) return;
            if(Seen==null) Seen=new List<string>();
            // Repeated delivery acknowledgements cannot advance recency twice.
            if(Seen.Contains(id)) return;
            Seen.Add(id); while(Seen.Count>512) Seen.RemoveAt(0);
        }
        public void Acknowledge(string id,string deliveryId)
        {
            if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(deliveryId)) return;
            if(AcknowledgedDeliveries==null) AcknowledgedDeliveries=new List<string>();
            string key=deliveryId.Length+":"+deliveryId+id;
            if(AcknowledgedDeliveries.Contains(key)) return;
            AcknowledgedDeliveries.Add(key);
            while(AcknowledgedDeliveries.Count>512) AcknowledgedDeliveries.RemoveAt(0);
            if(Seen==null) Seen=new List<string>();
            Seen.RemoveAll(value=>value==id); Seen.Add(id);
            while(Seen.Count>512) Seen.RemoveAt(0);
        }
    }
}
