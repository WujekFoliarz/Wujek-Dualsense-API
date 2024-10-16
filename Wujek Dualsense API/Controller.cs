﻿using HidSharp;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Wujek_Dualsense_API.LED;
using static Wujek_Dualsense_API.Motion;
using static Wujek_Dualsense_API.BatteryState;
using Windows.Devices.Power;
using Windows.System.Power;
using NAudio.Utils;
using Windows.Media.Playback;
using HidSharp.Utility;

namespace Wujek_Dualsense_API
{
    public class Dualsense : IDisposable
    {
        private DeviceStream DSDevice;
        public DeviceType DeviceType;
        public ConnectionStatus Connection { get; set; }
        public ConnectionType ConnectionType;
        private int reportLength = 0;
        private int offset = 0;
        public bool Working { get; private set; } = false;
        public string AudioDeviceID { get; private set; } = string.Empty;
        public string DeviceID { get; private set; } = string.Empty;

        private Task transitionTask;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private byte SpeakerVolume;
        private byte MicrophoneVolume;
        private MicrophoneLED micLed = MicrophoneLED.OFF;
        private Microphone.MicrophoneStatus microphoneStatus = Microphone.MicrophoneStatus.ON;
        private Lightbar lightbar = new Lightbar();
        private PlayerLED _playerLED = PlayerLED.OFF;
        private TriggerType.TriggerModes RightTriggerMode = TriggerType.TriggerModes.Off;
        private TriggerType.TriggerModes LeftTriggerMode = TriggerType.TriggerModes.Off;
        private int[] RightTriggerForces = new int[7];
        private int[] LeftTriggerForces = new int[7];
        private PulseOptions micLedPulse = PulseOptions.Off;
        private Vibrations.VibrationType rumbleMode = Vibrations.VibrationType.Haptic_Feedback;
        private AudioOutput _audioOutput = AudioOutput.SPEAKER;
        private HapticFeedback hapticFeedback;
        private Dictionary<string, byte[]> WAV_CACHE = new Dictionary<string, byte[]>();
        private bool bt_initialized = false;
        private FeatureType featureType = FeatureType.FULL;
        public BatteryState.Battery Battery = new BatteryState.Battery();

        public bool ReadOnly { get; set; } = false;
        public State ButtonState = new State();
        public byte LeftRotor = 0;
        public byte RightRotor = 0;
        public int ControllerNumber = 0;

        public Dualsense(string DevicePath)
        {
            Connection = new ConnectionStatus();
            DeviceList list = DeviceList.Local;
            List<Device> devices = new List<Device>();

            foreach (var deviceInfo in list.GetHidDevices())
            {
                if (deviceInfo.VendorID == 1356 && deviceInfo.ProductID == 3302 && deviceInfo.DevicePath == DevicePath) // DualSense
                {
                    reportLength = deviceInfo.GetMaxOutputReportLength();
                    DeviceType = DeviceType.DualSense;
                    devices.Add(deviceInfo);
                }
                else if (deviceInfo.VendorID == 1356 && deviceInfo.ProductID == 3570 && deviceInfo.DevicePath == DevicePath) // DualSense Edge
                {
                    reportLength = deviceInfo.GetMaxOutputReportLength();
                    DeviceType = DeviceType.DualSense_Edge;
                    devices.Add(deviceInfo);
                }
                else if (deviceInfo.VendorID == 1356 && deviceInfo.ProductID == 1476 && deviceInfo.DevicePath == DevicePath) // DualShock 4
                {
                    reportLength = deviceInfo.GetMaxOutputReportLength();
                    DeviceType = DeviceType.DualShock4;
                    devices.Add(deviceInfo);
                }
                else if (deviceInfo.VendorID == 1356 && deviceInfo.ProductID == 2508 && deviceInfo.DevicePath == DevicePath) // DualShock 4 V2
                {
                    reportLength = deviceInfo.GetMaxOutputReportLength();
                    DeviceType = DeviceType.DualShock4;
                    devices.Add(deviceInfo);
                }
            }

            try
            {
                DSDevice = devices[ControllerNumber].Open();
                DeviceID = devices[ControllerNumber].DevicePath;

                this.ControllerNumber = ControllerNumber;
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new Exception("Couldn't find Dualsense device number: " + ControllerNumber);
            }

            this.ConnectionType = getConnectionType();

            if (this.ConnectionType == ConnectionType.USB && this.DeviceType == DeviceType.DualSense || this.DeviceType == DeviceType.DualSense_Edge)
            {
                AudioDeviceID = PnPDevice.GetDeviceByInterfaceId(devices[ControllerNumber].DevicePath).Parent.DeviceId.ToString();
                SetSpeakerVolume(100);
                SetMicrophoneVolume(35);
                hapticFeedback = new HapticFeedback(AudioDeviceID, 1, 1, 1, this.DeviceType);
            }
        }

        public void ReinitializeHapticFeedback()
        {
            if (this.ConnectionType == ConnectionType.USB && hapticFeedback != null)
            {
                hapticFeedback.ReinitializeHapticFeedback();
            }
        }

