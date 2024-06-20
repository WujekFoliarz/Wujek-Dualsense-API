﻿using HidSharp;
using static Wujek_Dualsense_API.LED;
using static Wujek_Dualsense_API.Motion;

namespace Wujek_Dualsense_API
{
    public class Dualsense : IDisposable
    {
        private DeviceStream DSDevice;
        public ConnectionType ConnectionType;
        private int reportLength = 0;
        private int offset = 0;
        public bool Working { get; private set; } = false;

        private Task transitionTask;
        private CancellationTokenSource cts = new();
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
        private Vibrations.VibrationType rumbleMode = Vibrations.VibrationType.Standard_Rumble;
        private HapticFeedback hapticFeedback;
        private Dictionary<string, byte[]> WAV_CACHE = new Dictionary<string, byte[]>();
        private bool bt_initialized = false;

        public State ButtonState = new State();
        public byte LeftRotor = 0;
        public byte RightRotor = 0;
        public int ControllerNumber = 0;

        public Dualsense(int ControllerNumber)
        {
            DeviceList list = DeviceList.Local;
            List<Device> devices = new List<Device>();

            foreach (var deviceInfo in list.GetHidDevices())
            {
                if (deviceInfo.VendorID == 1356 && deviceInfo.ProductID == 3302)
                {
                    reportLength = deviceInfo.GetMaxOutputReportLength();
                    devices.Add(deviceInfo);
                }
            }

            try
            {
                DSDevice = devices[ControllerNumber].Open();
                this.ControllerNumber = ControllerNumber;
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new Exception("Couldn't find Dualsense device number: " + ControllerNumber);
            }

            this.ConnectionType = getConnectionType();

            if (this.ConnectionType == ConnectionType.USB)
            {
                SetSpeakerVolume(100);
                SetMicrophoneVolume(100);
                hapticFeedback = new HapticFeedback(ControllerNumber);
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
            SetVibrationType(Vibrations.VibrationType.Standard_Rumble);
            SetPlayerLED(PlayerLED.OFF);
            SetMicrophoneLED(MicrophoneLED.OFF);
            SetLightbar(0, 0, 0);
            if (this.ConnectionType == ConnectionType.BT)
                SetLightbar(0, 0, 255);
            SetLeftTrigger(TriggerType.TriggerModes.Rigid_B, 0, 0, 0, 0, 0, 0, 0);
            SetRightTrigger(TriggerType.TriggerModes.Rigid_B, 0, 0, 0, 0, 0, 0, 0);
            SetStandardRumble(0, 0);
            TurnMicrophoneOn();
            SetMicrophoneVolume(100);
        }

        public void PlayHaptics(string PathToWAV, float FileVolumeSpeaker, float fileVolumeLeftActuator, float FileVolumeRightActuator, bool ClearBuffer)
        {
            if (this.ConnectionType == ConnectionType.USB && rumbleMode == Vibrations.VibrationType.Haptic_Feedback)
            {
                new Task(() =>
                {
                    hapticFeedback.setVolume(FileVolumeSpeaker, fileVolumeLeftActuator, FileVolumeRightActuator);

                    if (!WAV_CACHE.Keys.Contains(PathToWAV))
                    {
                        byte[] file = File.ReadAllBytes(PathToWAV);
                        WAV_CACHE.Add(PathToWAV, file);

                        try
                        {
                            if (ClearBuffer)
                                hapticFeedback.bufferedWaveProvider.ClearBuffer();

                            hapticFeedback.bufferedWaveProvider.AddSamples(file, 0, file.Length);
                        }
                        catch (Exception e)
                        {
                            hapticFeedback.bufferedWaveProvider.ClearBuffer();
                            hapticFeedback.bufferedWaveProvider.AddSamples(file, 0, file.Length);
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
                                        hapticFeedback.bufferedWaveProvider.ClearBuffer();

                                    hapticFeedback.bufferedWaveProvider.AddSamples(pair.Value, 0, pair.Value.Length);
                                }
                                catch (Exception e)
                                {
                                    hapticFeedback.bufferedWaveProvider.ClearBuffer();
                                    hapticFeedback.bufferedWaveProvider.AddSamples(pair.Value, 0, pair.Value.Length);
                                }
                            }
                        }
                    }
                }).Start();
            }
        }

