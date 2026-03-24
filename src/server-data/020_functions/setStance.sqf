zdoArmaVoice_fnc_setStance = {
params ["_netIds", "_stance"];
{
    private _u = _x call BIS_fnc_objectFromNetId;
    if (vehicle _u == _u) then { _u setUnitPos _stance }
} forEach _netIds;
"ok"
}
