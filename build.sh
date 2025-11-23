#!/bin/bash

# Set your 7DTD installation path
GAME_DIR="${GAME_DIR:-$HOME/.local/share/7DaysToDie}"

mcs -target:library \
    -out:BotCommandMod.dll \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/mscorlib.dll" \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/netstandard.dll" \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/System.dll" \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/System.Core.dll" \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/Assembly-CSharp.dll" \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/UnityEngine.CoreModule.dll" \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/0Harmony.dll" \
    Harmony/BotCommandPatch.cs
