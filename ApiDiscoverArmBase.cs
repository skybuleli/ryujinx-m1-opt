using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;

class Program {
    static void Main() {
        var methods = typeof(ArmBase).GetMethods(BindingFlags.Public | BindingFlags.Static);
        
        Console.WriteLine("--- ArmBase Reverse Methods ---");
        foreach (var m in methods.Where(m => m.Name.Contains("Reverse"))) {
            Console.WriteLine(m.ToString());
        }
    }
}
