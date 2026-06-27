#r "F:\games\Stardew Valley\xTile.dll"
using System.Reflection;
var t = typeof(xTile.Display.IDisplayDevice);
foreach (var m in t.GetMethods())
{
    var parms = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
    Console.WriteLine($"{m.ReturnType.Name} {m.Name}({parms})");
}
