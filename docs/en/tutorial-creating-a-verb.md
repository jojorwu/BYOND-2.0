# Tutorial: Creating a Verb

This tutorial builds on the "Creating an Object" guide and shows how to add a `verb` to an object to make it interactive.

## 1. What is a Verb?

In DM, a `verb` is a procedure that can be called by a player on a specific object, atom, or turf. Verbs are the primary way players interact with the game world and typically appear in a right-click context menu.

## 2. Adding a Verb to an Object

Let's add a new verb to the `apple` object we created in the previous tutorial. This verb will allow a player to `inspect` the apple to see how many bites are left.

Open the `scripts/apple.dm` file and add the `inspect()` verb:

```dm
// scripts/apple.dm
/obj/item/apple
    var/bites_left = 3

    verb/eat()
        set src in usr // This verb targets the user (the player).

        if (bites_left > 0)
            bites_left--
            usr << "You take a bite of the apple. [bites_left] bites left."
            if (bites_left == 0)
                usr << "You finish the apple."
                del src
        else
            usr << "There's nothing left to eat!"

    // The 'inspect' verb allows players to check the apple's state.
    verb/inspect()
        set src in usr
        usr << "You inspect the apple. It has [bites_left] bites remaining."
```

The line `set src in usr` is a common DM pattern that sets the verb's context. It specifies that the source of the action (`src`, the apple) must be accessible to the user (`usr`, the player calling the verb). This typically means the player is holding the item or is standing near it.

## 3. Run and Test

Now, you can test your changes. Run the server from the project root:

```bash
./run_server.sh
```

Connect with a client. Now, when you right-click the apple, you will see an "inspect" option in the context menu. Clicking it will display a message with the number of bites left.
