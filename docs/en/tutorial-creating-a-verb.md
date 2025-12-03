# Tutorial: Creating a Verb

This tutorial builds on the "Creating an Object" guide and shows how to add a `verb` to an object to make it interactive.

## 1. What is a Verb?

In DM, a `verb` is a procedure that can be called by a player, usually through a right-click context menu on an object. Verbs are the primary way players interact with the game world.

## 2. Adding a Verb to an Object

Let's add a new verb to the `apple` object we created in the previous tutorial. This verb will allow a player to `inspect` the apple to see how many bites are left.

Open the `scripts/apple.dm` file and add the `inspect()` verb:

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

    verb/inspect()
        set src in usr
        usr << "You inspect the apple. It has [bites_left] bites remaining."
```

The `set src in usr` line is important. It ensures that the verb can only be called by a player who has the object in their inventory or is within a certain range.

## 3. Run and Test

Run the server and connect with a client. Now, when you right-click the apple, you will see an "inspect" option. Clicking it will display a message with the number of bites left.
