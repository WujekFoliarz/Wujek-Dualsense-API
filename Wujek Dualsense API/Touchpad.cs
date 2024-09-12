namespace Wujek_Dualsense_API
{
    public class Touchpad
    {
        public int RawTrackingNum { get; set; }
        public bool IsActive { get; set; }
        public int ID { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public Touchpad()
        {
            RawTrackingNum = 0;
            IsActive = false;
            ID = 0;
            X = 0;
            Y = 0;
        }
    }
}
