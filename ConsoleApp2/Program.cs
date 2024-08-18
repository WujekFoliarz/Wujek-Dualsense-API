using Wujek_Dualsense_API;

Dualsense dualsense = new Dualsense(0);
dualsense.Connection.ControllerDisconnected += Connection_ControllerDisconnected;
dualsense.Connection.ControllerConnected += Connection_ControllerConnected;

void Connection_ControllerConnected(object? sender, ConnectionStatus.Controller e)
{
    Console.WriteLine("Controller number " + e.ControllerNumber + " was connected!");
    dualsense.SetMicrophoneLED(LED.MicrophoneLED.PULSE);
    dualsense.SetSpeakerVolumeInSoftware(0, 1, 1);
    dualsense.SetAudioOutput(AudioOutput.SPEAKER);
    dualsense.SetPlayerLED(LED.PlayerLED.PLAYER_4);
    dualsense.StartSystemAudioToHaptics();
    dualsense.SetHeadsetVolume(100);
    dualsense.SetLightbar(255, 255, 255);
    dualsense.SetLEDBrightness(LED.Brightness.HIGH);
    dualsense.SetLeftTrigger(TriggerType.TriggerModes.Pulse, 0,0,0,0,0,0,0);
}
while (true)
{
    if (dualsense.ButtonState.square)
    {
        Console.Write("X");
    }
    Thread.Sleep(1);
}

void Connection_ControllerDisconnected(object? sender, ConnectionStatus.Controller e)
{
    Console.WriteLine("Controller number " + e.ControllerNumber + " was disconnected!");
}

Console.WriteLine("Press enter to exit");
Console.ReadLine();
dualsense.Dispose();