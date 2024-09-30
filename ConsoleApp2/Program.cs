using System;
using System.Threading;
using Wujek_Dualsense_API;

namespace ConsoleApp2
{
    class Program
    {
        static void Main()
        {
            Dualsense dualsense = new Dualsense(0);
            dualsense.Start();
            dualsense.Connection.ControllerDisconnected += Connection_ControllerDisconnected;
            dualsense.SetMicrophoneLED(LED.MicrophoneLED.PULSE);
            dualsense.SetLightbar(255, 255, 2);
            dualsense.SetAudioOutput(AudioOutput.SPEAKER);
            dualsense.TurnMicrophoneOn();
            dualsense.SetSpeakerVolumeInSoftware(1, 1, 1);
            Thread.Sleep(1000);
            dualsense.SetLightbar(255, 255, 255);
            dualsense.ReadOnly = true;
            Console.WriteLine(dualsense.Battery.State);
            void Connection_ControllerDisconnected(object? sender, ConnectionStatus.Controller e)
            {
                Console.WriteLine("Controller number " + e.ControllerNumber + " was disconnected!");
            }

            Console.ReadLine();
            Console.WriteLine("Press enter to exit");
            dualsense.Dispose();
        }
    }
}