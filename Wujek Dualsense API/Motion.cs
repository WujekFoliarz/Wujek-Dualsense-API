namespace Wujek_Dualsense_API
{
    public class Motion
    {
        public class Gyro
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Z { get; set; }

            public Gyro()
            {
                X = 0;
                Y = 0;
                Z = 0;
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
