zdoArmaVoice_fnc_heal = {
params ["_netIds"];
{
    private _u = _x call BIS_fnc_objectFromNetId;
    [_u] call ace_medical_ai_fnc_healSelf;
} forEach _netIds;
"ok"
}
