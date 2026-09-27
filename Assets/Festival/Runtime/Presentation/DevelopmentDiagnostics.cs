#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
namespace Festival.Presentation
{
    public static class DevelopmentDiagnostics
    {
        public static bool Visible;
        public static void Transition(string action, string reason, string roundId, double tick)
        {
            // Transient round IDs only. Never log names, session credentials or voice data.
            Debug.Log($"[Festival] action={action} round={roundId} time={tick:F3} reason={reason}");
        }
    }
}
#endif
