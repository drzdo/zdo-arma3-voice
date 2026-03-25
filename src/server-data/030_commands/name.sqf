zdoArmaVoice_fnc_commandName = {
    params ["_args", "_lookAtPosition", "_units"];
    private _label = _args getOrDefault ["label", ""];
    private _obj = nearestObject [_lookAtPosition, "All"];
    if (isNull _obj) exitWith { systemChat "No object found" };
    private _netId = _obj call BIS_fnc_netId;
    missionNamespace setVariable ["zdoArmaVoice_named_" + _label, _netId];
    systemChat format ["Named: %1 = %2", _label, _netId]
};
["name",
"Assign a custom name/label to the object at crosshair. Triggers: this is Alpha, name this vehicle Bravo, это Альфа, назови это Браво.",
"{label: string}",
zdoArmaVoice_fnc_commandName] call zdoArmaVoice_fnc_coreRegisterCommand
