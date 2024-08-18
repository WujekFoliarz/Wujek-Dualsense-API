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

        public event EventHandler<Controller> ControllerConnected;
        public void OnControllerConnect(int ControllerNumber)
        {
            if (this.ControllerConnected != null)
            {
                this.ControllerConnected(this, new Controller(ControllerNumber));
            }
        }
    }
}
