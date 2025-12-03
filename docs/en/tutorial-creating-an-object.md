# Tutorial: Creating an Object

This tutorial will guide you through the process of creating a new game object type using DM script and then spawning an instance of it.

## 1. Define the Object Type

First, we need to define a new object type. Let's create a simple "Apple" object that can be eaten. Create a new file named `scripts/apple.dm` and add the following code:

```dm
// scripts/apple.dm
/obj/item/apple
    var/bites_left = 3

    verb/eat()
        set src in usr

        if (bites_left > 0)
            bites_left--
            usr << "You take a bite of the apple. [bites_left] bites left."
            if (bites_left == 0)
                usr << "You finish the apple."
                del src
        else
            usr << "There's nothing left to eat!"
```

This code defines a new object type `/obj/item/apple` with a variable `bites_left` and a verb `eat()`.

## 2. Spawn the Object

Now, let's spawn an instance of our new apple object in the game world. We can do this in our main DM entry point. If you don't have a `main.dm` file, create one in the `scripts` directory.

```dm
// scripts/main.dm
/world/New()
    // Spawn an apple at coordinates (5, 5)
    new /obj/item/apple(locate(5, 5, 1))
```

## 3. Run the Server

Now, run the server:

```bash
dotnet run --project Server/Server.csproj
```

The server will compile your DM scripts and spawn an apple in the game world. You can then connect with a client and interact with the apple by using the "eat" verb.
