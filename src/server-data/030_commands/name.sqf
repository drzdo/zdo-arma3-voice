["name",
"Assign a custom name/label to the object the player is looking at. Triggers: this is Alpha, name this vehicle Bravo.",
"{position?: Position, label: string}",
{
    params ["_args", "_lookAtPosition"];
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    private _label = _args getOrDefault ["label", ""];
    [_pos, _label] call zdoArmaVoice_fnc_nameObject
}] call zdoArmaVoice_fnc_coreRegisterCommand
