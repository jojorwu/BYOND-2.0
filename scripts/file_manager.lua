-- file_manager.lua
-- This script demonstrates the new file management API.

print("File manager script started.")

-- 1. List all script files.
print("Listing all script files:")
local files = Game:ListScriptFiles()
for i = 0, files.Count - 1 do
    print("- " .. files[i])
end

-- 2. Create a new script file.
local newScriptName = "new_script.lua"
local newScriptContent = "print('This is a new script!')"
print("Creating a new script file: " .. newScriptName)
Game:WriteScriptFile(newScriptName, newScriptContent)

-- 3. Read the content of the new file.
print("Reading the content of the new script file:")
local content = Game:ReadScriptFile(newScriptName)
print(content)

-- 4. Delete the new file.
print("Deleting the new script file.")
Game:DeleteScriptFile(newScriptName)

-- 5. List the files again to show that the new file is gone.
print("Listing all script files again:")
files = Game:ListScriptFiles()
for i = 0, files.Count - 1 do
    print("- " .. files[i])
end

print("File manager script finished.")
