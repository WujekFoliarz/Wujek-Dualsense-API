namespace Wujek_Dualsense_API
{
    public class Vibrations
    {
        [Flags]
        public enum VibrationType
        {
            Standard_Rumble = 0xFF,
            Haptic_Feedback = 0xFC
        }
    }
}