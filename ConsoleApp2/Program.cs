using Wujek_Dualsense_API;

Dualsense dualsense = new Dualsense(0);
dualsense.Start();
dualsense.Connection.ControllerDisconnected += Connection_ControllerDisconnected;
dualsense.SetMicrophoneLED(LED.MicrophoneLED.PULSE);
dualsense.SetLightbar(255, 255, 255);
dualsense.StartSystemAudioToHaptics();
dualsense.SetAudioOutput(AudioOutput.SPEAKER);
dualsense.TurnMicrophoneOn();
dualsense.SetSpeakerVolumeInSoftware(0.1f, 1, 1);
Console.ReadLine();
dualsense.ReinitializeHapticFeedback();

void Connection_ControllerDisconnected(object? sender, ConnectionStatus.Controller e)
{
    Console.WriteLine("Controller number " + e.ControllerNumber + " was disconnected!");
}

Console.ReadLine();
Console.WriteLine("Press enter to exit");
dualsense.Dispose();