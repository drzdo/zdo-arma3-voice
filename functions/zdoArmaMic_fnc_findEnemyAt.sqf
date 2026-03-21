params ["_pos", ["_radius", 50]];
private _enemies = _pos nearEntities ["Man", _radius] select { alive _x && side _x != side player };
if (count _enemies > 0) exitWith { _enemies select 0 };
private _vehicles = _pos nearEntities ["LandVehicle", _radius] select { alive _x && side _x != side player };
if (count _vehicles > 0) exitWith { _vehicles select 0 };
objNull
