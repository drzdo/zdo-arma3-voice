params ["_netIds"];
{
    private _u = _x call BIS_fnc_objectFromNetId;
    _u setCaptive true;
    _u disableAI "AUTOTARGET";
    _u disableAI "TARGET";
    _u disableAI "AUTOCOMBAT";
} forEach _netIds;
(group ((_netIds select 0) call BIS_fnc_objectFromNetId)) setCombatMode "BLUE";
"ok"
