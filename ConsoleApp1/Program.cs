using Wujek_Dualsense_API;

Dualsense dualsense = new Dualsense(0);
dualsense.Start();
dualsense.SetPlayerLED(LED.PlayerLED.PLAYER_1);
dualsense.SetLightbar(0, 0, 255);
Console.ReadLine();