        public void ClearHapticFeedbackCache()
        {
            WAV_CACHE.Clear();
        }

        public void SetVibrationType(Vibrations.VibrationType vibrationType)
        {
            rumbleMode = vibrationType;
        }

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

        public void SetLeftTrigger(TriggerType.TriggerModes triggerMode, int forceOne, int forceTwo, int forceThree, int forceFour, int fourceFive, int fourceSix, int fourceSeven)
        {
            LeftTriggerMode = triggerMode;
            LeftTriggerForces[0] = forceOne;
            LeftTriggerForces[1] = forceTwo;
            LeftTriggerForces[2] = forceThree;
            LeftTriggerForces[3] = forceFour;
            LeftTriggerForces[4] = fourceFive;
            LeftTriggerForces[5] = fourceSix;
            LeftTriggerForces[6] = fourceSeven;
        }

        public void SetRightTrigger(TriggerType.TriggerModes triggerMode, int forceOne, int forceTwo, int forceThree, int forceFour, int fourceFive, int fourceSix, int fourceSeven)
        {
            RightTriggerMode = triggerMode;
            RightTriggerForces[0] = forceOne;
            RightTriggerForces[1] = forceTwo;
            RightTriggerForces[2] = forceThree;
            RightTriggerForces[3] = forceFour;
            RightTriggerForces[4] = fourceFive;
            RightTriggerForces[5] = fourceSix;
            RightTriggerForces[6] = fourceSeven;
        }

        public void TurnMicrophoneOn()
        {
            microphoneStatus = Microphone.MicrophoneStatus.ON;
        }

        public void TurnMicrophoneOff()
        {
            microphoneStatus = Microphone.MicrophoneStatus.OFF;
        }

        public LED.MicrophoneLED GetMicrophoneLED()
        {
            return micLed;
        }

        public void SetMicrophoneVolume(int Volume)
        {
            if (Volume >= 0 && Volume <= 200)
                MicrophoneVolume = Convert.ToByte(int.Parse(Convert.ToString(Volume)).ToString("X"), 16);
            else
                throw new ArgumentException("The microphone volume cannot be lower than 0 or higher than 200.");
        }

        public void SetSpeakerVolume(int Volume)
        {
            if (Volume != 0)
                Volume = Volume + 40;

            if (Volume >= 0 && Volume <= 240)
                SpeakerVolume = Convert.ToByte(int.Parse(Convert.ToString(Volume)).ToString("X"), 16);
            else
                throw new ArgumentException("The speaker volume cannot be lower than 0 or higher than 200.");
        }

        public void SetLightbar(int R, int G, int B)
        {
            if (transitionTask != null && !transitionTask.IsCompleted) // Cancel any transitions
                cts.Cancel();

            lightbar.R = R;
            lightbar.G = G;
            lightbar.B = B;
        }

        public void SetLightbarTransition(int R, int G, int B, int transitionSteps, int transitionDelay)
        {
            if (transitionTask != null && !transitionTask.IsCompleted)
                cts.Cancel();

            if (transitionTask != null && transitionTask.IsCompleted)
            {
                cts.Dispose();
                cts = new();
            }

            transitionTask = new Task(() => transitionLightBar(R, G, B, transitionSteps, transitionDelay, cts.Token));
            transitionTask.Start();
        }

