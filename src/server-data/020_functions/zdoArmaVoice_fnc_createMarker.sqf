zdoArmaVoice_fnc_createMarker = {
params ["_pos", "_text"];
private _id = format ["zdoArmaVoice_%1", floor random 99999];
private _marker = createMarker [_id, _pos];
_marker setMarkerType "mil_dot";
_marker setMarkerText _text;
_marker setMarkerColor "ColorBlack";
_id
}
