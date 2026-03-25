zdoArmaVoice_fnc_commandMark = {
    params ["_args", "_lookAtPosition", "_units"];
    private _posSpec = _args getOrDefault ["position", "lookAt"];
    private _pos = if (_posSpec isEqualType "" && {_posSpec == "self"} && {count _units > 0}) then {
        getPosATL ((_units select 0) call BIS_fnc_objectFromNetId)
    } else {
        [_posSpec, _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition
    };
    private _label = _args getOrDefault ["label", "Mark"];
    private _id = format ["zdoArmaVoice_%1", floor random 99999];
    private _marker = createMarker [_id, _pos];
    _marker setMarkerType "mil_dot";
    _marker setMarkerText _label;
    _marker setMarkerColor "ColorBlack";
    systemChat format ["Marker: %1", _label]
};
["mark",
"Create a map marker. Triggers: mark this position, mark as Bravo, mark your location as Alpha, отметь позицию, поставь маркер. Use position ""self"" if the player asks a unit to mark their own location.",
"{position?: Position or ""self"", label?: string}",
zdoArmaVoice_fnc_commandMark] call zdoArmaVoice_fnc_coreRegisterCommand
