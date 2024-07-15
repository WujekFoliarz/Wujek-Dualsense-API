using Wujek_Dualsense_API;

Dualsense dualsense = new Dualsense(0);
dualsense.Start();
dualsense.SetVibrationType(Vibrations.VibrationType.Haptic_Feedback);
dualsense.Connection.ControllerDisconnected += Connection_ControllerDisconnected;
//dualsense.PlayHaptics(@"audiotest.wav", 0.1f, 1, 1, false);
dualsense.SetLightbar(255, 255, 255);

void Connection_ControllerDisconnected(object? sender, ConnectionStatus.Controller e)
{
    Console.WriteLine("Controller number " + e.ControllerNumber + " was disconnected!");
}

Console.WriteLine("Press enter to exit");
Console.ReadLine();
dualsense.Dispose();