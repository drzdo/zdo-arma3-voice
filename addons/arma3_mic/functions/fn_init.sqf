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
    private _lookPos = call zdoArmaMic_fnc_getLookTarget;
    "arma3_mic" callExtension toJSON createHashMapFromArray [
        ["t", "ptt"], ["dir", "down"], ["pos", _lookPos]
    ];
},
{
    // Key up
    private _lookPos = call zdoArmaMic_fnc_getLookTarget;
    "arma3_mic" callExtension toJSON createHashMapFromArray [
        ["t", "ptt"], ["dir", "up"], ["pos", _lookPos]
    ];
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

    // Player pos + dir every frame (needed for real-time spatial audio)
    "arma3_mic" callExtension toJSON createHashMapFromArray [
        ["t", "head"], ["p", getPosASL player], ["d", getDirVisual player]
    ];

    // Full state with nearby units — throttled
    arma3_mic_frameCount = arma3_mic_frameCount + 1;
    if (arma3_mic_frameCount >= arma3_mic_stateInterval) then {
        arma3_mic_frameCount = 0;

        private _nearby = nearestObjects [player, ["Man"], 50] select {
            alive _x && _x != player
        };
        private _units = _nearby apply {
            [_x call BIS_fnc_netId, getPosASL _x]
        };
        "arma3_mic" callExtension toJSON createHashMapFromArray [
            ["t", "state"], ["u", _units]
        ];
    };

    // Poll ALL inbound RPCs (drain queue each frame)
    private _cmd = "arma3_mic" callExtension "poll";
    while {_cmd != ""} do {
        private _msg = fromJSON _cmd;
        private _id = _msg get "id";
        private _sqf = _msg get "sqf";
        if (_id == 0) then {
            // Fire-and-forget — execute directly
            diag_log format ["ArmaVoice RPC fire: %1", _sqf];
            call compile _sqf;
        } else {
            // RPC with response expected
            diag_log format ["ArmaVoice RPC call (id=%1): %2", _id, _sqf];
            private _result = call compile _sqf;
            diag_log format ["ArmaVoice RPC result (id=%1): %2", _id, str _result select [0, 80]];
            "arma3_mic" callExtension toJSON createHashMapFromArray [
                ["t", "rpc"], ["id", _id], ["r", _result]
            ];
        };
        _cmd = "arma3_mic" callExtension "poll";
    };
}];

// Reconnect loop — runs in parallel, retries every 10 seconds
[] spawn {
    while {true} do {
        if (arma3_mic_enabled) then {
            if (("arma3_mic" callExtension "status") == "0") then {
                private _addr = arma3_mic_serverHost + ":" + str (round arma3_mic_serverPort);
                "arma3_mic" callExtension toJSON createHashMapFromArray [
                    ["t", "connect"], ["addr", _addr]
                ];
                systemChat format ["ArmaVoice: connecting to %1...", _addr];
            } else {
                if (isNil "arma3_mic_wasConnected") then {
                    arma3_mic_wasConnected = true;
                    systemChat "ArmaVoice: connected";
                };
            };
        };
        sleep 10;
    };
};

diag_log "ArmaVoice: initialized";
