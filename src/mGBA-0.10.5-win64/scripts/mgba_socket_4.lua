package.path = package.path .. ";D:\\Programming\\PokemonRedRL\\src\\mGBA-0.10.5-win64\\lua\\?.lua"
package.cpath = package.cpath .. ";D:\\Programming\\PokemonRedRL\\src\\mGBA-0.10.5-win64\\lua\\?.dll"

local socket = require("socket")
local json = require("lunajson")

-- Configuration
local HOST = "127.0.0.1"
local PORT = 12348
if arg and arg[1] then
    PORT = tonumber(arg[1]) or PORT -- Если аргумент некорректный, используется значение по умолчанию
end
local TIMEOUT = 0.1 -- Short timeout for non-blocking mode

-- Memory addresses

-- Base game data
local PLAYER_HP_ADDR = 0xD16B
local PLAYER_X_ADDR = 0xD362
local PLAYER_Y_ADDR = 0xD361
local PLAYER_MAP_ADDR = 0xD35E           -- Current map ID
local PLAYER_MONEY_ADDR = 0xD347         -- Money (3 bytes in BCD format)
local PLAYER_BADGES_ADDR = 0xD356        -- Badges (bitmask: 0b76543210)
local PLAYER_DIRECTION_ADDR = 0xD367     -- Facing direction (0x00-0x03)

-- Pokemon party data
local PARTY_COUNT_ADDR = 0xD163          -- Number of Pokemon in party (1-6)

-- Party Pokemon data (each 44 bytes)
local PARTY_POKEMON_1_ADDR = 0xD164  -- First Pokemon
local PARTY_POKEMON_2_ADDR = 0xD190  -- Second Pokemon
local PARTY_POKEMON_3_ADDR = 0xD1BC   -- Third Pokemon
local PARTY_POKEMON_4_ADDR = 0xD1E8   -- Fourth Pokemon
local PARTY_POKEMON_5_ADDR = 0xD214   -- Fifth Pokemon
local PARTY_POKEMON_6_ADDR = 0xD240   -- Sixth Pokemon

-- Pokemon data structure offsets
local POKEMON_STRUCT = {
    SPECIES       = 0x00,  -- Species ID (1 byte)
    CURRENT_HP    = 0x01,  -- Current HP (2 bytes, little-endian)
    LEVEL         = 0x03,  -- Level (1 byte)
    STATUS        = 0x04,  -- Status condition (0x00 = normal)
    TYPE_1        = 0x05,  -- Type 1 (1 byte)
    TYPE_2        = 0x06,  -- Type 2 (1 byte)
    MOVE_1        = 0x08,  -- Move 1 ID
    MOVE_2        = 0x09,  -- Move 2 ID
    MOVE_3        = 0x0A,
    MOVE_4        = 0x0B,
    MAX_HP        = 0x22,  -- Max HP (2 bytes)
    ATTACK        = 0x23,  -- Attack stat (2 bytes)
    DEFENSE       = 0x25,  -- Defense stat (2 bytes)
    SPEED         = 0x27,  -- Speed stat (2 bytes)
    SPECIAL       = 0x29,  -- Special stat (2 bytes)
}

-- Inventory data
local BAG_ITEMS_ADDR = 0xD31D   -- Bag items ([ID][quantity] pairs, terminated with 0xFF)
local PC_ITEMS_ADDR = 0xD53D    -- PC items (same format as bag)

-- Battle data
local ENEMY_SPECIES_ADDR = 0xCFE5   -- Current enemy Pokemon species
local ENEMY_HP_ADDR = 0xCFE6        -- Enemy current HP (2 bytes)
local ENEMY_STATUS_ADDR = 0xCFE8    -- Enemy status condition

local ENEMY_COUNT = 0xD89C
local IN_BATTLE_FLAG = 0xD057
local BATTLE_WON_FLAG = 0xCD3A
local FIRST_ENEMY_TRAINER_ID_ADDR = 0xD8B0  -- Added for trainer battle detection

-- Miscellaneous
local DAYCARE_POKEMON_ADDR = 0xD30C  -- Daycare Pokemon data
local CURRENT_BOX_ADDR = 0xDA80      -- Current storage box (1-12)
local TRAINER_ID_ADDR = 0xD35A       -- Trainer ID (2 bytes)

