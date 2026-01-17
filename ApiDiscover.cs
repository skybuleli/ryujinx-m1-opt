using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;

class Program {
    static void Main() {
        var methods = typeof(AdvSimd).GetMethods(BindingFlags.Public | BindingFlags.Static);
        
        Console.WriteLine("--- Reverse Methods ---");
        foreach (var m in methods.Where(m => m.Name.Contains("Reverse"))) {
            Console.WriteLine(m.ToString());
        }

        Console.WriteLine("\n--- Table Methods ---");
        foreach (var m in methods.Where(m => m.Name.Contains("Table"))) {
            Console.WriteLine(m.ToString());
        }
    }
}
