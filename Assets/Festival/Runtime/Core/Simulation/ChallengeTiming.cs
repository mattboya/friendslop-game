namespace Festival.Core
{
    public sealed partial class FestivalSimulation
    {
        // TRIP-9: checks and trust dances leave time for the first arrow to enter even the longest-look-ahead lane.
        // Chart times and the late-input verdict window remain relative to the future start.
        void ScheduleChallengeLeadIn(InteractionState i)
        {
            i.StartSeconds+=ChallengeLeadIn;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Existing opt-in diagnostic callback: only kind, transient round and scheduled time, never private content.
            ChatDiagnostic?.Invoke("ChallengeLeadIn",i.Kind,State.RoundId,i.StartSeconds);
#endif
        }
    }
}
