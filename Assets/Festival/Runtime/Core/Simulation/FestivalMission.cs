using System;

namespace Festival.Core
{
    public sealed partial class FestivalSimulation
    {
        // Fixed landmarks let friends learn the grounds; the seed selects the clue order.
        public static WorldPoint CluePoint(int seed,int index)
        {
            bool east=((seed & int.MaxValue)%2==0)==(index==0);
            return east?new WorldPoint(16,-4):new WorldPoint(-16,5);
        }
        public static bool CanReadClues(PlayerState p) => p!=null && p.Life=="Alive" && p.Effects.Exists(e=>e.Id=="lsd"||e.Id=="mushrooms");
        public static string ClueHint(RoundState s,PlayerState p)
        {
            if(!CanReadClues(p)||s.CluesRead>=2)return "";
            var point=CluePoint(s.Seed,s.CluesRead);
            return point.X>0?"The SUN remembers. Find the yellow totem east of the dance path.":"The MOON remembers. Find the blue totem west of the dance path.";
        }
        CommandResult ReadClue(PlayerState p)
        {
            if(State.CluesRead>=2)return Reject("Both clues are already shared");
            if(!CanReadClues(p))return Reject("An affected volunteer must interpret the totem");
            var point=CluePoint(State.Seed,State.CluesRead);
            if(!Near(p,point.X,point.Z))return Reject("Follow the private clue to the correct totem");
            bool solo=State.Players.FindAll(x=>x.Connected).Count==1;
            if(!solo&&!State.Players.Exists(x=>x!=p&&x.Connected&&x.Life=="Alive"&&x.Effects.Count==0&&Near(x,point.X,point.Z,4)))return Reject("Bring a sober friend within four metres to ground the clue");
            return BeginTask(p,"ReadClue",State.CluesRead.ToString(),solo?6:3);
        }
        CommandResult HelpSelf(PlayerState p)
        {
            if(State.Phase!="Playing"||(p.Life!="Downed"&&p.Life!="Detained"))return Reject("Help is available while downed or detained");
            if(State.SimulationSeconds<p.HelpUntil)return Reject("Catch your breath before trying again");
            p.HelpUntil=State.SimulationSeconds+4;
            if(p.Life=="Downed") { Distraction(p,5,2,true); return Ok("You made a scene! Friends have a brief opening to drag you clear."); }
            p.EscapeProgress++;
            if(p.EscapeProgress>=4){p.Life="Alive";p.X=24;p.Z=2;p.EscapeProgress=0;p.RecoveryUntil=State.SimulationSeconds+5;ClearEvidence(p.Id);return Ok("You talked your way out. Meet your crew!");}
            return Ok("Distracting security: "+p.EscapeProgress+" / 4. Friends can still release you sooner.");
        }
    }
}
