﻿using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace Wujek_Dualsense_API
{
    public class HapticFeedback : IDisposable
    {
        private MMDevice device;
        private MMDevice mmdeviceplayback;
        private WasapiOut hapticStream;
        private WasapiOut audioPassthroughStream;
        private WasapiOut hapticsToSpeakerStream;
        private WasapiLoopbackCapture wasapiLoopbackCapture = null;
        private WasapiLoopbackCapture dualsenseLoopback = null;
        private MMDeviceEnumerator mmdeviceEnumerator = new MMDeviceEnumerator();
        private bool StartNewPlayback = true;
        public BufferedWaveProvider bufferedWaveProvider = new BufferedWaveProvider(WaveFormat.CreateCustomFormat(WaveFormatEncoding.IeeeFloat, 48000, 2, 32, 8, 8));
        public BufferedWaveProvider hapticsToSpeakerBuffer;
        public BufferedWaveProvider audioPassthroughBuffer;
        public float speakerPlaybackVolume = 1;
        public float leftActuatorVolume = 1;
        public float rightActuatorVolume = 1;
        public bool SystemAudioPlayback = false;
        public bool HapticPassthrough = false;
        private string audioID = string.Empty;


        public HapticFeedback(string AudioDeviceID, float speaker, float leftactuator, float rightactuator)
        {
            Initalize(AudioDeviceID, speaker, leftactuator, rightactuator);
        }
        
        private void Initalize(string AudioDeviceID, float speaker, float leftactuator, float rightactuator)
        {
            speakerPlaybackVolume = speaker;
            leftActuatorVolume = leftactuator;
            rightActuatorVolume = rightactuator;

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
                            audioID = AudioDeviceID;
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
                        audioID = AudioDeviceID;
                        break;
                    }
                }
            }

            if (device == null || device.State == DeviceState.NotPresent || device.State == DeviceState.Unplugged || device.State == DeviceState.Disabled) // Return if both failed
            {
                return;
            }

            // HAPTIC/SPEAKER STREAM (dualsense.PlaySpeaker())
            bufferedWaveProvider.BufferLength = 5000000; // 5MB buffer
            bufferedWaveProvider.ReadFully = true;
            bufferedWaveProvider.DiscardOnBufferOverflow = true;
            hapticStream = new WasapiOut(device, AudioClientShareMode.Shared, true, 10);

            MultiplexingWaveProvider multiplexingWaveProvider = new MultiplexingWaveProvider(new BufferedWaveProvider[] {
                bufferedWaveProvider,
            }, 4);

            multiplexingWaveProvider.ConnectInputToOutput(0, 0);
            multiplexingWaveProvider.ConnectInputToOutput(0, 1);
            multiplexingWaveProvider.ConnectInputToOutput(0, 2);
            multiplexingWaveProvider.ConnectInputToOutput(1, 3);

            // AUDIO PASSTHROUGH STREAM
            setNewPlayback();
            audioPassthroughStream = new WasapiOut(device, AudioClientShareMode.Shared, true, 10);
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


            // HAPTIC PASSTHROUGH
            dualsenseLoopback = new WasapiLoopbackCapture(device);
            dualsenseLoopback.DataAvailable += DualsenseLoopback_DataAvailable;
            hapticsToSpeakerBuffer = new BufferedWaveProvider(WaveFormat.CreateCustomFormat(dualsenseLoopback.WaveFormat.Encoding, dualsenseLoopback.WaveFormat.SampleRate, 4, dualsenseLoopback.WaveFormat.AverageBytesPerSecond, dualsenseLoopback.WaveFormat.BlockAlign, dualsenseLoopback.WaveFormat.BitsPerSample));
            dualsenseLoopback.StartRecording();
            hapticsToSpeakerStream = new WasapiOut(device, AudioClientShareMode.Shared, true, 10);
            hapticsToSpeakerBuffer.BufferLength = 5000000; // 5MB buffer
            hapticsToSpeakerBuffer.ReadFully = true;
            hapticsToSpeakerBuffer.DiscardOnBufferOverflow = true;

            MultiplexingWaveProvider multiplexingWaveProviderHS = new MultiplexingWaveProvider(new BufferedWaveProvider[] {
                hapticsToSpeakerBuffer,
            }, 4);

            multiplexingWaveProviderHS.ConnectInputToOutput(0, 0);
            multiplexingWaveProviderHS.ConnectInputToOutput(2, 1);
            multiplexingWaveProviderHS.ConnectInputToOutput(2, 2);
            multiplexingWaveProviderHS.ConnectInputToOutput(3, 3);

            // Start the streams
            hapticStream.Init(multiplexingWaveProvider);
            Thread t = new Thread(new ThreadStart(Play));
            t.IsBackground = true;
            t.Start();
            
            audioPassthroughStream.Init(multiplexingWaveProviderAP);
            Thread AudioPassthroughPlayThread = new Thread(() => PlayAudioPassthrough());
            AudioPassthroughPlayThread.IsBackground = true;
            AudioPassthroughPlayThread.Start();

            hapticsToSpeakerStream.Init(multiplexingWaveProviderHS);
            Thread HapticPassthroughPlayThread = new Thread(() => PlayHapticPassthrough());
            HapticPassthroughPlayThread.IsBackground = true;
            HapticPassthroughPlayThread.Start();
            hapticsToSpeakerStream.AudioStreamVolume.SetChannelVolume(0, 0);
            hapticsToSpeakerStream.AudioStreamVolume.SetChannelVolume(1, 1);
            hapticsToSpeakerStream.AudioStreamVolume.SetChannelVolume(2, 0);
            hapticsToSpeakerStream.AudioStreamVolume.SetChannelVolume(3, 0);

            setVolume(speakerPlaybackVolume, leftActuatorVolume, rightActuatorVolume);
        }

        public void setNewPlayback()
        {
            if (wasapiLoopbackCapture != null)
            {
                wasapiLoopbackCapture.StopRecording();
                wasapiLoopbackCapture.Dispose();
            }

            mmdeviceplayback = mmdeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            wasapiLoopbackCapture = new WasapiLoopbackCapture(mmdeviceplayback);
            wasapiLoopbackCapture.RecordingStopped += WasapiLoopbackCapture_RecordingStopped;
            wasapiLoopbackCapture.DataAvailable += WasapiLoopbackCapture_DataAvailable;
            wasapiLoopbackCapture.StartRecording();
        }

        private void DualsenseLoopback_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (HapticPassthrough)
            {
                hapticsToSpeakerBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
            }
        }

        public void ReinitializeHapticFeedback()
        {
            Dispose();
            Initalize(audioID, speakerPlaybackVolume, leftActuatorVolume, rightActuatorVolume);
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

        private void PlayHapticPassthrough()
        {
            hapticsToSpeakerStream.Play();
        }

        private void PlayAudioPassthrough()
        {
            audioPassthroughStream.Play();
        }

        public void setHapticPassthroughVolume()
        {

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
                    if (hapticStream != null)
                    {
                        hapticStream.AudioStreamVolume.SetChannelVolume(0, speakerPlaybackVolume);
                        hapticStream.AudioStreamVolume.SetChannelVolume(1, speakerPlaybackVolume);
                        hapticStream.AudioStreamVolume.SetChannelVolume(2, leftActuatorVolume);
                        hapticStream.AudioStreamVolume.SetChannelVolume(3, rightActuatorVolume);
                    }

                    if (audioPassthroughStream != null)
                    {
                        audioPassthroughStream.AudioStreamVolume.SetChannelVolume(0, speakerPlaybackVolume);
                        audioPassthroughStream.AudioStreamVolume.SetChannelVolume(1, speakerPlaybackVolume);
                        audioPassthroughStream.AudioStreamVolume.SetChannelVolume(2, leftActuatorVolume);
                        audioPassthroughStream.AudioStreamVolume.SetChannelVolume(3, rightActuatorVolume);
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    throw new ArgumentOutOfRangeException("Volume must be between 0.0 and 1.0.");
                }
            }
        }

        public void Dispose()
        {
            StartNewPlayback = false;

            if (hapticStream != null)
            {
                hapticStream.Dispose();
                bufferedWaveProvider.ClearBuffer();
            }

            if(audioPassthroughStream != null)
            {
                audioPassthroughStream.Dispose();
                audioPassthroughBuffer.ClearBuffer();
            }

            if (wasapiLoopbackCapture != null)
            {
                wasapiLoopbackCapture.Dispose();
            }
        }
    }
}