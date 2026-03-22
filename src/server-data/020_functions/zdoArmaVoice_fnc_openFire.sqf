zdoArmaVoice_fnc_openFire = {
params ["_netIds"];
{
    private _u = _x call BIS_fnc_objectFromNetId;
    _u setCaptive false;
    _u enableAI "AUTOTARGET";
    _u enableAI "TARGET";
    _u enableAI "AUTOCOMBAT";
} forEach _netIds;
(group ((_netIds select 0) call BIS_fnc_objectFromNetId)) setCombatMode "RED";
"ok"
}
