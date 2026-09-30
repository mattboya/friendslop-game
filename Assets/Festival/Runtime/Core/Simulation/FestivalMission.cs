using System;

namespace Festival.Core
{
    public sealed partial class FestivalSimulation
    {
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
