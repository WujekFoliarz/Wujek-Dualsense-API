namespace Wujek_Dualsense_API
{
    public class Motion
    {
        public class Gyro
        {
            public int Pitch { get; set; }
            public int Yaw { get; set; }
            public int Roll { get; set; }

            public Gyro()
            {
                Pitch = 0;
                Yaw = 0;
                Roll = 0;
            }
        }

        public class Accelerometer
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Z { get; set; }

            public Accelerometer()
            {
                X = 0;
                Y = 0;
                Z = 0;
            }
        }
    }
}
