using NAudio.CoreAudioApi;
using NAudio.Wave;
using Nefarius.Utilities.DeviceManagement.Extensions;
using Nefarius.Utilities.DeviceManagement.PnP;
using System.Text.RegularExpressions;
using Windows.Devices.Enumeration.Pnp;

namespace Wujek_Dualsense_API
{
    public class HapticFeedback : IDisposable
    {
        private MMDevice device;
        private WasapiOut hapticStream;
        public BufferedWaveProvider bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(48000, 32, 2));
        public float speakerPlaybackVolume = 1;
        public float leftActuatorVolume = 1;
        public float rightActuatorVolume = 1;
        

        public HapticFeedback(int ControllerNumber, string AudioDeviceID)
        {
            MMDeviceEnumerator mmdeviceEnumerator = new MMDeviceEnumerator();
            foreach (MMDevice mmdevice in mmdeviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                mmdevice.GetPropertyInformation(NAudio.CoreAudioApi.Interfaces.StorageAccessMode.Read);
                Regex rg = new Regex(@"[{\d}]*\.(.*)$");
                PropertyStoreProperty controllerDeviceId = mmdevice.Properties[PropertyKeys.PKEY_Device_ControllerDeviceId];
                Match deviceIdMatch = rg.Match((string)controllerDeviceId.Value);
                string instancePath = deviceIdMatch.Groups[1].Value;

                if (instancePath.ToLower().Substring(0, AudioDeviceID.Length - 4).Replace("mi_00", "") == AudioDeviceID.ToLower().Substring(0, AudioDeviceID.Length - 4).Replace("mi_03", ""))
                {
                    device = mmdevice;
                    break;
                }
            }

            if (device == null || device.State == DeviceState.NotPresent || device.State == DeviceState.Unplugged)
            {
                return;
            }

            bufferedWaveProvider.BufferLength = 5000000; // 5MB buffer
            bufferedWaveProvider.ReadFully = true;
            hapticStream = new WasapiOut(device, AudioClientShareMode.Shared, true, 10);

            MultiplexingWaveProvider multiplexingWaveProvider = new MultiplexingWaveProvider(new BufferedWaveProvider[] {
                bufferedWaveProvider,
            }, 4);

            multiplexingWaveProvider.ConnectInputToOutput(0, 0);
            multiplexingWaveProvider.ConnectInputToOutput(0, 1);
            multiplexingWaveProvider.ConnectInputToOutput(0, 2);
            multiplexingWaveProvider.ConnectInputToOutput(0, 3);
            hapticStream.Init(multiplexingWaveProvider);
            Thread t = new Thread(new ThreadStart(Play));
            t.Start();
        }

        private void Play()
        {
            hapticStream.Play();
        }

        public void setVolume(float speaker, float left, float right)
        {
            speakerPlaybackVolume = speaker;
            leftActuatorVolume = left;
            rightActuatorVolume = right;

            try
            {
                hapticStream.AudioStreamVolume.SetChannelVolume(0, speakerPlaybackVolume);
                hapticStream.AudioStreamVolume.SetChannelVolume(1, speakerPlaybackVolume);
                hapticStream.AudioStreamVolume.SetChannelVolume(2, leftActuatorVolume);
                hapticStream.AudioStreamVolume.SetChannelVolume(3, rightActuatorVolume);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentOutOfRangeException("Volume must be between 0.0 and 1.0.");
            }
        }

        public void Dispose()
        {
            hapticStream.Dispose();
            bufferedWaveProvider.ClearBuffer();
        }
    }
}
