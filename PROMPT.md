# Winforms Designer App MCP Server Tools

This project is focused on creating a set of tools for a Model Context Protocol (MCP) server that can assist in
designing WinForms applications. The goal is to provide an interface that allows an AI to discover, understand, and
manipulate the components of a WinForms designer canvas effectively.

## Preamble

Building a WinForms Designer App MCP (Model Context Protocol) Server involves bridging the gap between a high-level LLM
and the granular, property-heavy world of desktop UI.

Since MCP tools are essentially callable functions, here are the tools we want to include to make an AI "aware"
of a WinForms canvas:

## Visual Tree & State Inspection

To give the AI context of what already exists on the form:

- *list_controls*: Returns a hierarchical list of all controls on the current form, including their types, names, and
  parent-child relationships.
- *get_control_properties*: Fetches the full property bag (Size, Location, Anchor, Dock, etc.) for a specific control.
- *capture_form_screenshot*: Generates a base64 image of the current designer surface so the AI can "see" alignment
  or overlap issues.

## Layout & Positioning Tools

Since manually calculating X/Y coordinates is hard for LLMs, provide abstracted tools:

- *place_control*: Adds a new control (e.g., Button, DataGridView) with smart defaults for common types.
- *align_controls*: A high-level tool that takes a list of control IDs and an alignment type
  (e.g., Top, Middle, DistributeVertically).
- *apply_docking_and_anchoring*: Sets complex responsive behavior without the AI needing to know the
  exact bitwise enums.

## Event & Logic Integration

- *create_event_handler*: Scaffolds a C# method in the code-behind for a specific event (e.g., Click, TextChanged)
  and links it in the .Designer.cs file.
- *search_component_docs*: A resource-based tool that provides snippets from official Microsoft WinForms
  Documentation or specialized libraries like Telerik UI for WinForms.

## Styling & Theming

- *apply_theme_preset*: Batch-updates colors and fonts across all controls to match a specific style
  (e.g., "Modern Dark" or "System Native").
- *set_control_style*: A tool to modify visual properties like BackColor, FlatStyle, or BackgroundImage.

## Debugging & Validation

- *check_accessibility_compliance*: Scans the form for missing AccessibleName or TabIndex issues and returns a report.
- *validate_layout_constraints*: Identifies overlapping controls or those hidden outside the form's visible bounds.
