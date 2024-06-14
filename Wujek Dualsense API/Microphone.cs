namespace Wujek_Dualsense_API
{
    public class Microphone
    {
        [Flags]
        public enum MicrophoneStatus
        {
            ON = 0,
            OFF = 0x10
        }
    }
}
