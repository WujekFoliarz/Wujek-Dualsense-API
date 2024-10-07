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