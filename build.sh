#!/bin/bash

# Set your 7DTD installation path
GAME_DIR="${GAME_DIR:-$HOME/Software/SteamLibrary/steamapps/common/7 Days To Die}"

# Use csc (Roslyn compiler) instead of mcs
csc -target:library \
    -out:CHRANIBotTNG/CHRANIBotTNG.dll \
    -nostdlib \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/mscorlib.dll" \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/netstandard.dll" \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/System.dll" \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/System.Core.dll" \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/System.Xml.dll" \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/System.Xml.Linq.dll" \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/Assembly-CSharp.dll" \
    -r:"$GAME_DIR/7DaysToDie_Data/Managed/UnityEngine.CoreModule.dll" \
    -r:"/home/ecv/Software/SteamLibrary/steamapps/common/7 Days To Die/Mods/0_TFP_Harmony/0Harmony.dll" \
    Harmony/CHRANIBotTNG.cs
