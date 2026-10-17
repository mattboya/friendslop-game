using System;
namespace Festival.Core
{
    /// <summary>The confetti cannon. At the festival each use spends one and turns calm festivalgoers within ConfettiRange to the
    /// burst for ConfettiSeconds; anyone accusing or swarming ignores it. At camp it fires free and fools nobody. Every accepted
    /// use counts one public burst (PlayerState.VisualConfettiBurstSequence), so every client shows it once.</summary>
    public sealed partial class FestivalSimulation
    {
        public const string ConfettiItem="confetti";
        const double ConfettiRange=8,ConfettiSeconds=4;
        // Use reaches here with an owned cannon: at the festival after its guards, at camp through Apply's one Shopping exception.
        CommandResult Confetti(PlayerState p)
        {
            if(State.Phase=="Shopping")return Burst(p,"Confetti! A camp party trick: your cannon stays loaded");
            Take(p,ConfettiItem,1);int fooled=0,immune=0;
            foreach(var n in State.Npcs)
            {
                if(n.Kind!="Wook"||Distance(p.X,p.Z,n.X,n.Z)>ConfettiRange)continue;
                if(n.Mode=="Swarming"||n.Mode=="Accusing"){immune++;continue;}
                n.DistractedUntil=Math.Max(n.DistractedUntil,State.SimulationSeconds+ConfettiSeconds);n.Yaw=Bearing(n.X,n.Z,p.X,p.Z);fooled++;
            }
            return Burst(p,fooled>0?"Confetti! "+fooled+(fooled==1?" festivalgoer looks":" festivalgoers look")+" away for "+ConfettiSeconds+" s"
                :immune>0?"Confetti! They're too worked up to care":"Confetti! Nobody close enough to fool");
        }
        CommandResult Burst(PlayerState p,string notice)
        {
            p.VisualConfettiBurstSequence++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ChatDiagnostic?.Invoke("Confetti",notice,State.RoundId,State.SimulationSeconds);
#endif
            return Ok(notice);
        }
    }
}
