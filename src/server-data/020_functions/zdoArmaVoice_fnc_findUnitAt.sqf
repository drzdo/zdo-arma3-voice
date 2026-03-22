zdoArmaVoice_fnc_findUnitAt = {
params ["_pos", ["_radius", 15]];
private _units = _pos nearEntities ["Man", _radius] select { alive _x && _x != player };
if (count _units > 0) exitWith { _units select 0 };
objNull
}
