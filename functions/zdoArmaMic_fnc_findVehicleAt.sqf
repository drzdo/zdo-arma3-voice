params ["_pos", ["_radius", 15]];
private _veh = nearestObject [_pos, "LandVehicle"];
diag_log format ["ArmaVoice findVehicleAt: pos=%1 radius=%2 nearest=%3 dist=%4", _pos, _radius, _veh, if (!isNull _veh) then { _veh distance2D _pos } else { -1 }];
if (!isNull _veh && _veh distance2D _pos < _radius) exitWith { _veh };
_veh = nearestObject [_pos, "Air"];
if (!isNull _veh && _veh distance2D _pos < _radius) exitWith { _veh };
_veh = nearestObject [_pos, "Ship"];
if (!isNull _veh && _veh distance2D _pos < _radius) exitWith { _veh };
diag_log "ArmaVoice findVehicleAt: no vehicle found";
objNull
