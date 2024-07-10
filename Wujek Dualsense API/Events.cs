using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Wujek_Dualsense_API
{
    public class ConnectionStatus
    {
        public class Controller : EventArgs
        {
            public int ControllerNumber { get; set; }

            public Controller(int controllerNumber)
            {
                ControllerNumber = controllerNumber;
            }
        }

        public event EventHandler<Controller> ControllerDisconnected;
        public void OnControllerDisconnect(int ControllerNumber)
        {
            if (this.ControllerDisconnected != null)
            {
                this.ControllerDisconnected(this, new Controller(ControllerNumber));
            }
        }
    }
}
