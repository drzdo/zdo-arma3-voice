# Zdo Arma Voice

Voice command and NPC dialog system for Arma 3. Speak to control your squad and talk to NPCs — they respond with AI-generated voice and spatial audio.

Works in any language — English, Russian, Ukrainian, etc.

## How it works

Hold PTT (Caps Lock = radio, Tab = direct/nearby) and speak naturally. An LLM parses your speech into commands and executes them in-game.

Two PTT modes:

- **Caps Lock (radio)** — all squad units hear you, response plays with radio effect
- **Tab (direct)** — only units within 20m hear you, response plays as spatial audio

## Install

See [doc/SETUP.md](doc/SETUP.md) for installation, configuration, and build instructions.

There may be bugs. Report them in `Issues`.

## Documentation

- [doc/SETUP.md](doc/SETUP.md) — installation, configuration, Russian language setup
- [doc/DEVELOPMENT.md](doc/DEVELOPMENT.md) — building, releasing, CI/CD
- [doc/DESIGN-v1.md](doc/DESIGN-v1.md) — architecture and design

## Voice Commands

### Movement

| Say                                     | What happens                                                  |
| --------------------------------------- | ------------------------------------------------------------- |
| "Second, third — move to that building" | Units #2 and #3 move to your crosshair                        |
| "Второй, иди туда"                      | Unit #2 moves to your crosshair                               |
| "Everyone, move 100 meters north"       | Whole squad moves 100m north                                  |
| "Red team, move to marker Alpha"        | Red team moves to map marker                                  |
| "Taylor, move to Braun"                 | One unit moves to another unit's position                     |
| "Regroup!" / "Ко мне!"                  | Units return to the player                                    |
| "Garrison that building"                | Units enter building at crosshair and spread across positions |
| "Get inside" / "В здание"               | Each unit enters the nearest building to them                 |
| "Drive there" / "Езжай туда"            | Works for vehicles too — driver moves to crosshair            |

### Combat

| Say                                    | What happens                                                          |
| -------------------------------------- | --------------------------------------------------------------------- |
| "Attack that guy" / "Огонь по нему"    | Engage target at crosshair. Finds nearest enemy automatically         |
| "Open fire" / "Weapons free"           | Fire at will                                                          |
| "Hold fire" / "Не стрелять"            | Cease fire                                                            |
| "Suppress that position" / "Подавить!" | Suppressive fire at crosshair                                         |
| "Pop smoke" / "Дым!"                   | Unit throws a smoke grenade from inventory. "Pop red smoke" for color |

### Stance, Speed & Behaviour

| Say                         | What happens                                                                |
| --------------------------- | --------------------------------------------------------------------------- |
| "Hit the dirt!" / "Ложись!" | Go prone instantly                                                          |
| "Crouch" / "Присядь"        | Crouch                                                                      |
| "Stand up" / "Встань"       | Stand                                                                       |
| "Sprint!" / "Бегом!"        | Full speed                                                                  |
| "Walk" / "Шагом"            | Slow speed                                                                  |
| "Go stealth" / "Скрытно"    | Stealth mode                                                                |
| "Combat mode" / "К бою"     | Combat mode                                                                 |
| "Copy my stance"            | Unit continuously mirrors player's posture until given a new stance command |

Commands can be combined: "run to me" = regroup + sprint, "crawl to me" = regroup + prone.

### Stop & Hold

| Say                                 | What happens                           |
| ----------------------------------- | -------------------------------------- |
| "Stop!" / "Стой!"                   | Cancel current action, stay responsive |
| "Hold position" / "Держать позицию" | Lock in place until new move order     |

### Vehicles

| Say                                   | What happens                                                      |
| ------------------------------------- | ----------------------------------------------------------------- |
| "Get in" / "В машину"                 | If close (<7m) — teleports into vehicle. If far — moves toward it |
| "Get in as driver" / "Садись за руль" | Roles: driver, gunner, commander, cargo                           |
| "Get out" / "Из машины"               | Dismount                                                          |

> **Note:** Getting in may require two commands. First "get in" makes the unit walk to the vehicle. Second "get in" (when close enough) puts them inside.

### Reports (voice response via TTS)

| Say                                | What happens                                                 |
| ---------------------------------- | ------------------------------------------------------------ |
| "Report contacts" / "Кого видишь?" | Terse radio-style report: type, distance, azimuth, direction |
| "Where are you?" / "Где ты?"       | Approximate position (distance and bearing rounded)          |
| "Status report" / "Как дела?"      | Health/wounds report                                         |

### Dialog (NPC conversation)

| Say                        | What happens                                                  |
| -------------------------- | ------------------------------------------------------------- |
| "Miller, what do you see?" | NPC responds in character via voice                           |
| "Петрович, что впереди?"   | NPC personality varies by side (NATO/CSAT/guerrilla/civilian) |

### Teams

Teams have multiple names — use whichever feels natural:

| Team   | Names                                                                           |
| ------ | ------------------------------------------------------------------------------- |
| Red    | "red team", "team 1", "team A", "team alpha", "первая группа", "группа А"       |
| Green  | "green team", "team 2", "team B", "team bravo", "вторая группа", "группа Б"     |
| Blue   | "blue team", "team 3", "team C", "team charlie", "третья группа", "группа В"    |
| Yellow | "yellow team", "team 4", "team D", "team delta", "четвёртая группа", "группа Г" |

| Say                                            | What happens        |
| ---------------------------------------------- | ------------------- |
| "Assign second and third to red team"          | Units join the team |
| "Remove from team" / "Из группы"               | Unassign from team  |
| "Red team, move there" / "Первая группа, туда" | Address whole team  |

### Map & Naming

| Say                                          | What happens                                       |
| -------------------------------------------- | -------------------------------------------------- |
| "Mark this as Bravo" / "Отметь Альфа"        | Creates map marker at crosshair                    |
| "Mark your location as Alpha"                | Unit marks its own position on the map             |
| "This is vehicle Alpha" / "Это машина Альфа" | Names the object at crosshair for future reference |
| "Move to vehicle Alpha"                      | Uses previously named position                     |

### Loot

| Say                             | What happens                                                        |
| ------------------------------- | ------------------------------------------------------------------- |
| "Loot this area" / "Собери всё" | Unit collects weapons/gear from bodies and weapon piles within 100m |

> Looting runs in the background. Unit walks to each pile/body, collects items into a nearby vehicle (or onto itself if no vehicle). Say "stop" or "regroup" or give a move order to cancel.

### Switch POV

| Say                          | What happens                       |
| ---------------------------- | ---------------------------------- |
| "Let me look from your eyes" | Switch to the unit's point of view |
| "That's enough" / "Go back"  | Return to your original unit       |

> These commands appear conditionally — "switch to" only when you're in your own body, "switch back" only when controlling another unit.

### Targeting

- **"there" / "that building" / "туда"** — crosshair position (raycasts to objects, not just terrain)
- **"100m forward" / "200m north-west"** — relative to player. Supports all 16 compass directions
- **"bearing 320, 200m"** — explicit azimuth
- **"marker Alpha"** — map marker (matched by display name)
- **"vehicle Alpha"** — previously named object
- **"to Braun"** — another unit's current position

### Unit selection

- **"second" / "второй"** — unit #2 in squad
- **"Miller" / "Петрович"** — by name
- **"red team" / "группа А" / "team 1"** — team color (see Teams above)
- **"everyone" / "все"** — whole squad
- No unit mentioned — reuses last addressed units (falls back to whole squad on first command)
