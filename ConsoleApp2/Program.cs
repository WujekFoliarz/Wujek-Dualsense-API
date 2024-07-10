using Wujek_Dualsense_API;

Dualsense dualsense = new Dualsense(0);
dualsense.Start();
dualsense.SetVibrationType(Vibrations.VibrationType.Haptic_Feedback);
dualsense.Connection.ControllerDisconnected += Connection_ControllerDisconnected;

void Connection_ControllerDisconnected(object? sender, ConnectionStatus.Controller e)
{
    Console.WriteLine("Controller number " + e.ControllerNumber + " was disconnected!");
}

Console.ReadLine();