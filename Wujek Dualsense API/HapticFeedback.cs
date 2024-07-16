using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Text.RegularExpressions;

namespace Wujek_Dualsense_API
{
    public class HapticFeedback : IDisposable
    {
        private MMDevice device;
        private MMDevice mmdeviceplayback;
        private WasapiOut hapticStream;
        private WasapiLoopbackCapture wasapiLoopbackCapture = null;
        private MMDeviceEnumerator mmdeviceEnumerator = new MMDeviceEnumerator();
        public BufferedWaveProvider bufferedWaveProvider = new BufferedWaveProvider(WaveFormat.CreateCustomFormat(WaveFormatEncoding.IeeeFloat, 48000, 2, 32, 8, 8));
        public float speakerPlaybackVolume = 1;
        public float leftActuatorVolume = 1;
        public float rightActuatorVolume = 1;
        public bool SystemAudioPlayback = false;
        

        public HapticFeedback(int ControllerNumber, string AudioDeviceID)
        {
            foreach (MMDevice mmdevice in mmdeviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                mmdevice.GetPropertyInformation(NAudio.CoreAudioApi.Interfaces.StorageAccessMode.Read);
                Regex rg = new Regex(@"[{\d}]*\.(.*)$");
                PropertyStoreProperty controllerDeviceId = mmdevice.Properties[PropertyKeys.PKEY_Device_ControllerDeviceId];
                Match deviceIdMatch = rg.Match((string)controllerDeviceId.Value);
                string instancePath = deviceIdMatch.Groups[1].Value;

                try
                {
                    if (instancePath.ToLower().Substring(0, AudioDeviceID.Length - 4).Replace("mi_00", "") == AudioDeviceID.ToLower().Substring(0, AudioDeviceID.Length - 4).Replace("mi_03", ""))
                    {
                        device = mmdevice;
                        break;
                    }
                }
                catch
                {
                    continue;
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

        public void setNewPlayback()
        {
            if(wasapiLoopbackCapture != null)
            {
                wasapiLoopbackCapture.Dispose();
            }

            mmdeviceplayback = mmdeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            wasapiLoopbackCapture = new WasapiLoopbackCapture(mmdeviceplayback);
            wasapiLoopbackCapture.StartRecording();
            wasapiLoopbackCapture.DataAvailable += WasapiLoopbackCapture_DataAvailable;
        }

       
        private void WasapiLoopbackCapture_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (SystemAudioPlayback)
            {
                bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
            }
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
            if(hapticStream != null){
                bufferedWaveProvider.ClearBuffer();
                hapticStream.Dispose();
            }

            if(wasapiLoopbackCapture != null){
                wasapiLoopbackCapture.Dispose();
            }
        }
    }
}