        private void transitionLightBar(int R, int G, int B, int transitionSteps, int transitionDelay, CancellationToken token)
        {
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


        public void SetPlayerLED(PlayerLED PlayerLED)
        {
            _playerLED = PlayerLED;
        }

        public void SetMicrophoneLED(MicrophoneLED MicLED)
        {
            micLed = MicLED;
        }

        private void Read()
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
            ButtonState.trackPadTouch0.ID = (byte)(ButtonStates[33 + offset] & 0x7F);
            ButtonState.trackPadTouch0.IsActive = (ButtonStates[33 + offset] & 0x80) == 0;
            ButtonState.trackPadTouch0.X = ((ButtonStates[35 + offset] & 0x0F) << 8) | ButtonStates[34];
            ButtonState.trackPadTouch0.Y = ((ButtonStates[36 + offset]) << 4) | ((ButtonStates[35] & 0xF0) >> 4);

            // trackpad touch
            ButtonState.trackPadTouch1.ID = (byte)(ButtonStates[37 + offset] & 0x7F);
            ButtonState.trackPadTouch1.IsActive = (ButtonStates[37 + offset] & 0x80) == 0;
            ButtonState.trackPadTouch1.X = ((ButtonStates[39 + offset] & 0x0F) << 8) | ButtonStates[38];
            ButtonState.trackPadTouch1.Y = ((ButtonStates[40 + offset]) << 4) | ((ButtonStates[39] & 0xF0) >> 4);

            // accelerometer
            ButtonState.accelerometer.X = BitConverter.ToInt16(new byte[] { ButtonStates[16 + offset], ButtonStates[17 + offset] }, 0);
            ButtonState.accelerometer.Y = BitConverter.ToInt16(new byte[] { ButtonStates[18 + offset], ButtonStates[19 + offset] }, 0);
            ButtonState.accelerometer.Z = BitConverter.ToInt16(new byte[] { ButtonStates[20 + offset], ButtonStates[21 + offset] }, 0);

            // gyrometer
            ButtonState.gyro.Pitch = BitConverter.ToInt16(new byte[] { ButtonStates[22 + offset], ButtonStates[23 + offset] }, 0);
            ButtonState.gyro.Yaw = BitConverter.ToInt16(new byte[] { ButtonStates[24 + offset], ButtonStates[25 + offset] }, 0);
            ButtonState.gyro.Roll = BitConverter.ToInt16(new byte[] { ButtonStates[26 + offset], ButtonStates[27 + offset] }, 0);
        }

        private void Write()
        {
            byte[] outReport = new byte[reportLength];

            if (this.ConnectionType == ConnectionType.USB)
            {
                outReport[0] = 2;
                outReport[1] = (byte)rumbleMode;
                outReport[2] = 0x1 | 0x2 | 0x4 | 0x10 | 0x40;
                outReport[3] = (byte)RightRotor; // right low freq motor 0-255
                outReport[4] = (byte)LeftRotor; // left low freq motor 0-255
                outReport[5] = 0x7C; // <-- headset volume
                outReport[6] = (byte)SpeakerVolume; // <-- speaker volume
                outReport[7] = (byte)MicrophoneVolume; // <-- mic volume
                outReport[8] = 0x7C; // <-- no idea what that does
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
                    outReport[3] = 0x1 | 0x2 | 0x4 | 0x10 | 0x40;
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

            try
            {
                DSDevice.WriteAsync(outReport, 0, reportLength);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + e.Source + e.StackTrace);
                Working = false;
            }
        }

        private ConnectionType getConnectionType()
        {
            if (reportLength == 48)
            {
                reportLength = 64;
                offset = 0;
                return ConnectionType.USB;
            }
            else if (reportLength == 547)
            {
                reportLength = 78;
                offset = 1;
                return ConnectionType.BT;
            }
            else { Console.WriteLine(reportLength); throw new ArgumentException("Could not specify connection type."); }
        }

        public void Dispose()
        {
            Working = false;
            ResetSettings();
            Write();
            if (this.ConnectionType == ConnectionType.USB)
                hapticFeedback.Dispose();
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
                    DpadLeft = false;
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
