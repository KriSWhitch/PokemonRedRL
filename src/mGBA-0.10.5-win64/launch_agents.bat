@echo off
setlocal enabledelayedexpansion

set BASE_PORT=12345
set MGBA_PATH="D:\Programming\PokemonRedRL\src\mGBA-0.10.5-win64\mGBA.exe"
set LUA_SCRIPT="D:\Programming\PokemonRedRL\src\mGBA-0.10.5-win64\scripts\mgba_socket.lua"
set ROM_PATH="D:\Programming\PokemonRedRL\src\data\roms\pokemon_red.gb"

for /L %%i in (0,1,19) do (
    set /a PORT=BASE_PORT + %%i
    start "" %MGBA_PATH% --port !PORT! --script %LUA_SCRIPT% !PORT! %ROM_PATH%
)

echo Started 20 mGBA instances on ports %BASE_PORT%-12364
pause