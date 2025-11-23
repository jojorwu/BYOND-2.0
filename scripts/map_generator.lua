-- A simple map generator script

print("Generating map...")

-- Create a 10x10x1 map
Game:CreateMap(10, 10, 1)

-- Fill the map with grass (tile id 1)
for x = 0, 9 do
    for y = 0, 9 do
        Game:SetTile(x, y, 0, 1)
    end
end

-- Create a wall (tile id 2) around the border
for x = 0, 9 do
    Game:SetTile(x, 0, 0, 2)
    Game:SetTile(x, 9, 0, 2)
end
for y = 0, 9 do
    Game:SetTile(0, y, 0, 2)
    Game:SetTile(9, y, 0, 2)
end

-- Save the map to a file
Game:SaveMap("maps/generated_map.json")

print("Map generation complete.")