-- Преобразуем таблицу BUTTONS в более удобный формат для битовых операций
local BUTTON_VALUES = {
    ButtonA = 0,
    ButtonB = 1,
    ButtonSelect = 2,
    ButtonStart = 3,
    ButtonRight = 4,
    ButtonLeft = 5,
    ButtonUp = 6,
    ButtonDown = 7
}

-- Server initialization
local server = assert(socket.bind(HOST, PORT))
server:settimeout(TIMEOUT)
console:log("Server started at "..HOST..":"..PORT)

local client = nil
local previous_input = 0
local current_input = 0

-- Constants
local EMULATOR_FRAME_RATE = 60

-- Конфигурация управления
local BUTTON_PRESS_DURATION = 15  -- Длительность нажатия кнопки в кадрах (примерно 250мс при 60fps)
local keyEventQueue = {}          -- Очередь событий нажатия кнопок

local next_frame = EMULATOR_FRAME_RATE

-- Функция для создания битовой маски из списка кнопок
local function createButtonMask(buttons)
    local mask = 0
    for _, button in ipairs(buttons) do
        if BUTTON_VALUES[button] then
            mask = mask | (1 << BUTTON_VALUES[button])
        end
    end
    return mask
end

-- Добавляем событие нажатия кнопки в очередь
local function enqueueButtonPress(button, duration)
    local mask = createButtonMask({button})
    local startFrame = emu:currentFrame()
    local endFrame = startFrame + (duration or BUTTON_PRESS_DURATION)
    
    table.insert(keyEventQueue, {
        mask = mask,
        startFrame = startFrame,
        endFrame = endFrame,
        pressed = false
    })
    
    console:log(string.format("Button enqueued: %s (mask: 0x%X, frames: %d-%d)", 
        button, mask, startFrame, endFrame))
end

-- Основная функция обработки ввода, вызывается каждый кадр
local function updateInput()
    local currentFrame = emu:currentFrame()
    local indexesToRemove = {}

    -- Обрабатываем все события в очереди
    for index, event in ipairs(keyEventQueue) do
        if currentFrame >= event.startFrame and currentFrame <= event.endFrame then
            if not event.pressed then
                -- Нажимаем кнопку в первый кадр события
                emu:addKeys(event.mask)
                event.pressed = true
                console:log(string.format("Button pressed: 0x%X at frame %d", event.mask, currentFrame))
            end
        elseif currentFrame > event.endFrame then
            -- Отпускаем кнопку после завершения события
            emu:clearKeys(event.mask)
            table.insert(indexesToRemove, index)
            console:log(string.format("Button released: 0x%X at frame %d", event.mask, currentFrame))
        end
    end

    -- Удаляем завершенные события из очереди
    for i = #indexesToRemove, 1, -1 do
        table.remove(keyEventQueue, indexesToRemove[i])
    end
end

-- Reads Pokemon data from memory
local function read_pokemon_data(base_addr)
    local data = {}
    
    data.species = emu:read8(base_addr + POKEMON_STRUCT.SPECIES) or 0
    data.level = emu:read8(base_addr + POKEMON_STRUCT.LEVEL) or 0
    data.status = emu:read8(base_addr + POKEMON_STRUCT.STATUS) or 0
    
    -- Read 2-byte values as little-endian
    data.current_hp = emu:read16(base_addr + POKEMON_STRUCT.CURRENT_HP) or 0
    data.max_hp = emu:read16(base_addr + POKEMON_STRUCT.MAX_HP) or 0
    
    data.moves = {
        emu:read8(base_addr + POKEMON_STRUCT.MOVE_1),
        emu:read8(base_addr + POKEMON_STRUCT.MOVE_2),
        emu:read8(base_addr + POKEMON_STRUCT.MOVE_3),
        emu:read8(base_addr + POKEMON_STRUCT.MOVE_4)
    }
    
    data.attack = emu:read16(base_addr + POKEMON_STRUCT.ATTACK) or 0
    data.defense = emu:read16(base_addr + POKEMON_STRUCT.DEFENSE) or 0
    data.speed = emu:read16(base_addr + POKEMON_STRUCT.SPEED) or 0
    data.special = emu:read16(base_addr + POKEMON_STRUCT.SPECIAL) or 0
    
    return data
