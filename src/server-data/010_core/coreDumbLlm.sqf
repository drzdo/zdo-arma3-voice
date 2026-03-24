zdoArmaVoice_fnc_coreDumbLlm = {
    params ["_text"];
    private _clean = toLower _text;
    _clean = _clean regexReplace ["[^a-zа-яё0-9 ]", ""];
    _clean = _clean trim [" ", 0];

    private _match = zdoArmaVoice_dumbLlmMap getOrDefault [_clean, ""];
    if (_match == "") exitWith { nil };

    _match
};

private _stop = "{""units"":[""last""],""commands"":[{""command"":""stop"",""args"":{}}]}";
private _regroup = "{""units"":[""all""],""commands"":[{""command"":""regroup"",""args"":{}}]}";
private _combat = "{""units"":[""all""],""commands"":[{""command"":""behaviour"",""args"":{""mode"":""COMBAT""}}]}";
private _holdFire = "{""units"":[""all""],""commands"":[{""command"":""behaviour"",""args"":{""mode"":""AWARE""}}]}";
private _medic = "{""units"":[],""commands"":[{""command"":""medic"",""args"":{}}]}";
private _drop = "{""units"":[""all""],""commands"":[{""command"":""drop"",""args"":{}}]}";
private _stealth = "{""units"":[""all""],""commands"":[{""command"":""behaviour"",""args"":{""mode"":""STEALTH""}}]}";

zdoArmaVoice_dumbLlmMap = createHashMapFromArray [
    ["stop",            _stop],
    ["стоп",            _stop],
    ["halt",            _stop],
    ["freeze",          _stop],
    ["замри",           _stop],
    ["замрите",         _stop],
    ["стой",            _stop],
    ["стойте",          _stop],
    ["отставить",       _stop],

    ["regroup",         _regroup],
    ["ко мне",          _regroup],
    ["все ко мне",      _regroup],
    ["сюда",            _regroup],
    ["все сюда",        _regroup],
    ["come to me",      _regroup],
    ["on me",           _regroup],
    ["rally on me",     _regroup],

    ["open fire",       _combat],
    ["огонь",           _combat],
    ["fire",            _combat],
    ["fire at will",    _combat],
    ["стреляй",        _combat],
    ["стреляйте",      _combat],
    ["стрелять",        _combat],
    ["в бой",           _combat],
    ["к бою",           _combat],
    ["бой",             _combat],
    ["в атаку",         _combat],
    ["атакуем",         _combat],

    ["hold fire",       _holdFire],
    ["cease fire",      _holdFire],
    ["не стрелять",     _holdFire],
    ["не стреляй",      _holdFire],
    ["прекратить огонь", _holdFire],
    ["отбой",           _holdFire],

    ["медик",           _medic],
    ["medic",           _medic],
    ["врач",            _medic],
    ["санитар",         _medic],

    ["get down",        _drop],
    ["down",            _drop],
    ["вниз",            _drop],
    ["ложись",          _drop],
    ["ложить",          _drop],
    ["лежать",          _drop],
    ["лежа",            _drop],
    ["лёжа",            _drop],
    ["лежим",           _drop],
    ["на землю",        _drop],
    ["на пол",          _drop],

    ["тихо",            _stealth],
    ["тише",            _stealth],
    ["quiet",           _stealth],
    ["stealth",         _stealth]
]
