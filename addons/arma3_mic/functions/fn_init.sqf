// ArmaVoice — init (preInit)
// Registers CBA settings, keybind, reconnect loop, and per-frame handler.

// Load CBA settings
call compile preprocessFileLineNumbers "\arma3_mic\cba_settings.sqf";

// Frame counter for state push throttling
arma3_mic_frameCount = 0;

// PTT keybind — captures look target at press and release, sends to extension
["ArmaVoice", "arma3_mic_ptt", ["Push to Talk", "Hold to speak a voice command"],
{
    // Key down
    private _lookPos = if (visibleMap) then {
        screenToWorld getMousePosition
    } else {
        screenToWorld [0.5, 0.5]
    };
    "arma3_mic" callExtension ["ptt", ["down", str _lookPos]];
},
{
    // Key up
    private _lookPos = if (visibleMap) then {
        screenToWorld getMousePosition
    } else {
        screenToWorld [0.5, 0.5]
    };
    "arma3_mic" callExtension ["ptt", ["up", str _lookPos]];
},
[0xC7, [false, false, false]]] call CBA_fnc_addKeybind;
// Default: Home key, no modifiers

// Check if extension is loaded
private _ver = "arma3_mic" callExtension "status";
if (_ver == "") then {
    diag_log "ArmaVoice: ERROR — extension arma3_mic_x64.dll not loaded!";
    systemChat "ArmaVoice: extension NOT loaded — check that arma3_mic_x64.dll is in the mod folder";
} else {
    diag_log "ArmaVoice: extension loaded";
    systemChat "ArmaVoice: extension loaded";
};

// Per-frame handler — state push + RPC poll
addMissionEventHandler ["EachFrame", {
    // Skip if not connected
    if (("arma3_mic" callExtension "status") == "0") exitWith {};

    // Throttled state push
    arma3_mic_frameCount = arma3_mic_frameCount + 1;
    if (arma3_mic_frameCount >= arma3_mic_stateInterval) then {
        arma3_mic_frameCount = 0;

        private _pos = getPosASL player;
        private _dir = getDirVisual player;
        private _nearby = nearestObjects [player, ["Man"], 50] select {
            alive _x && _x != player
        };
        private _units = _nearby apply {
            [_x call BIS_fnc_netId, getPosASL _x]
        };
        private _stateStr = str [_pos, _dir, _units];
        "arma3_mic" callExtension ["state", [_stateStr]];
    };

    // Poll for inbound RPC (every frame)
    private _cmd = "arma3_mic" callExtension "poll";
    if (_cmd != "") then {
        (parseSimpleArray _cmd) params ["_id", "_sqf"];
        private _result = call compile _sqf;
        "arma3_mic" callExtension ["respond", [_id, str _result]];
    };
}];

// Reconnect loop — runs in parallel, retries every 10 seconds
[] spawn {
    while {true} do {
        if (("arma3_mic" callExtension "status") == "0") then {
            private _addr = arma3_mic_serverHost + ":" + str (round arma3_mic_serverPort);
            "arma3_mic" callExtension ["connect", [_addr]];
            systemChat format ["ArmaVoice: connecting to %1...", _addr];
        } else {
            // Connected — check once, log on first connect
            if (isNil "arma3_mic_wasConnected") then {
                arma3_mic_wasConnected = true;
                systemChat "ArmaVoice: connected";
            };
        };
        sleep 10;
    };
};

diag_log "ArmaVoice: initialized";
