# MuteSound

## Feature

- Mute or replace certain sound names in the game.
- For example, you can mute the "CharSFX_Lotus_Revive" sound and leave the "IntSFX_LotusClose" sound intact. This allows you to mute specific sounds while keeping others.

## Tutorial: Mod Installation and Sound Configuration

### 1. Install Mod

- After installing the mod, start the game.
- The mod will generate two files in the `BepInEx\config` directory: 
  - `muteSoundNames.json`
  - `replaceSoundNames.json`

### 2. muteSoundNames.json

The `muteSoundNames.json` file contains a list of sound names to be muted. Here's an example of how the file should look:
Player_SFX_Dash is dash
Player_SFX_ReflectMove is parry sound
use Toast Sound Name to get sound name

```json
[
  "Player_SFX_Dash",
  "Player_SFX_ReflectMove"
]
```
### 3. replaceSoundNames.json support mp3 wav

The `replaceSoundNames.json`

```json
{
  "Player_SFX_Dash": "E:/Download/sound1.mp3",
  "Player_SFX_ReflectMove": "E:/Download/sound2.mp3"
}
```
or you can put file in BepInEx\config
```json
{
  "Player_SFX_Dash": "sound1.mp3",
  "Player_SFX_ReflectMove": "sound2.mp3"
}
```
