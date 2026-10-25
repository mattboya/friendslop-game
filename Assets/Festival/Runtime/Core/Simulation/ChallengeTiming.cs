namespace Festival.Core
{
    public sealed partial class FestivalSimulation
    {
        // TRIP-9: checks and trust dances leave time for the first arrow to enter even the longest-look-ahead lane.
        // Chart times and the late-input verdict window remain relative to the future start.
        void ScheduleStageRhythm(InteractionState i)
        {
            ScheduleChallengeLeadIn(i);
            var p=Player(i.PlayerId);RhythmChart.InitializeStage(i,StageMusic.TrackAt(State,p.X,p.Z),WorldSeconds(State)+ChallengeLeadIn);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RhythmDiagnostic?.Invoke("[Festival.StageChart] track="+i.StageTrack+" anchor="+i.StageFirstHeadWorldSeconds.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+" stride="+i.StageBeatStride.ToString("F1",System.Globalization.CultureInfo.InvariantCulture));
#endif
        }
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
