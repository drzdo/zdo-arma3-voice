params ["_label"];
private _varName = "zdoArmaMic_named_" + _label;
private _netId = missionNamespace getVariable [_varName, ""];
if (_netId == "") exitWith { [] };
getPosASL (_netId call BIS_fnc_objectFromNetId)
