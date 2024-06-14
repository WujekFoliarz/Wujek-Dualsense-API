using Wujek_Dualsense_API;

Dualsense dualsense = new Dualsense(0);
dualsense.Start();
dualsense.SetLightbar(0, 0, 255);
dualsense.SetPlayerLED(LED.PlayerLED.PLAYER_1);
dualsense.SetMicrophoneLED(LED.MicrophoneLED.OFF);
dualsense.SetMicrophoneVolume(100);
dualsense.SetSpeakerVolume(100);
dualsense.SetLeftTrigger(TriggerType.TriggerModes.Pulse_AB, 93, 84, 0, 255, 255, 0, 0);
dualsense.SetRightTrigger(TriggerType.TriggerModes.Pulse_B, 14, 255, 0, 14, 255, 0, 0);
dualsense.SetVibrationType(Vibrations.VibrationType.Haptic_Feedback);
dualsense.SetStandardRumble(100, 255);


dualsense.PlayHaptics("player_collar_beep_end_0.wav", 0.01f, 1.0f, 1.0f, true);
Thread.Sleep(1000);
dualsense.PlayHaptics("fon2.wav", 0.01f, 1.0f, 1.0f, true);
Thread.Sleep(1000);


if (dualsense.Working)
{
    Console.WriteLine("Shutting down the controller.");
    dualsense.Dispose();
}