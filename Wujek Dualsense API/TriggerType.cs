namespace Wujek_Dualsense_API
{
    public class TriggerType
    {
        [Flags]
        public enum TriggerModes
        {
            Off = 0x0,
            Rigid = 0x1,
            Pulse = 0x2,
            Rigid_A = 0x1 | 0x20,
            Rigid_B = 0x1 | 0x04,
            Rigid_AB = 0x1 | 0x20 | 0x04,
            Pulse_A = 0x2 | 0x20,
            Pulse_B = 0x2 | 0x04,
            Pulse_AB = 0x2 | 0x20 | 0x04,
            Calibration = 0xFC
        }

        //public int[] TriggerForces = new int[7];
    }
}
