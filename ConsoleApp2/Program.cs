using System;
using System.Threading;
using Wujek_Dualsense_API;

namespace ConsoleApp2
{
    class Program
    {
        static void Main()
        {
            Dualsense dualsense = new Dualsense(DualsenseUtils.GetControllerIDs()[0]);
            dualsense.Start();
            dualsense.Connection.ControllerDisconnected += Connection_ControllerDisconnected;
            dualsense.SetMicrophoneLED(LED.MicrophoneLED.PULSE);
            dualsense.SetLightbar(0, 255, 255);
            dualsense.SetAudioOutput(AudioOutput.SPEAKER);
            dualsense.TurnMicrophoneOn();
            dualsense.SetSpeakerVolumeInSoftware(1, 1, 1);
            dualsense.SetVibrationType(Vibrations.VibrationType.Standard_Rumble);
            dualsense.PlayHaptics("C:\\Users\\Igor\\Desktop\\smg1_fire1.wav", 0f,1f,1f,false,10);
            dualsense.PlayHaptics(@"C:\Users\Igor\Documents\DualSenseY\audiotest.wav", 0f, 1f, 1f, false,2);

            while (true)
            {
                Console.WriteLine(dualsense.ButtonState.trackPadTouch0.Y);
                Thread.Sleep(100);
            }

            void Connection_ControllerDisconnected(object? sender, ConnectionStatus.Controller e)
            {
                Console.WriteLine("Controller number " + e.ControllerNumber + " was disconnected!");
            }

            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
            dualsense.Dispose();
        }
    }
}