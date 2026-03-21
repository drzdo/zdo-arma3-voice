params ["_pos", ["_radius", 15]];
private _veh = nearestObject [_pos, "LandVehicle"];
if (!isNull _veh && _veh distance2D _pos < _radius) exitWith { _veh };
_veh = nearestObject [_pos, "Air"];
if (!isNull _veh && _veh distance2D _pos < _radius) exitWith { _veh };
_veh = nearestObject [_pos, "Ship"];
if (!isNull _veh && _veh distance2D _pos < _radius) exitWith { _veh };
objNull
