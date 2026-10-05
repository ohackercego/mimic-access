using System.Reflection;
using MimicPartyAccess;

const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
var handler = typeof(AccessibleMenuHandler);
var screen = new TestScreen();
handler.GetField("_activeScreen", flags)!.SetValue(null, screen);
var refresh = handler.GetMethod("RefreshOptions", flags)!;
var missing = new object[] { Type.Missing };
refresh.Invoke(null, missing);
screen.room.Destroyed = true;
screen.room = new Button(3);
try
{
    refresh.Invoke(null, missing);
}
catch (TargetInvocationException exception)
{
    throw new Exception("Room list refresh accessed a destroyed control.", exception.InnerException);
}
Console.WriteLine("PASS: room list can replace the selected, destroyed button.");

var same = handler.GetMethod("AreSameControl", flags)!;
var destroyed = new Button(5) { Destroyed = true };
if ((bool)same.Invoke(null, new object[] { destroyed, new Button(6) })!)
    throw new Exception("Different native controls compared equal.");
if (!(bool)same.Invoke(null, new object[] { destroyed, new Button(5) })!)
    throw new Exception("Two wrappers for the same native control compared unequal.");
Console.WriteLine("PASS: native identity comparison does not dereference a destroyed gameObject.");

public sealed class TestScreen
{
    public Button room = new(1);
    public Button close = new(2);
}

public sealed class Button
{
    public Button(int identity) => Pointer = new IntPtr(identity);
    public IntPtr Pointer { get; }
    public bool Destroyed { get; set; }
    public object gameObject => Destroyed ? throw new NullReferenceException("Destroyed native component") : new TestGameObject(Pointer.ToInt32());
    public bool interactable => true;
}

public sealed class TestGameObject
{
    public TestGameObject(int identity) => Pointer = new IntPtr(identity);
    public IntPtr Pointer { get; }
    public string name => "Room";
    public bool activeInHierarchy => true;
}

namespace MimicPartyAccess
{
    internal static class ScreenReader
    {
        public static void Say(string text, bool interrupt = true) { }
        public static void Repeat() { }
    }
}
