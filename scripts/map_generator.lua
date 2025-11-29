-- A simple map generator script using the new chunk-based system

print("Map generator script started.")

-- Since the map is now infinite, we don't create it.
-- We just place a few walls to have something to see.

local wallType = Game:GetObjectType("/obj/wall")
if wallType == nil then
    print("Error: Could not find object type '/obj/wall'")
else
    print("Creating a small room...")
    -- Top and bottom walls
    for x = -5, 5 do
        Game:CreateObject(wallType, x, -5, 0)
        Game:CreateObject(wallType, x, 5, 0)
    end

    -- Left and right walls (avoiding corners)
    for y = -4, 4 do
        Game:CreateObject(wallType, -5, y, 0)
        Game:CreateObject(wallType, 5, y, 0)
    end
    print("Room creation complete.")
end
