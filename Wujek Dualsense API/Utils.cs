using HidSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wujek_Dualsense_API
{
    public static class DualsenseUtils
    {
        public static List<string> GetControllerIDs()
        {
            List<string> IDlist = new List<string>();
            DeviceList list = DeviceList.Local;
            List<Device> devices = new List<Device>();

            foreach (var deviceInfo in list.GetHidDevices())
            {
                if (deviceInfo.VendorID == 1356 && deviceInfo.ProductID == 3302) // DualSense
                {
                    IDlist.Add(deviceInfo.DevicePath);
                }
                else if (deviceInfo.VendorID == 1356 && deviceInfo.ProductID == 3570) // DualSense Edge
                {
                    IDlist.Add(deviceInfo.DevicePath);
                }
                else if (deviceInfo.VendorID == 1356 && deviceInfo.ProductID == 1476) // DualShock 4
                {
                    IDlist.Add(deviceInfo.DevicePath);
                }
                else if (deviceInfo.VendorID == 1356 && deviceInfo.ProductID == 2508) // DualShock 4 V2
                {
                    IDlist.Add(deviceInfo.DevicePath);
                }
            }

            return IDlist;
        }
    }
}
