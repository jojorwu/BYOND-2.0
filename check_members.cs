using System;
using LiteNetLib;

public class MemberChecker {
    public static void Main() {
        var type = typeof(NetPeer);
        foreach (var prop in type.GetProperties()) {
            Console.WriteLine($"Property: {prop.Name}");
        }
    }
}
