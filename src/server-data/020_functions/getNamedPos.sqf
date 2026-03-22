zdoArmaVoice_fnc_getNamedPos = {
params ["_label"];
private _varName = "zdoArmaVoice_named_" + _label;
private _netId = missionNamespace getVariable [_varName, ""];
if (_netId == "") exitWith { [] };
getPosASL (_netId call BIS_fnc_objectFromNetId)
}
