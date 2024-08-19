namespace Wujek_Dualsense_API
{
    public class LED
    {
        public class Lightbar
        {
            public int R = 0;
            public int G = 0;
            public int B = 0;
        }

        [Flags]
        public enum PlayerLED
        {
            OFF = 0,
            PLAYER_1 = 4,
            PLAYER_2 = 10,
            PLAYER_3 = 21,
            PLAYER_4 = 27,
            ALL = 31
        }

        [Flags]
        public enum MicrophoneLED
        {
            PULSE = 0x2,
            ON = 0x1,
            OFF = 0x0
        }

        [Flags]
        public enum Brightness
        {
            high = 0x0,
            medium = 0x1,
            low = 0x2
        }

        [Flags]
        public enum PulseOptions
        {
            Off = 0x0,
            FadeBlue = 0x1,
            FadeOut = 0x2
        }
    }
}
