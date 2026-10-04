namespace Festival.Integrations
{
    // Steam/voice providers plug in here; absence must remain visible to the player.
    public interface IVoiceSession
    {
        bool Available { get; }
        string Status { get; }
        void Join(string sessionId);
        void Leave();
        void SetLocalMute(bool muted);
        void SetRemoteMute(string playerId, bool muted);
        void UpdatePosition(string playerId, float x, float z, bool spirit);
    }
    // SET-1: optional so an existing provider still implements the original voice contract unchanged.
    public interface IConfigurableVoiceSession : IVoiceSession
    {
        void Configure(VoiceConfiguration configuration);
    }
    public readonly struct VoiceConfiguration
    {
        public readonly string MicrophoneDevice,Mode;
        // OutputVolume is the voice bus only; playback sources still pass through FestivalTrip's master listener.
        public readonly float InputGain,OutputVolume;
        public VoiceConfiguration(string microphoneDevice,float inputGain,string mode,float outputVolume)
        {
            MicrophoneDevice=microphoneDevice;InputGain=inputGain;Mode=mode;OutputVolume=outputVolume;
        }
    }
    public sealed class UnavailableVoiceSession : IConfigurableVoiceSession
    {
        public bool Available => false;
        public string Status => "Voice unavailable — provider not configured";
        // Saving configuration neither opens a microphone nor makes this provider available.
        public VoiceConfiguration Configuration {get;private set;}=new VoiceConfiguration("",1,"PushToTalk",1);
        public void Configure(VoiceConfiguration configuration) { Configuration=configuration; }
        public void Join(string sessionId) { }
        public void Leave() { }
        public void SetLocalMute(bool muted) { }
        public void SetRemoteMute(string playerId, bool muted) { }
        public void UpdatePosition(string playerId, float x, float z, bool spirit) { }
    }
}
