["mark",
"Create a map marker at the look target position. Triggers: mark this position, mark as Bravo. If no label given, use Mark.",
"{units: Units, position?: Position, label?: string}",
{
    params ["_args", "_lookAtPosition"];
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    private _label = _args getOrDefault ["label", "Mark"];
    [_pos, _label] call zdoArmaVoice_fnc_createMarker;
    systemChat format ["Marker: %1", _label]
}] call zdoArmaVoice_fnc_coreRegisterCommand
