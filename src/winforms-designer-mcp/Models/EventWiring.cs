namespace WinFormsDesignerMcp.Models;

/// <summary>
/// An event wiring found in a designer file (e.g., button1.Click += handler).
/// </summary>
public class EventWiring
{
    /// <summary>
    /// The event name (e.g., "Click", "TextChanged").
    /// </summary>
    public required string EventName { get; set; }

    /// <summary>
    /// The handler method name in the code-behind (e.g., "button1_Click").
    /// </summary>
    public required string HandlerMethodName { get; set; }

    /// <summary>
    /// The delegate type used to wire the event in C# designer files
    /// (e.g., "System.Windows.Forms.KeyPressEventHandler").
    /// When null, defaults to "System.EventHandler" on write.
    /// Not used for VB.NET, which uses AddHandler/AddressOf syntax.
    /// </summary>
    public string? DelegateTypeName { get; set; }
}
