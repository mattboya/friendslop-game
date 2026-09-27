using System;
using System.Collections.Generic;

namespace Festival.Core
{
    [Serializable] public sealed class DialogueLine
    {
        public string Id, Context, Text, AwkwardText;
        public string Speaker = "Wook", Tone = "Playful", Prerequisites = "Context eligible", Locale = "en", Status = "Draft";
        public int Difficulty = 1, ContentRevision = 1;
    }
    public static class DialogueCatalog
    {
        public static readonly DialogueLine[] Lines = Build();
        public static DialogueLine Find(string id) { foreach(var line in Lines) if(line.Id==id) return line; return null; }
        private static DialogueLine[] Build()
        {
            var result=new List<DialogueLine>();
            Add(result,"greeting",new[] {
                "Your hat looks like it knows the next set.|My hat has declined to comment.",
                "We saved a patch of grass for good company.|I brought a company. It sells grass.",
                "You made it! The entrance keeps moving in my memory.|I entered through the concept of a fence.",
                "Excellent wristband energy. Very convincing wrist.|I practiced this wrist all week.",
                "Are you with the people carrying the enormous pear?|I represent several smaller pears.",
                "Welcome to our extremely unofficial meeting spot.|I have prepared unofficial minutes.",
                "That bass just rearranged my afternoon.|My afternoon is now in alphabetical order.",
                "You look like you could recommend a good cloud.|I only review indoor clouds."
            });
            Add(result,"dance",new[] {
                "Follow my feet; ignore my elbows.|My elbows have started a separate project.",
                "This move is called finding your tent at midnight.|Mine is called apologizing to the wrong tent.",
                "Small steps, enormous confidence.|I ordered those sizes the other way around.",
                "Catch the beat before it wanders off.|I caught a nearby administrative rhythm.",
                "Give the speakers a respectful little nod.|I bowed to a bin by mistake.",
                "Let's make a circle with an excellent exit plan.|I have made a rectangle of concern.",
                "The next four beats belong to us.|I leased the wrong four beats.",
                "Match this shuffle and we'll call it choreography.|I call mine a footwear inspection."
            });
            Add(result,"suspicion",new[] {
                "You keep counting us. Is it a dance thing?|Yes. This dance requires an accurate census.",
                "Why are your pockets standing at attention?|They were promoted this morning.",
                "That's a very official way to hold a lemonade.|I have a permit for this elbow.",
                "You asked where the exits were before the stages.|I am a passionate exit enthusiast.",
                "Nobody here calls it a recreational gathering.|My recreational vocabulary is buffering.",
                "Relax your shoulders; they look like they're taking notes.|They insist these are only minutes.",
                "Who exactly sent you to our little dance patch?|A committee of extremely relaxed people.",
                "You nodded on the silence. Explain the confidence.|I was following the premium invisible beat."
            });
            Add(result,"sale",new[] {
                "One little festival curiosity, at the price we agreed.|I have forgotten which end of a deal this is.",
                "Keep it simple: one unit, one exchange.|I prepared a thirty-slide explanation.",
                "You have the stock? I've got the agreed cash.|I brought a receipt for my confidence.",
                "Let's finish before my friends relocate again.|My friends relocated my ability to count.",
                "No rush. Just keep your hands where we can see them.|These are my presentation hands.",
                "Same terms as the sign, no surprise extras.|The surprise is my sudden formal accent.",
                "We can trade after this beat lands.|I believe the beat missed its connecting flight.",
                "That's the one. Nice and straightforward.|I have made it diagonally complicated."
            });
            Add(result,"police",new[] {
                "Let's step aside and clear up what I observed.|Certainly. Which side is the clearing-up side?",
                "Please keep the walkway clear while we talk.|I was supervising its walkability.",
                "I need a straight answer about that exchange.|My answer has one small roundabout.",
                "Your dance score doesn't change what I witnessed.|Could it at least improve the paperwork font?",
                "Stay here while I explain the next step.|I have several unrelated steps prepared.",
                "That merchandise bag isn't an exemption from a search.|The bag will be disappointed in its career.",
                "We'll handle this calmly. Listen to the instructions.|My calm is arriving on the next shuttle.",
                "Your friends can help through the release desk.|Can they bring my less suspicious shoulders?"
            });
            Add(result,"medical",new[] {
                "Take a breath. We'll handle one problem at a time.|I numbered my problems with confetti.",
                "A wristband tells us which friend needs help.|Good. My description was mostly hat noises.",
                "The voucher clears an effect; revival is separate.|I filed those under one very large feeling.",
                "No cash? There is a recovery task you can do.|At last, a task priced in effort.",
                "Stay near the tent until we finish helping.|I will be aggressively stationary.",
                "Your friend gets another chance, not another wallet.|Their wallet has chosen a solo career.",
                "The release marker goes to the holding desk.|I nearly gave it to a very official tree.",
                "You're back on your feet. Give yourself a moment.|These feet and I are renegotiating terms."
            });
            return result.ToArray();
        }
        private static void Add(List<DialogueLine> target,string context,string[] texts)
        {
            for(int i=0;i<texts.Length;i++) { var pair=texts[i].Split('|'); target.Add(new DialogueLine { Id=context+"_"+(i+1).ToString("D2"),Context=context,Text=pair[0],AwkwardText=pair[1],Speaker=context=="police"?"Cop":context=="medical"?"Medic":"Wook",Difficulty=context=="suspicion"||context=="police"?2:1 }); }
        }
    }
    [Serializable] public sealed class DialogueHistory
    {
        // Oldest first. Selection never changes history; only acknowledged displayed lines do.
        public List<string> Seen=new List<string>();
        public List<string> AcknowledgedDeliveries=new List<string>();
        public DialogueLine Select(string context,int seed) => Select(context,seed,null);
        public DialogueLine Select(string context,int seed,ICollection<string> reserved)
        {
            var eligible=new List<DialogueLine>();
            foreach(var line in DialogueCatalog.Lines) if(line.Context==context) eligible.Add(line);
            if(eligible.Count==0) return null;
            var available=eligible.FindAll(line=>reserved==null||!reserved.Contains(line.Id));
            if(available.Count==0) available=eligible;
            var unseen=available.FindAll(line=>Seen==null||!Seen.Contains(line.Id));
            if(unseen.Count>0) return unseen[new ContentRandom(seed).Next(unseen.Count)];
            DialogueLine oldest=available[0]; int index=Seen.IndexOf(oldest.Id);
            foreach(var line in available) { int position=Seen.IndexOf(line.Id); if(position<index) { oldest=line; index=position; } }
            return oldest;
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
