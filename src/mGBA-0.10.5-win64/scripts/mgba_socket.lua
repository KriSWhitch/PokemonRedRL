package.path = package.path .. ";D:\\Programming\\PokemonRedRL\\src\\mGBA-0.10.5-win64\\lua\\?.lua"
package.cpath = package.cpath .. ";D:\\Programming\\PokemonRedRL\\src\\mGBA-0.10.5-win64\\lua\\?.dll"

local socket = require("socket")

-- Configuration
local HOST = "127.0.0.1"
local PORT = 12345
local TIMEOUT = 0.1 -- Short timeout for non-blocking mode

-- Memory addresses
local PLAYER_HP_ADDR = 0xD16B
local PLAYER_X_ADDR = 0xD362
local PLAYER_Y_ADDR = 0xD361
local PLAYER_MAP_ADDR = 0xD35E  -- Current map ID

-- Constants
local EMULATOR_FRAME_RATE = 60;

-- Button codes
local BUTTONS = {
    ButtonA = 0x01,
    ButtonB = 0x02,
    ButtonSelect = 0x04,
    ButtonStart = 0x08,
    ButtonRight = 0x10,
    ButtonLeft = 0x20,
    ButtonUp = 0x40,
    ButtonDown = 0x80
}

-- Server initialization
local server = assert(socket.bind(HOST, PORT))
server:settimeout(TIMEOUT)
console:log("Server started at "..HOST..":"..PORT)

local client = nil
local previous_input = 0
local current_input = 0

local next_frame = EMULATOR_FRAME_RATE
local reset_before_next_frame = 15

-- Function to get player's current location
local function get_player_location()
    local x = emu:read8(PLAYER_X_ADDR)
    local y = emu:read8(PLAYER_Y_ADDR)
    local map = emu:read8(PLAYER_MAP_ADDR)
    return map, x, y
end

-- Main loop (called every frame)
local function frame_callback()
    next_frame = next_frame - 1;

    if next_frame < reset_before_next_frame & previous_input ~= 0 then
        emu:clearKeys(previous_input) 
        previous_input = 0
    end

    if next_frame > 0 then
        return
    end

    next_frame = EMULATOR_FRAME_RATE

    -- Input handling
    if current_input ~= 0 then
        previous_input = current_input
        emu:addKeys(current_input)
        current_input = 0 -- Reset input after applying
    end
    
    -- Accept new connections
    if not client then
        client = server:accept()
        if client then
            client:settimeout(TIMEOUT)
            console:log("Client connected")
        end
    end
    
    -- Command processing
    if client then
        local cmd, err = client:receive("*l")
        if cmd then
            console:log("Received command:"..cmd)
            
            local response
            if cmd == "get_state" then
                local hp = emu:read8(PLAYER_HP_ADDR)
                local map, x, y = get_player_location()
                response = string.format("%d,%d,%d,%d\n", hp, map, x, y)
            elseif BUTTONS[cmd] then
                current_input = BUTTONS[cmd]
                response = "OK\n"
            elseif cmd == "ping" then
                response = "pong\n"
            elseif cmd == "disconnect" then
                response = "BYE\n"
                client:send(response)
                client:close()
                client = nil
                return
            else
                response = "UNKNOWN_COMMAND\n"
            end
            
            client:send(response)
        elseif err == "closed" then
            client:close()
            client = nil
            console:log("Client disconnected")
        end
    end
end

-- Register frame callback
callbacks:add('frame', frame_callback)

console:log("Script initialized - waiting for connections")