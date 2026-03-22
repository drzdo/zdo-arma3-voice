zdoArmaVoice_fnc_commandMark = {
    params ["_args", "_lookAtPosition", "_units"];
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    private _label = _args getOrDefault ["label", "Mark"];
    private _id = format ["zdoArmaVoice_%1", floor random 99999];
    private _marker = createMarker [_id, _pos];
    _marker setMarkerType "mil_dot";
    _marker setMarkerText _label;
    _marker setMarkerColor "ColorBlack";
    systemChat format ["Marker: %1", _label]
};
["mark",
"Create a map marker at a position. If no label given, use Mark. Triggers: mark this position, mark as Bravo.",
"{position?: Position, label?: string}",
zdoArmaVoice_fnc_commandMark] call zdoArmaVoice_fnc_coreRegisterCommand
