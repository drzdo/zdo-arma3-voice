zdoArmaVoice_fnc_nameObject = {
params ["_pos", "_label"];
private _obj = nearestObject [_pos, "All"];
if (isNull _obj) exitWith { "no object" };
private _netId = _obj call BIS_fnc_netId;
private _varName = "zdoArmaVoice_named_" + _label;
missionNamespace setVariable [_varName, _netId];
systemChat format ["Named: %1 = %2", _label, _netId];
_netId
}
