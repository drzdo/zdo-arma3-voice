params ["_netIds"];
_netIds select {
    private _u = _x call BIS_fnc_objectFromNetId;
    alive _u && { !(_u getVariable ["ace_medical_isUnconscious", false]) }
}
