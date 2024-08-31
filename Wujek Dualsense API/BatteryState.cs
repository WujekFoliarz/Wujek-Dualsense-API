using System;

namespace Wujek_Dualsense_API
{
    public class BatteryState
    {
        [Flags]
        public enum State
        {
            POWER_SUPPLY_STATUS_DISCHARGING = 0x0,
            POWER_SUPPLY_STATUS_CHARGING = 0x1,
            POWER_SUPPLY_STATUS_FULL = 0x2,
            POWER_SUPPLY_STATUS_NOT_CHARGING = 0xb,
            POWER_SUPPLY_STATUS_ERROR = 0xf,
            POWER_SUPPLY_TEMP_OR_VOLTAGE_OUT_OF_RANGE = 0xa,
            POWER_SUPPLY_STATUS_UNKNOWN = 0x0
        }

        class Battery
        {
            public BatteryState.State State { get; set; }
            public int Level { get; set; }

            public Battery()
            {
                State = State.POWER_SUPPLY_STATUS_UNKNOWN;
                Level = 0;
            }
        }
    }
}
