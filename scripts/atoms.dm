// The foundational atom type for all game objects.
/atom
    var/opacity = 0 // By default, atoms are transparent

// A simple wall that blocks sight.
/obj/wall
    parent_type = /atom
    opacity = 1
