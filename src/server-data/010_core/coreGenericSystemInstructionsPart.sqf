zdoArmaVoice_fnc_unitPersonality = {
    params ["_netId"];
    private _unit = _netId call BIS_fnc_objectFromNetId;
    private _side = str side _unit;
    switch (_side) do {
        case "WEST": { "You speak English with a professional NATO military tone." };
        case "EAST": { "You speak English with a Russian accent. Occasionally use Russian military terms." };
        case "GUER": { "You speak English with a rough, informal guerrilla fighter tone." };
        case "CIV": { "You speak English as a nervous civilian caught in a warzone." };
        default { "You always respond in English." };
    }
}