end

local function in_trainer_battle()
    -- Check if we're in any battle first
    if emu:read8(IN_BATTLE_FLAG) == 0 then
        return false
    end
    
    -- Check if this is a trainer battle by looking at the enemy data structure
    -- Trainer battles have trainer IDs for each Pokémon
    local first_pokemon_trainer_id = emu:read16(FIRST_ENEMY_TRAINER_ID_ADDR)  -- Trainer ID of first enemy Pokémon
    
    -- Wild Pokémon will have 0 for trainer ID, trainer battles will have actual IDs
    return first_pokemon_trainer_id ~= 0
end

-- Gets player's current map position
local function get_player_location()
    local x = emu:read8(PLAYER_X_ADDR)
    local y = emu:read8(PLAYER_Y_ADDR)
    local map = emu:read8(PLAYER_MAP_ADDR)
    return map, x, y
end

-- Converts 3-byte BCD value to integer
local function read3ByteBCD(address)
    local byte1 = emu:read8(address)    -- Most significant byte
    local byte2 = emu:read8(address + 1)-- Middle byte
    local byte3 = emu:read8(address + 2)-- Least significant byte
    
    -- Converts single byte BCD to number
    local function bcdToNum(b)
        return math.floor(b / 16) * 10 + (b % 16)
    end
    
    return bcdToNum(byte1) * 10000 + 
           bcdToNum(byte2) * 100 + 
           bcdToNum(byte3)
end

-- Main emulation loop (called every frame)
local function frame_callback()
    next_frame = next_frame - 1

    -- Обновляем состояние ввода
    updateInput()

    if next_frame > 0 then
        return
    end

    next_frame = EMULATOR_FRAME_RATE
    
    -- Accept new client connections
    if not client then
        client = server:accept()
        if client then
            client:settimeout(TIMEOUT)
            console:log("Client connected")
        end
    end
    
    -- Process client commands
    if client then
        local cmd, err = client:receive("*l")
        if cmd then
            console:log("Received command:"..cmd)
            
            local response
            if cmd == "get_state" then
                local state = {}
    
                -- Base player data
                state.hp = emu:read8(PLAYER_HP_ADDR)
                state.map, state.x, state.y = get_player_location()
                
                -- New data
                state.direction = emu:read8(PLAYER_DIRECTION_ADDR)
                state.badges = emu:read8(PLAYER_BADGES_ADDR)
                state.money = read3ByteBCD(PLAYER_MONEY_ADDR)
                
                -- Battle detection
                state.in_battle = emu:read8(IN_BATTLE_FLAG) ~= 0  -- Battle flag
                state.battle_won = emu:read8(BATTLE_WON_FLAG) ~= 0 -- Battle result flag
                state.in_trainer_battle = in_trainer_battle()
                
                -- Party Pokemon data
                local party_count = emu:read8(PARTY_COUNT_ADDR)
                state.party = {}
                
                local addresses = {
                    PARTY_POKEMON_1_ADDR, PARTY_POKEMON_2_ADDR,
                    PARTY_POKEMON_3_ADDR, PARTY_POKEMON_4_ADDR,
                    PARTY_POKEMON_5_ADDR, PARTY_POKEMON_6_ADDR
                }
                
                for i = 1, 6 do
                    local pokemon = {exists = i <= party_count}
                    
                    if pokemon.exists then
                        pokemon = read_pokemon_data(addresses[i])
                        pokemon.exists = true  -- Update existence flag
                    end
                    
                    state.party[i] = pokemon
                end
                
                -- JSON serialization
                local ok, json_str = pcall(json.encode, state, {nulval=""})

                if not ok then
                    console:log("JSON error: "..json_str)
                    response = '{"error":"data_corruption"}\n'
                else
                    response = json_str.."\n"
                end
            elseif BUTTON_VALUES[cmd] then
                -- Вместо непосредственного нажатия кнопки добавляем событие в очередь
                enqueueButtonPress(cmd)
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