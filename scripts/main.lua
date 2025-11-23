-- main.lua
-- This script is the entry point for the game logic.

print("Main script started.")

-- 1. Generate the map if it doesn't exist.
-- In a real game, you would likely have a separate tool for map creation.
dofile("scripts/map_generator.lua")

-- 2. Load the map.
print("Loading map from file...")
Game:LoadMap("maps/generated_map.json")
print("Map loaded.")

-- 3. Create game objects.
local player = Game:CreateObject("Player", 5, 5, 0)
print("Created object: " .. player.Name .. " at (" .. player.X .. "," .. player.Y .. "," .. player.Z .. ") with ID " .. player.Id)

-- 4. Demonstrate object manipulation.
print("Moving the player object...")
local retrievedPlayer = Game:GetObject(player.Id)
if retrievedPlayer ~= nil then
    retrievedPlayer:SetPosition(7, 2, 0)
    print("Player moved to: (" .. retrievedPlayer.X .. "," .. retrievedPlayer.Y .. "," .. retrievedPlayer.Z .. ")")
else
    print("Could not find the player object!")
end

print("Main script finished.")
