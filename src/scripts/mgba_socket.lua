package.path = package.path .. ";D:\\Programming\\PokemonRedRL\\src\\mGBA-0.10.5-win64\\lua\\?.lua"
package.cpath = package.cpath .. ";D:\\Programming\\PokemonRedRL\\src\\mGBA-0.10.5-win64\\lua\\?.dll"

local socket = require("socket")

-- Конфигурация
local HOST = "127.0.0.1"
local PORT = 12345
local TIMEOUT = 0.1 -- Короткий таймаут для неблокирующего режима

-- Адреса памяти
local PLAYER_HP_ADDR = 0xD16B
local PLAYER_X_ADDR = 0xD362
local PLAYER_Y_ADDR = 0xD361
local PLAYER_MAP_ADDR = 0xD35E  -- Текущая карта

-- Constants

local EMULATOR_FRAME_RATE = 60;

-- Коды кнопок
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

-- Инициализация сервера
local server = assert(socket.bind(HOST, PORT))
server:settimeout(TIMEOUT)
console:log("Server started at "..HOST..":"..PORT)

local client = nil
local previous_input = 0
local current_input = 0

local next_frame = EMULATOR_FRAME_RATE
local reset_before_next_frame = 15

-- функция получения текущей локации пользователя

local function get_player_location()
    local x = emu:read8(PLAYER_X_ADDR)
    local y = emu:read8(PLAYER_Y_ADDR)
    local map = emu:read8(PLAYER_MAP_ADDR)
    return map, x, y
end

-- Основной цикл (вызывается каждый кадр)
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

    -- Обработка ввода
    if current_input ~= 0 then
        previous_input = current_input
        emu:addKeys(current_input)
        current_input = 0 -- Сбрасываем ввод после применения
    end
    
    -- Принимаем новые соединения
    if not client then
        client = server:accept()
        if client then
            client:settimeout(TIMEOUT)
            console:log("Client connected")
        end
    end
    
    -- Обработка команд
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

-- Регистрируем callback для каждого кадра
callbacks:add('frame', frame_callback)

console:log("Script initialized - waiting for connections")