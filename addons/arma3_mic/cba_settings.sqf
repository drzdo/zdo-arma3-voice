[
    "arma3_mic_serverHost", "EDITBOX",
    ["Server Host", "Hostname or IP of the ArmaVoice C# server"],
    "ArmaVoice",
    "127.0.0.1",
    false
] call CBA_fnc_addSetting;

[
    "arma3_mic_serverPort", "SLIDER",
    ["Server Port", "TCP port of the ArmaVoice C# server"],
    "ArmaVoice",
    [1024, 65535, 9500, 0],
    false
] call CBA_fnc_addSetting;

[
    "arma3_mic_stateInterval", "SLIDER",
    ["State Interval (frames)", "Send game state every N frames. Lower = more responsive, higher = less CPU"],
    "ArmaVoice",
    [1, 30, 3, 0],
    false
] call CBA_fnc_addSetting;

[
    "arma3_mic_enabled", "CHECKBOX",
    ["Enabled", "Enable/disable connection to ArmaVoice server"],
    "ArmaVoice",
    true,
    false
] call CBA_fnc_addSetting;
