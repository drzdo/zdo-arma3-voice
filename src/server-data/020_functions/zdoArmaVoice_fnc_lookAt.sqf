zdoArmaVoice_fnc_lookAt = {
params ["_netIds", "_pos"];
private _units = _netIds apply { _x call BIS_fnc_objectFromNetId };
_units lookAt _pos;
"ok"
}
