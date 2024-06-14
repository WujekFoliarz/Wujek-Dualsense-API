using Wujek_Dualsense_API;

Dualsense dualsense = new Dualsense(0);
dualsense.Start(); // Start listening
dualsense.SetLightbar(255, 0, 0); // R G B
dualsense.SetPlayerLED(LED.PlayerLED.PLAYER_4); // The white LEDs below the touchpad
dualsense.SetMicrophoneLED(LED.MicrophoneLED.ON); // Microphone LED
dualsense.SetMicrophoneVolume(100); // Microphone Volume
dualsense.SetSpeakerVolume(100); // Speaker Volume
dualsense.SetLeftTrigger(TriggerType.TriggerModes.Pulse_AB, 93, 84, 0, 255, 255, 0, 0); // Example adaptive trigger
dualsense.SetRightTrigger(TriggerType.TriggerModes.Pulse_B, 14, 255, 0, 14, 255, 0, 0); // Example adaptive trigger
dualsense.SetVibrationType(Vibrations.VibrationType.Haptic_Feedback); // Use standard rumble (Controller audio won't work with this option)

dualsense.PlayHaptics("pickup_default_0.wav", 1.0f, 1.0f, 1.0f, true);


Thread.Sleep(1000);
dualsense.Dispose();