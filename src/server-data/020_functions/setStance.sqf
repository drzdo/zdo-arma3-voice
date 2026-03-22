zdoArmaVoice_fnc_setStance = {
params ["_netIds", "_stance"];
{ (_x call BIS_fnc_objectFromNetId) setUnitPos _stance } forEach _netIds;
"ok"
}