        public void Start()
        {
            Working = true;

            new Thread(() =>
            {
                while (Working)
                {
                    Write();
                    Thread.Sleep(25);
                }
            }).Start();

            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                Thread.CurrentThread.Priority = ThreadPriority.Highest;
                while (Working)
                {
                    Read();
                }
            }).Start();
        }

        public void ResetSettings()
        {
            SetVibrationType(Vibrations.VibrationType.Haptic_Feedback); // Haptic feedback is native
            ReleaseLED();
            SetLeftTrigger(TriggerType.TriggerModes.Rigid_B, 0, 0, 0, 0, 0, 0, 0);
            SetRightTrigger(TriggerType.TriggerModes.Rigid_B, 0, 0, 0, 0, 0, 0, 0);
            SetStandardRumble(0, 0);
            if (this.ConnectionType == ConnectionType.USB)
            {
                TurnMicrophoneOn();
                SetAudioOutput(AudioOutput.SPEAKER);
                SetMicrophoneVolume(35);
            }
        }

        /// <summary>
        /// Sets audio output on your controller
        /// </summary>
        /// <returns></returns>
        public void SetAudioOutput(AudioOutput audioOutput)
        {
            _audioOutput = audioOutput;
        }

        /// <summary>
        /// Starts capturing system audio and playing it through your controller
        /// </summary>
        /// <returns></returns>
        public void StartSystemAudioToHaptics()
        {
            if (this.ConnectionType == ConnectionType.USB && hapticFeedback != null)
            {
                hapticFeedback.SystemAudioPlayback = true;
            }
        }

        /// <summary>
        /// Stops capturing system audio
        /// </summary>
        /// <returns></returns>
        public void StopSystemAudioToHaptics()
        {
            if (this.ConnectionType == ConnectionType.USB && hapticFeedback != null)
            {
                hapticFeedback.SystemAudioPlayback = false;
            }
        }

        /// <summary>
        /// Plays local WAV file on your controller
        /// </summary>
        /// <param name="PathToWAV">Path to WAV file located on user's computer</param>
        /// <param name="fileVolumeLeftActuator">Sets vibration force of the left side</param>
        /// <param name="fileVolumeRightActuator">Sets vibration force of the right side</param>
        /// <param name="ClearBuffer">When set to true, all previous sounds are cancelled in favour of the current one</param>
        /// <returns></returns>
        public void PlayHaptics(string PathToWAV, float fileVolumeLeftActuator, float fileVolumeRightActuator, bool ClearBuffer, int Channel)
        {
            if(Channel > 10 || Channel < 0)
            {
                throw new Exception("Channel can't be set to less than 0 or more than 10");
            }
            else if(Channel != 0)
            {
                Channel = Channel-1;
            }

            if (this.ConnectionType == ConnectionType.USB && rumbleMode == Vibrations.VibrationType.Haptic_Feedback && hapticFeedback != null)
            {
                hapticFeedback.setVolume(fileVolumeLeftActuator, fileVolumeRightActuator);

                if (!WAV_CACHE.ContainsKey(PathToWAV))
                {
                    byte[] file = File.ReadAllBytes(PathToWAV);
                    try
                    {
                        WAV_CACHE.Add(PathToWAV, file);
                    }
                    catch (ArgumentException)
                    {
                        foreach (KeyValuePair<string, byte[]> pair in WAV_CACHE)
                        {
                            if (pair.Key == PathToWAV)
                            {
                                try
                                {
                                    if (ClearBuffer)
                                        hapticFeedback.bufferedWaveProvider[Channel].ClearBuffer();

                                    hapticFeedback.bufferedWaveProvider[Channel].AddSamples(pair.Value, 0, pair.Value.Length);
                                }
                                catch (Exception)
                                {
                                    hapticFeedback.bufferedWaveProvider[Channel].ClearBuffer();
                                    hapticFeedback.bufferedWaveProvider[Channel].AddSamples(pair.Value, 0, pair.Value.Length);
                                }
                            }
                        }
                    }

                    try
                    {
                        if (ClearBuffer)
                            hapticFeedback.bufferedWaveProvider[Channel].ClearBuffer();

                        hapticFeedback.bufferedWaveProvider[Channel].AddSamples(file, 0, file.Length);
                    }
                    catch (Exception)
                    {
                        hapticFeedback.bufferedWaveProvider[Channel].ClearBuffer();
                        hapticFeedback.bufferedWaveProvider[Channel].AddSamples(file, 0, file.Length);
                    }

                    file = null;
                }
                else
                {
                    foreach (KeyValuePair<string, byte[]> pair in WAV_CACHE)
                    {
                        if (pair.Key == PathToWAV)
                        {
                            try
                            {
                                if (ClearBuffer)
                                    hapticFeedback.bufferedWaveProvider[Channel].ClearBuffer();

                                hapticFeedback.bufferedWaveProvider[Channel].AddSamples(pair.Value, 0, pair.Value.Length);
                            }
                            catch (Exception)
                            {
                                hapticFeedback.bufferedWaveProvider[Channel].ClearBuffer();
                                hapticFeedback.bufferedWaveProvider[Channel].AddSamples(pair.Value, 0, pair.Value.Length);
                            }
                        }
                    }
                }
            }
        }

        public byte[] ChangeWaveVolume(byte[] wavFileBytes, float volumeScale)
        {
            // Ensure the volumeScale is between 0 and 1 for reducing volume, and above 1 for amplifying.
            if (volumeScale < 0)
                throw new ArgumentException("Volume scale cannot be negative.");

            // WAV file headers are 44 bytes long.
            const int wavHeaderSize = 44;

            // Check if the input is at least large enough to be a valid WAV file.
            if (wavFileBytes.Length < wavHeaderSize)
                throw new ArgumentException("Invalid WAV file.");

            // Copy the header without modifying it.
            byte[] newWavFileBytes = new byte[wavFileBytes.Length];
            Array.Copy(wavFileBytes, newWavFileBytes, wavHeaderSize);

            // WAV data typically starts at byte 44 (header size).
            for (int i = wavHeaderSize; i < wavFileBytes.Length; i += 2)
            {
                // WAV audio data is usually stored in 16-bit samples (2 bytes per sample).
                // Read the sample (little-endian).
                short sample = BitConverter.ToInt16(wavFileBytes, i);

                // Scale the sample by the volumeScale.
                int newSample = (int)(sample * volumeScale);

                // Clamp the value to prevent overflow.
                if (newSample > short.MaxValue) newSample = short.MaxValue;
                if (newSample < short.MinValue) newSample = short.MinValue;

                // Write the new sample back into the byte array (little-endian).
                byte[] newSampleBytes = BitConverter.GetBytes((short)newSample);
                newWavFileBytes[i] = newSampleBytes[0];
                newWavFileBytes[i + 1] = newSampleBytes[1];
            }

            return newWavFileBytes;
        }

        /// <summary>
        /// Plays local WAV file on your controller
        /// </summary>
        /// <param name="PathToWAV">Path to WAV file located on user's computer</param>
        /// <param name="FileVolumeSpeaker">Sets volume of the speaker</param>
        /// <param name="ClearBuffer">When set to true, all previous sounds are cancelled in favour of the current one</param>
        /// <returns></returns>
        public void PlaySpeaker(string PathToWAV, float FileVolumeSpeaker, bool ClearBuffer)
        {
            if (this.ConnectionType == ConnectionType.USB && rumbleMode == Vibrations.VibrationType.Haptic_Feedback && hapticFeedback != null)
            {
                hapticFeedback.setSpeakerVolume(FileVolumeSpeaker);

                if (!WAV_CACHE.ContainsKey(PathToWAV))
                {
                    byte[] file = ChangeWaveVolume(File.ReadAllBytes(PathToWAV), 1f);

                    try
                    {
                        WAV_CACHE.Add(PathToWAV, file);
                    }
                    catch (ArgumentException)
                    {
                        foreach (KeyValuePair<string, byte[]> pair in WAV_CACHE)
                        {
                            if (pair.Key == PathToWAV)
                            {
                                try
                                {
                                    if (ClearBuffer)
                                        hapticFeedback.BufferSpeaker.ClearBuffer();

                                    hapticFeedback.BufferSpeaker.AddSamples(pair.Value, 0, pair.Value.Length);
                                }
                                catch (Exception)
                                {
                                    hapticFeedback.BufferSpeaker.ClearBuffer();
                                    hapticFeedback.BufferSpeaker.AddSamples(pair.Value, 0, pair.Value.Length);
                                }
                            }
                        }
                    }

                    try
                    {
                        if (ClearBuffer)
                            hapticFeedback.BufferSpeaker.ClearBuffer();

                        hapticFeedback.BufferSpeaker.AddSamples(file, 0, file.Length);
                    }
                    catch (Exception)
                    {
                        hapticFeedback.BufferSpeaker.ClearBuffer();
                        hapticFeedback.BufferSpeaker.AddSamples(file, 0, file.Length);
                    }

                    file = null;
                }
                else
                {
                    foreach (KeyValuePair<string, byte[]> pair in WAV_CACHE)
                    {
                        if (pair.Key == PathToWAV)
                        {
                            try
                            {
                                if (ClearBuffer)
                                    hapticFeedback.BufferSpeaker.ClearBuffer();

                                hapticFeedback.BufferSpeaker.AddSamples(pair.Value, 0, pair.Value.Length);
                            }
                            catch (Exception)
                            {
                                hapticFeedback.BufferSpeaker.ClearBuffer();
                                hapticFeedback.BufferSpeaker.AddSamples(pair.Value, 0, pair.Value.Length);
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Removes WAV files from memory.
        /// </summary>
        /// <returns></returns>
        public void ClearHapticFeedbackCache()
        {
            WAV_CACHE.Clear();
        }

        /// <summary>
        /// Sets the type of vibration your controller will use.
        /// </summary>
        /// <returns></returns>
        public void SetVibrationType(Vibrations.VibrationType vibrationType)
        {
            rumbleMode = vibrationType;
        }

        /// <summary>
        /// Sets standard rumble vibrations.
        /// </summary>
        /// <param name="LeftRumble">Left rumble strength 0-255</param>
        /// <param name="RightRumble">Right rumble strength 0-255</param>
        /// <returns></returns>
        public void SetStandardRumble(byte LeftRumble, byte RightRumble)
        {
            if (LeftRumble >= 0 && RightRumble >= 0 &&
                LeftRumble <= 255 && RightRumble <= 255)
            {
                LeftRotor = LeftRumble;
                RightRotor = RightRumble;
            }
            else
            {
                throw new ArgumentException("Rumble motors cannot be set lower than 0 or higher than 255.");
            }
        }

        /// <summary>
        /// Sets adaptive trigger effect for left trigger
        /// </summary>
        /// <returns></returns>
        public void SetLeftTrigger(TriggerType.TriggerModes triggerMode, int forceOne, int forceTwo, int forceThree, int forceFour, int forceFive, int forceSix, int forceSeven)
        {
            LeftTriggerMode = triggerMode;
            LeftTriggerForces[0] = forceOne;
            LeftTriggerForces[1] = forceTwo;
            LeftTriggerForces[2] = forceThree;
            LeftTriggerForces[3] = forceFour;
            LeftTriggerForces[4] = forceFive;
            LeftTriggerForces[5] = forceSix;
            LeftTriggerForces[6] = forceSeven;
        }

        /// <summary>
        /// Sets adaptive trigger effect for right trigger
        /// </summary>
        /// <returns></returns>
        public void SetRightTrigger(TriggerType.TriggerModes triggerMode, int forceOne, int forceTwo, int forceThree, int forceFour, int forceFive, int forceSix, int forceSeven)
        {
            RightTriggerMode = triggerMode;
            RightTriggerForces[0] = forceOne;
            RightTriggerForces[1] = forceTwo;
            RightTriggerForces[2] = forceThree;
            RightTriggerForces[3] = forceFour;
            RightTriggerForces[4] = forceFive;
            RightTriggerForces[5] = forceSix;
            RightTriggerForces[6] = forceSeven;
        }

        /// <summary>
        /// Unmutes the microphone
        /// </summary>
        /// <returns></returns>
        public void TurnMicrophoneOn()
        {
            microphoneStatus = Microphone.MicrophoneStatus.ON;
        }

        /// <summary>
        /// Mutes the microphone
        /// </summary>
        /// <returns></returns>
        public void TurnMicrophoneOff()
        {
            microphoneStatus = Microphone.MicrophoneStatus.OFF;
        }

        /// <summary>
        /// Sets the controller's microphone volume on hardware level, 0-200
        /// </summary>
        /// <returns></returns>
        public void SetMicrophoneVolume(int Volume)
        {
            if (Volume >= 0 && Volume <= 200)
                MicrophoneVolume = Convert.ToByte(int.Parse(Convert.ToString(Volume)).ToString("X"), 16);
            else
                throw new ArgumentException("The microphone volume cannot be lower than 0 or higher than 200.");
        }

        /// <summary>
        /// Sets the controller's speaker volume on hardware level, 0-200
        /// </summary>
        /// <returns></returns>
        public void SetSpeakerVolume(int Volume)
        {
            if (Volume != 0)
                Volume = Volume + 40;

            if (Volume >= 0 && Volume <= 240)
                SpeakerVolume = Convert.ToByte(int.Parse(Convert.ToString(Volume)).ToString("X"), 16);
            else
                throw new ArgumentException("The speaker volume cannot be lower than 0 or higher than 200.");
        }

        /// <summary>
        /// Sets the controller's speaker and actuator volumes on software level, 0f-1f
        /// </summary>
        /// <returns></returns>
        public void SetSpeakerVolumeInSoftware(float SpeakerVolume, float LeftActuatorVolume, float RightActuatorVolume)
        {
            if (hapticFeedback != null && this.ConnectionType == ConnectionType.USB && rumbleMode == Vibrations.VibrationType.Haptic_Feedback)
            {
                hapticFeedback.setVolume(LeftActuatorVolume, RightActuatorVolume);
                hapticFeedback.setSpeakerVolume(SpeakerVolume);
            }
        }

        /// <summary>
        /// Sets the controller's speaker and actuator volumes on system audio passthrough stream
        /// </summary>
        /// <returns></returns>
        public void SetSystemAudioToHapticsVolume(float SpeakerVolume, float LeftActuatorVolume, float RightActuatorVolume)
        {
            if (hapticFeedback != null && this.ConnectionType == ConnectionType.USB && rumbleMode == Vibrations.VibrationType.Haptic_Feedback)
            {
                hapticFeedback.setAudioPassthroughVolume(SpeakerVolume, LeftActuatorVolume, RightActuatorVolume);
            }
        }

        /// <summary>
        /// Sets lightbar color
        /// </summary>
        /// <returns></returns>
        public void SetLightbar(int R, int G, int B)
        {
            featureType = FeatureType.FULL;
            if (transitionTask != null && !transitionTask.IsCompleted) // Cancel any transitions
                cts.Cancel();

            lightbar.R = R;
            lightbar.G = G;
            lightbar.B = B;
        }

        /// <summary>
        /// Sets lightbar color with transition animation
        /// </summary>
        /// <param name="transitionSteps">Defines the number of incremental steps the transition will take from the current color to the target color.</param>
        /// <param name="transitionDelay">Defines the time (in milliseconds) the program waits between each step of the transition.</param>
        /// <returns></returns>
        public void SetLightbarTransition(int R, int G, int B, int transitionSteps, int transitionDelay)
        {
            if (transitionTask != null && !transitionTask.IsCompleted)
                cts.Cancel();

            if (transitionTask != null && transitionTask.IsCompleted)
            {
                cts.Dispose();
                cts = new CancellationTokenSource();
            }

            transitionTask = new Task(() => transitionLightBar(R, G, B, transitionSteps, transitionDelay, cts.Token));
            transitionTask.Start();
        }

        private void transitionLightBar(int R, int G, int B, int transitionSteps, int transitionDelay, CancellationToken token)
        {
            featureType = FeatureType.FULL;
            int startR = lightbar.R;
            int startG = lightbar.G;
            int startB = lightbar.B;

            for (int step = 0; step <= transitionSteps; step++)
            {
                token.ThrowIfCancellationRequested();
                double t = (double)step / transitionSteps;

                lightbar.R = (int)(startR + t * (R - startR));
                lightbar.G = (int)(startG + t * (G - startG));
                lightbar.B = (int)(startB + t * (B - startB));
                Thread.Sleep(transitionDelay);
            }
        }

        /// <summary>
        /// Sets player LEDs
        /// </summary>
        /// <returns></returns>
        public void SetPlayerLED(PlayerLED PlayerLED)
        {
            featureType = FeatureType.FULL;
            _playerLED = PlayerLED;
        }

        /// <summary>
        /// Sets microphone LED
        /// </summary>
        /// <returns></returns>
        public void SetMicrophoneLED(MicrophoneLED MicLED)
        {
            featureType = FeatureType.FULL;
            micLed = MicLED;
        }

        /// <summary>
        /// Release the LEDs from API
        /// </summary>
        /// <returns></returns>
        public void ReleaseLED()
        {
            SetPlayerLED(PlayerLED.OFF);
            SetLightbar(0, 0, 0);
            SetMicrophoneLED(MicrophoneLED.OFF);
            featureType = FeatureType.LETGO;
        }

        private void Read()
        {
            try
            {
                if(this.DeviceType == DeviceType.DualSense || this.DeviceType == DeviceType.DualSense_Edge)
                {
                    byte[] ButtonStates = new byte[reportLength];
                    DSDevice.Read(ButtonStates);
                    if (this.ConnectionType == ConnectionType.BT) { offset = 1; }

                    // ButtonButtonStates 0 is always 1
                    ButtonState.LX = ButtonStates[1 + offset];
                    ButtonState.LY = ButtonStates[2 + offset];
                    ButtonState.RX = ButtonStates[3 + offset];
                    ButtonState.RY = ButtonStates[4 + offset];
                    ButtonState.L2 = ButtonStates[5 + offset];
                    ButtonState.R2 = ButtonStates[6 + offset];

                    // ButtonState 7 always increments -> not used anywhere

                    byte buttonButtonState = ButtonStates[8 + offset];
                    ButtonState.triangle = (buttonButtonState & (1 << 7)) != 0;
                    ButtonState.circle = (buttonButtonState & (1 << 6)) != 0;
                    ButtonState.cross = (buttonButtonState & (1 << 5)) != 0;
                    ButtonState.square = (buttonButtonState & (1 << 4)) != 0;

                    // dpad
                    byte dpad_ButtonState = (byte)(buttonButtonState & 0x0F);
                    ButtonState.SetDPadState(dpad_ButtonState);

                    byte misc = ButtonStates[9 + offset];
                    ButtonState.R3 = (misc & (1 << 7)) != 0;
                    ButtonState.L3 = (misc & (1 << 6)) != 0;
                    ButtonState.options = (misc & (1 << 5)) != 0;
                    ButtonState.share = (misc & (1 << 4)) != 0;
                    ButtonState.R2Btn = (misc & (1 << 3)) != 0;
                    ButtonState.L2Btn = (misc & (1 << 2)) != 0;
                    ButtonState.R1 = (misc & (1 << 1)) != 0;
                    ButtonState.L1 = (misc & (1 << 0)) != 0;

                    byte misc2 = ButtonStates[10 + offset];
                    ButtonState.ps = (misc2 & (1 << 0)) != 0;
                    ButtonState.touchBtn = (misc2 & 0x02) != 0;
                    ButtonState.micBtn = (misc2 & 0x04) != 0;

                    // trackpad touch
                    ButtonState.trackPadTouch0.RawTrackingNum = (byte)(ButtonStates[33 + offset]);
                    ButtonState.trackPadTouch0.ID = (byte)(ButtonStates[33 + offset] & 0x7F);
                    ButtonState.trackPadTouch0.IsActive = (ButtonStates[33 + offset] & 0x80) == 0;
                    ButtonState.trackPadTouch0.X = ((ButtonStates[35 + offset] & 0x0F) << 8) | ButtonStates[34 + offset];
                    ButtonState.trackPadTouch0.Y = ((ButtonStates[36 + offset]) << 4) | ((ButtonStates[35 + offset] & 0xF0) >> 4);

                    // trackpad touch
                    ButtonState.trackPadTouch1.RawTrackingNum = (byte)(ButtonStates[37 + offset]);
                    ButtonState.trackPadTouch1.ID = (byte)(ButtonStates[37 + offset] & 0x7F);
                    ButtonState.trackPadTouch1.IsActive = (ButtonStates[37 + offset] & 0x80) == 0;
                    ButtonState.trackPadTouch1.X = ((ButtonStates[39 + offset] & 0x0F) << 8) | ButtonStates[38 + offset];
                    ButtonState.trackPadTouch1.Y = ((ButtonStates[40 + offset]) << 4) | ((ButtonStates[39 + offset] & 0xF0) >> 4);
                    ButtonState.TouchPacketNum = (byte)(ButtonStates[41 + offset]);

                    // gyro
                    ButtonState.gyro.X = BitConverter.ToInt16(new byte[] { ButtonStates[16 + offset], ButtonStates[17 + offset] }, 0);
                    ButtonState.gyro.Y = BitConverter.ToInt16(new byte[] { ButtonStates[18 + offset], ButtonStates[19 + offset] }, 0);
                    ButtonState.gyro.Z = BitConverter.ToInt16(new byte[] { ButtonStates[20 + offset], ButtonStates[21 + offset] }, 0);

                    // accel
                    ButtonState.accelerometer.X = BitConverter.ToInt16(new byte[] { ButtonStates[22 + offset], ButtonStates[23 + offset] }, 0);
                    ButtonState.accelerometer.Y = BitConverter.ToInt16(new byte[] { ButtonStates[24 + offset], ButtonStates[25 + offset] }, 0);
                    ButtonState.accelerometer.Z = BitConverter.ToInt16(new byte[] { ButtonStates[26 + offset], ButtonStates[27 + offset] }, 0);

                    ButtonState.accelerometer.SensorTimestamp = BitConverter.ToInt16(new byte[] { ButtonStates[28 + offset], ButtonStates[29 + offset] }, 0);

                    // battery
                    this.Battery.State = (BatteryState.State)((byte)(ButtonStates[53 + offset] & 0xF0) >> 4);
                    this.Battery.Level = Math.Min((int)((ButtonStates[53 + offset] & 0x0F) * 10 + 5), 100);
                }
                else if (this.DeviceType == DeviceType.DualShock4)
                {
                    byte[] ButtonStates = new byte[reportLength];
                    DSDevice.Read(ButtonStates);
                    if (this.ConnectionType == ConnectionType.BT) { offset = 0; } // ???

                    ButtonState.LX = ButtonStates[1 + offset];
                    ButtonState.LY = ButtonStates[2 + offset];
                    ButtonState.RX = ButtonStates[3 + offset];
                    ButtonState.RY = ButtonStates[4 + offset];

                    byte buttonButtonState = ButtonStates[5 + offset];
                    ButtonState.triangle = (buttonButtonState & (1 << 7)) != 0;
                    ButtonState.circle = (buttonButtonState & (1 << 6)) != 0;
                    ButtonState.cross = (buttonButtonState & (1 << 5)) != 0;
                    ButtonState.square = (buttonButtonState & (1 << 4)) != 0;

                    byte dpad_ButtonState = (byte)(buttonButtonState & 0x0F);
                    ButtonState.SetDPadState(dpad_ButtonState);

                    byte misc = ButtonStates[6 + offset];
                    ButtonState.R3 = (misc & (1 << 7)) != 0;
                    ButtonState.L3 = (misc & (1 << 6)) != 0;
                    ButtonState.options = (misc & (1 << 5)) != 0;
                    ButtonState.share = (misc & (1 << 4)) != 0;
                    ButtonState.R2Btn = (misc & (1 << 3)) != 0;
                    ButtonState.L2Btn = (misc & (1 << 2)) != 0;
                    ButtonState.R1 = (misc & (1 << 1)) != 0;
                    ButtonState.L1 = (misc & (1 << 0)) != 0;

                    byte misc2 = ButtonStates[7 + offset];
                    ButtonState.ps = (misc2 & (1 << 0)) != 0;
                    ButtonState.touchBtn = (misc2 & 0x02) != 0;
                    ButtonState.micBtn = (misc2 & 0x04) != 0;

                    ButtonState.L2 = ButtonStates[8 + offset];
                    ButtonState.R2 = ButtonStates[9 + offset];

                    ButtonState.accelerometer.SensorTimestamp = BitConverter.ToInt16(new byte[] { ButtonStates[10 + offset], ButtonStates[11 + offset] }, 0);

                    this.Battery.State = (BatteryState.State)((byte)(ButtonStates[12 + offset] & 0xF0) >> 4);
                    this.Battery.Level = Math.Min((int)((ButtonStates[12 + offset] & 0x0F) * 10 + 5), 100);

                    ButtonState.gyro.X = BitConverter.ToInt16(new byte[] { ButtonStates[13 + offset], ButtonStates[14 + offset] }, 0);
                    ButtonState.gyro.Y = BitConverter.ToInt16(new byte[] { ButtonStates[15 + offset], ButtonStates[16 + offset] }, 0);
                    ButtonState.gyro.Z = BitConverter.ToInt16(new byte[] { ButtonStates[17 + offset], ButtonStates[18 + offset] }, 0);

                    ButtonState.accelerometer.X = BitConverter.ToInt16(new byte[] { ButtonStates[19 + offset], ButtonStates[20 + offset] }, 0);
                    ButtonState.accelerometer.Y = BitConverter.ToInt16(new byte[] { ButtonStates[21 + offset], ButtonStates[22 + offset] }, 0);
                    ButtonState.accelerometer.Z = BitConverter.ToInt16(new byte[] { ButtonStates[23 + offset], ButtonStates[24 + offset] }, 0);

                    // trackpad touch
                    ButtonState.trackPadTouch0.RawTrackingNum = (byte)(ButtonStates[33 + offset]);
                    ButtonState.trackPadTouch0.ID = (byte)(ButtonStates[34 + offset] & 0x7F);
                    ButtonState.trackPadTouch0.IsActive = (ButtonStates[35 + offset] & 0x80) == 0;
                    ButtonState.trackPadTouch0.X = ((ButtonStates[37 + offset] & 0x0F) << 8) | ButtonStates[36 + offset];
                    ButtonState.trackPadTouch0.Y = ((ButtonStates[38 + offset]) << 4) | ((ButtonStates[39 + offset] & 0xF0) >> 4);

                    // trackpad touch
                    ButtonState.trackPadTouch1.IsActive = (ButtonStates[39 + offset] & 0x80) == 0;
                    ButtonState.trackPadTouch1.X = ((ButtonStates[41 + offset] & 0x0F) << 8) | ButtonStates[40 + offset];
                    ButtonState.trackPadTouch1.Y = ((ButtonStates[42 + offset]) << 4) | ((ButtonStates[43 + offset] & 0xF0) >> 4);
                }
            }
            catch (Exception e)
            {
                Working = false;
                if (e.Message.Contains("Operation failed after some time"))
                {
                    Connection.OnControllerDisconnect(ControllerNumber);
                }
                else if (e.Message.Contains("Closed."))
                {
                    // ignore
                }
                else
                {
                    Console.WriteLine(e.Message + e.Source + e.StackTrace);
                    MessageBox.Show(e.Message + e.Source + e.StackTrace);
                    Connection.OnControllerDisconnect(ControllerNumber);
                }
            }
        }

        private void Connection_ControllerDisconnected(object? sender, ConnectionStatus.Controller e)
        {
            e.ControllerNumber = ControllerNumber;
            Dispose();
        }

        private void Write()
        {
            if (ReadOnly == false)
            {
                byte[] outReport = null;

                if (this.DeviceType == DeviceType.DualSense || this.DeviceType == DeviceType.DualSense_Edge)
                {
                    outReport = new byte[reportLength];

                    if (this.ConnectionType == ConnectionType.USB)
                    {
                        outReport[0] = 2;
                        outReport[1] = (byte)rumbleMode;
                        outReport[2] = (byte)featureType;
                        outReport[3] = (byte)RightRotor; // right low freq motor 0-255
                        outReport[4] = (byte)LeftRotor; // left low freq motor 0-255
                        outReport[5] = 0x7C; // <-- headset volume
                        outReport[6] = (byte)SpeakerVolume; // <-- speaker volume
                        outReport[7] = (byte)MicrophoneVolume; // <-- mic volume
                        outReport[8] = (byte)_audioOutput; // <-- audio output
                        outReport[9] = (byte)micLed; //microphone led
                        outReport[10] = (byte)microphoneStatus;
                        outReport[11] = (byte)RightTriggerMode;
                        outReport[12] = (byte)RightTriggerForces[0];
                        outReport[13] = (byte)RightTriggerForces[1];
                        outReport[14] = (byte)RightTriggerForces[2];
                        outReport[15] = (byte)RightTriggerForces[3];
                        outReport[16] = (byte)RightTriggerForces[4];
                        outReport[17] = (byte)RightTriggerForces[5];
                        outReport[20] = (byte)RightTriggerForces[6];
                        outReport[22] = (byte)LeftTriggerMode;
                        outReport[23] = (byte)LeftTriggerForces[0];
                        outReport[24] = (byte)LeftTriggerForces[1];
                        outReport[25] = (byte)LeftTriggerForces[2];
                        outReport[26] = (byte)LeftTriggerForces[3];
                        outReport[27] = (byte)LeftTriggerForces[4];
                        outReport[28] = (byte)LeftTriggerForces[5];
                        outReport[31] = (byte)LeftTriggerForces[6];
                        outReport[39] = (byte)Brightness.high;
                        outReport[41] = (byte)Brightness.high;
                        outReport[42] = (byte)micLedPulse;
                        outReport[43] = (byte)Brightness.high;
                        outReport[44] = (byte)_playerLED;
                        outReport[45] = (byte)lightbar.R;
                        outReport[46] = (byte)lightbar.G;
                        outReport[47] = (byte)lightbar.B;
                    }
                    else if (this.ConnectionType == ConnectionType.BT)
                    {
                        outReport[0] = 0x31;
                        outReport[1] = 2;
                        outReport[2] = (byte)rumbleMode;
                        if (bt_initialized == false)
                        {
                            outReport[3] = 0x1 | 0x2 | 0x4 | 0x8 | 0x10 | 0x40;
                            bt_initialized = true;
                        }
                        else if (bt_initialized == true)
                        {
                            outReport[3] = (byte)featureType;
                        }
                        outReport[4] = (byte)RightRotor; // right low freq motor 0-255
                        outReport[5] = (byte)LeftRotor; // left low freq motor 0-255
                        outReport[10] = (byte)micLed; //microphone led
                        outReport[11] = (byte)microphoneStatus;
                        outReport[12] = (byte)RightTriggerMode;
                        outReport[13] = (byte)RightTriggerForces[0];
                        outReport[14] = (byte)RightTriggerForces[1];
                        outReport[15] = (byte)RightTriggerForces[2];
                        outReport[16] = (byte)RightTriggerForces[3];
                        outReport[17] = (byte)RightTriggerForces[4];
                        outReport[18] = (byte)RightTriggerForces[5];
                        outReport[21] = (byte)RightTriggerForces[6];
                        outReport[23] = (byte)LeftTriggerMode;
                        outReport[24] = (byte)LeftTriggerForces[0];
                        outReport[25] = (byte)LeftTriggerForces[1];
                        outReport[26] = (byte)LeftTriggerForces[2];
                        outReport[27] = (byte)LeftTriggerForces[3];
                        outReport[28] = (byte)LeftTriggerForces[4];
                        outReport[29] = (byte)LeftTriggerForces[5];
                        outReport[32] = (byte)LeftTriggerForces[6];
                        outReport[40] = (byte)Brightness.high;
                        outReport[43] = (byte)Brightness.high;
                        outReport[44] = (byte)micLedPulse;
                        outReport[45] = (byte)Brightness.high;
                        outReport[45] = (byte)_playerLED;
                        outReport[46] = (byte)lightbar.R;
                        outReport[47] = (byte)lightbar.G;
                        outReport[48] = (byte)lightbar.B;
                        uint crcChecksum = CRC32.ComputeCRC32(outReport, 74);
                        byte[] checksumBytes = BitConverter.GetBytes(crcChecksum);
                        Array.Copy(checksumBytes, 0, outReport, 74, 4);
                    }
                }
                else if(this.DeviceType == DeviceType.DualShock4)
                {
                    if(this.ConnectionType == ConnectionType.USB)
                    {
                        outReport = new byte[9];
                        outReport[0] = 0x05;
                        outReport[1] = 0x07;
                        outReport[4] = (byte)RightRotor;
                        outReport[5] = (byte)LeftRotor;
                        outReport[6] = (byte)lightbar.R;
                        outReport[7] = (byte)lightbar.G;
                        outReport[8] = (byte)lightbar.B;
                    }
                    else if (this.ConnectionType == ConnectionType.BT)
                    {
                        outReport = new byte[78]; // doesnt work
                        outReport[0] = 0xa2;
                        outReport[1] = 0x11;
                        outReport[2] = 0xc0;
                        outReport[3] = 0x20;
                        outReport[4] = 0xf4;
                        outReport[5] = 0x04;
                        outReport[6] = 0x00;

                        outReport[7] = (byte)RightRotor;
                        outReport[8] = (byte)LeftRotor;
                        outReport[9] = (byte)lightbar.R;
                        outReport[10] = (byte)lightbar.G;
                        outReport[11] = (byte)lightbar.B;
                        outReport[22] = 0x43;
                        outReport[23] = 0x43;
                        outReport[22] = 0x00;
                        outReport[23] = 0x4d;
                        outReport[24] = 0x85;

                        uint crc32 = CRC32.Crc32(outReport, 75);

                        for (int i = 0; i < outReport.Length - 4; i++)
                        {
                            outReport[i] = outReport[i + 1];
                        }

                        outReport[74] = (byte)(crc32 & 0xFF);
                        outReport[75] = (byte)(crc32 >> 8 & 0xFF);
                        outReport[76] = (byte)(crc32 >> 16 & 0xFF);
                        outReport[77] = (byte)(crc32 >> 24);
                    }
                }

                try
                {
                    if(outReport != null)
                        DSDevice.WriteAsync(outReport, 0, outReport.Length);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message + e.Source + e.StackTrace);
                    Console.WriteLine(e.Message + e.Source + e.StackTrace);
                    Working = false;
                }
            }
        }

        private ConnectionType getConnectionType()
        {
            if (DeviceType == DeviceType.DualSense)
            {
                if (reportLength >= 78)
                {
                    reportLength = 78;
                    offset = 1;
                    return ConnectionType.BT;
                }
                else
                {
                    reportLength = 64;
                    offset = 0;
                    return ConnectionType.USB;
                }
            }
            else if (DeviceType == DeviceType.DualSense_Edge)
            {
                if (reportLength >= 94)
                {
                    reportLength = 94;
                    offset = 1;
                    return ConnectionType.BT;
                }
                else
                {
                    reportLength = 64;
                    offset = 0;
                    return ConnectionType.USB;
                }
            }
            else
            {
                if (reportLength == 32)
                {
                    reportLength = 64;
                    offset = 0;
                    return ConnectionType.USB;
                }
                else
                {
                    throw new Exception("This application doesn't support DualShock 4 over Bluetooth");
                    Dispose(); // bluetooth not supported for now
                    reportLength = 77;
                    offset = 0; // ???
                    return ConnectionType.BT;
                }
            }
        }

        /// <summary>
        /// Releases the controller from API
        /// </summary>
        /// <returns></returns>
        public void Dispose()
        {
            ReadOnly = false;
            Working = false;
            ResetSettings();
            Write();
            if (this.ConnectionType == ConnectionType.USB && hapticFeedback != null)
            {
                hapticFeedback.Dispose();
            }
            DSDevice.Dispose();
        }
    }

    public class State
    {
        public bool square, triangle, circle, cross;
        public bool DpadUp, DpadDown, DpadLeft, DpadRight;
        public bool L1, L3, R1, R3, R2Btn, L2Btn;
        public byte L2, R2;
        public bool share, options, ps, touch1, touch2, touchBtn, touchRight, touchLeft;
        public bool touchFinger1, touchFinger2;
        public bool micBtn;
        public int RX, RY, LX, LY;
        public int TouchPacketNum;
        public int SensorTimestamp;
        public Touchpad trackPadTouch0 = new Touchpad();
        public Touchpad trackPadTouch1 = new Touchpad();
        public Gyro gyro = new Gyro();
        public Accelerometer accelerometer = new Accelerometer();

        public void SetDPadState(int dpad_state)
        {
            switch (dpad_state)
            {
                case 0:
                    DpadUp = true;
                    DpadDown = false;
                    DpadLeft = false;
                    DpadRight = false;
                    break;

                case 1:
                    DpadUp = true;
                    DpadDown = false;
                    DpadLeft = false;
                    DpadRight = true;
                    break;

                case 2:
                    DpadUp = false;
                    DpadDown = false;
                    DpadLeft = false;
                    DpadRight = true;
                    break;

                case 3:
                    DpadUp = false;
                    DpadDown = true;
                    DpadLeft = false;
                    DpadRight = true;
                    break;

                case 4:
                    DpadUp = false;
                    DpadDown = true;
                    DpadLeft = false;
                    DpadRight = false;
                    break;

                case 5:
                    DpadUp = false;
                    DpadDown = true;
                    DpadLeft = true;
                    DpadRight = false;
                    break;

                case 6:
                    DpadUp = false;
                    DpadDown = false;
                    DpadLeft = true;
                    DpadRight = false;
                    break;

                case 7:
                    DpadUp = true;
                    DpadDown = false;
                    DpadLeft = true;
                    DpadRight = false;
                    break;

                default:
                    DpadUp = false;
                    DpadDown = false;
                    DpadLeft = false;
                    DpadRight = false;
                    break;
            }
        }
    }
}