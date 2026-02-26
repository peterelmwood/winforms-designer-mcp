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
}
