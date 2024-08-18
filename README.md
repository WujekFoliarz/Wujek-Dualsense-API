# Wujek Dualsense API

Wujek Dualsense API is a .NET library designed to interface with the PlayStation DualSense controller, providing functionalities to control its various features such as haptic feedback, LEDs, triggers, and more.

https://www.nuget.org/packages/Wujek_Dualsense_API

## Features

- Initialize and manage DualSense controllers via USB or Bluetooth
- Control haptic feedback and rumble
- Set lightbar colors and player LEDs
- Adjust microphone and speaker volumes
- Access button states and sensor data (gyroscope, accelerometer)
- Play audio haptics from WAV files
- Convert system audio to haptics in real time

## Getting Started

### Installation

Add the library to your project by including the `Wujek_Dualsense_API` namespace:

```csharp
using Wujek_Dualsense_API;
```

### Examples

```csharp
Dualsense dualsense = new Dualsense(0); // 0 = controller index
dualsense.Connection.ControllerDisconnected += Connection_ControllerDisconnected;
dualsense.Connection.ControllerConnected += Connection_ControllerConnected;

void Connection_ControllerConnected(object? sender, ConnectionStatus.Controller e)
{
  dualsense.SetLightbar(0, 0, 255); // R G B
  dualsense.SetPlayerLED(LED.PlayerLED.PLAYER_1); // The white LEDs below the touchpad
  dualsense.SetMicrophoneLED(LED.MicrophoneLED.OFF); // Microphone LED
  dualsense.SetMicrophoneVolume(35); // Microphone Volume (35 is default)
  dualsense.SetSpeakerVolume(100); // Speaker Volume
  dualsense.SetLeftTrigger(TriggerType.TriggerModes.Pulse_AB, 93, 84, 0, 255, 255, 0, 0); // Example adaptive trigger
  dualsense.SetRightTrigger(TriggerType.TriggerModes.Pulse_B, 14, 255, 0, 14, 255, 0, 0); // Example adaptive trigger
  dualsense.SetVibrationType(Vibrations.VibrationType.Standard_Rumble); // Use standard rumble (Controller audio won't work with this option)
  dualsense.SetStandardRumble(100, 255); // Start vibrations
}

void Connection_ControllerDisconnected(object? sender, ConnectionStatus.Controller e)
{
    Console.WriteLine("Controller number " + e.ControllerNumber + " was disconnected!");
}

Console.ReadLine();

dualsense.Dispose(); // Disconnects from the controller and resets any applied settings
```

### Haptic Feedback example

### To play correctly, the WAV file must be a Stereo 48KHz IEEE Float PCM

```csharp
dualsense.SetVibrationType(Vibrations.VibrationType.Haptic_Feedback); // Use haptic feedback and audio
dualsense.PlayHaptics("player_collar_beep_end_0.wav", 1.0f, 1.0f, 1.0f, true); // (WAV file location, speaker volume, left acustor volume, right acustor volume, cancel previous sounds)
```

## Special thanks to Nefarius
