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
        private WasapiOut audioPasstroughStream;
        private WasapiLoopbackCapture wasapiLoopbackCapture = null;
        private MMDeviceEnumerator mmdeviceEnumerator = new MMDeviceEnumerator();
        private Thread AudioPassthroughPlayThread;
        private bool StartNewPlayback = true;
        public BufferedWaveProvider bufferedWaveProvider = new BufferedWaveProvider(WaveFormat.CreateCustomFormat(WaveFormatEncoding.IeeeFloat, 48000, 2, 32, 8, 8));
        public BufferedWaveProvider audioPassthroughBuffer;
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

                for (int i = 0; i < deviceIdMatch.Groups.Count; i++)
                {
                    try
                    {
                        string instancePath = deviceIdMatch.Groups[i].Value;
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

            }

            if (device == null) // If the audio device couldn't be found with Device ID, use device name instead. This won't work with multiple controllers but that's better than nothing.
            {
                foreach (MMDevice mmdevice in mmdeviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    if (mmdevice.FriendlyName.Contains("Wireless Controller"))
                    {
                        device = mmdevice;
                        break;
                    }
                }
            }

            if (device == null || device.State == DeviceState.NotPresent || device.State == DeviceState.Unplugged || device.State == DeviceState.Disabled) // Return if both failed
            {
                return;
            }

            bufferedWaveProvider.BufferLength = 5000000; // 5MB buffer
            bufferedWaveProvider.ReadFully = true;
            bufferedWaveProvider.DiscardOnBufferOverflow = true;
            hapticStream = new WasapiOut(device, AudioClientShareMode.Shared, true, 10);

            setNewPlayback();

            MultiplexingWaveProvider multiplexingWaveProvider = new MultiplexingWaveProvider(new BufferedWaveProvider[] {
                bufferedWaveProvider,
            }, 4);

            multiplexingWaveProvider.ConnectInputToOutput(0, 0);
            multiplexingWaveProvider.ConnectInputToOutput(0, 1);
            multiplexingWaveProvider.ConnectInputToOutput(0, 2);
            multiplexingWaveProvider.ConnectInputToOutput(1, 3);

            hapticStream.Init(multiplexingWaveProvider);
            Thread t = new Thread(new ThreadStart(Play));
            t.IsBackground = true;
            t.Start();
        }

        public void setNewPlayback()
        {
            if (wasapiLoopbackCapture != null)
            {
                StartNewPlayback = false;
                wasapiLoopbackCapture.StopRecording();
                wasapiLoopbackCapture.Dispose();
            }

            if (audioPassthroughBuffer != null)
            {
                audioPassthroughBuffer.ClearBuffer();               
            }

            if(audioPasstroughStream != null)
            {
                audioPasstroughStream.Stop();
            }
            
            if(mmdeviceplayback != null)
            {
                mmdeviceplayback.Dispose();
            }

            mmdeviceplayback = mmdeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            wasapiLoopbackCapture = new WasapiLoopbackCapture(mmdeviceplayback);
            wasapiLoopbackCapture.RecordingStopped += WasapiLoopbackCapture_RecordingStopped;
            wasapiLoopbackCapture.DataAvailable += WasapiLoopbackCapture_DataAvailable;
            wasapiLoopbackCapture.StartRecording();

            audioPasstroughStream = new WasapiOut(device, AudioClientShareMode.Shared, true, 10);
            audioPassthroughBuffer = new BufferedWaveProvider(WaveFormat.CreateCustomFormat(WaveFormatEncoding.IeeeFloat, 48000, wasapiLoopbackCapture.WaveFormat.Channels, 32, 8, 8));
            audioPassthroughBuffer.BufferLength = 5000000; // 5MB buffer
            audioPassthroughBuffer.ReadFully = true;
            audioPassthroughBuffer.DiscardOnBufferOverflow = true;

            MultiplexingWaveProvider multiplexingWaveProviderAP = new MultiplexingWaveProvider(new BufferedWaveProvider[] {
                audioPassthroughBuffer,
            }, 4);

            multiplexingWaveProviderAP.ConnectInputToOutput(0, 0);
            multiplexingWaveProviderAP.ConnectInputToOutput(0, 1);
            multiplexingWaveProviderAP.ConnectInputToOutput(0, 2);
            multiplexingWaveProviderAP.ConnectInputToOutput(1, 3);

            audioPasstroughStream.Init(multiplexingWaveProviderAP);
            AudioPassthroughPlayThread = new Thread(() => PlayAudioPassthrough());
            AudioPassthroughPlayThread.IsBackground = true;
            AudioPassthroughPlayThread.Start();
            StartNewPlayback = true;
        }

        private void WasapiLoopbackCapture_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (StartNewPlayback)
            {
                setNewPlayback();
            }
        }

        private void WasapiLoopbackCapture_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (SystemAudioPlayback)
            {
                audioPassthroughBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
            }
        }

        private void Play()
        {
            hapticStream.Play();
        }

        private void PlayAudioPassthrough()
        {
            audioPasstroughStream.Play();
        }

        public void setVolume(float speaker, float left, float right)
        {
            if (hapticStream != null)
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
        }

        public void Dispose()
        {
            if (hapticStream != null)
            {
                bufferedWaveProvider.ClearBuffer();
                hapticStream.Dispose();
            }

            if (audioPasstroughStream != null)
            {
                audioPassthroughBuffer.ClearBuffer();
                audioPasstroughStream.Dispose();
            }

            if (wasapiLoopbackCapture != null)
            {
                StartNewPlayback = false;
                wasapiLoopbackCapture.Dispose();
            }
        }
    }
}
