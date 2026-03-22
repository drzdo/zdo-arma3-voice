zdoArmaVoice_fnc_coreOnPlayerSay = {
    params ["_text"];
    systemChat format ["%1: %2", name player, _text]
}
