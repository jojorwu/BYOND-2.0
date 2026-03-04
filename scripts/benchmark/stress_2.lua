
function OnServerStart()
    for k=1,100 do
        -- Busy loop to simulate load (simulating 100 scripts per file)
        local x = 0
        for j=1,100 do
            x = x + j
            Game:GetTurf(1, 1, 1)
        end
    end
end
