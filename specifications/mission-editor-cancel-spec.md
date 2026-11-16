# Mission Editor: Escape and Right-click Cancellation

## Problem Statement

Mission authors need a mouse gesture to cancel Add Node or Add Connection and clear the current selection without reaching for the keyboard. Escape currently cancels a tool but does not clear selection, while right-click currently starts panning. Authors need consistent cancellation from the keyboard and mouse without losing right-drag panning.

## Solution

Make Escape and a right-click anywhere on the mission canvas perform the same ordered cancellation. If Add Node or Add Connection is active, cancel only that tool and preserve the selected object. Otherwise, clear the selection. When a tool and node are both active, the first cancel disarms the tool and the second clears the node.

A right-click takes effect on release without a drag, whether over empty canvas, a node, or a connection. Right-drag continues to pan and never cancels the tool or selection.

## User Stories

1. As a mission author, I want to right-click to cancel Add Node, so that I can stop placing nodes without using the keyboard.
2. As a mission author, I want to right-click to cancel Add Connection, including a pending connection source, so that I can abandon a connection without creating a route.
3. As a mission author, I want cancelling an active tool to preserve my selected node, so that its properties remain available.
4. As a mission author, I want a subsequent cancel to clear the selection, so that I can return to an unselected canvas.
5. As a mission author, I want Escape and right-click to follow the same cancellation order, so that switching between keyboard and mouse is predictable.
6. As a mission author, I want cancellation to work over nodes and connections as well as empty space, so that I do not need to find a blank area.
7. As a mission author, I want right-drag to keep panning without cancelling my work, so that I can navigate while a tool or selection is active.
8. As a mission author, I want invalid property input to retain its existing discard protection when deselecting, so that cancellation cannot silently lose typed input.

## Implementation Decisions

### Ordered cancellation

Use one cancellation decision in the mission editing session, shared by the Escape and canvas right-click entry points. The session owns tool and selection changes; the view owns keyboard routing and the canvas owns gesture recognition. Keep rule code independent of Unity and add no presentation-feature dependencies.

Each unconsumed cancel performs at most one of these steps, in priority order:

| State before cancel | Result |
| --- | --- |
| Add Node active, with or without selection | Return to Select; preserve selection. |
| Add Connection active, with or without a pending source or selection | Return to Select; clear the pending connection source and preview; preserve selection. |
| Select active and an object selected | Clear selection through the existing selection-change workflow. |
| Select active and nothing selected | No tool or selection change. |

- Selection means the editor's existing single node or connection selection. The same clearing behavior applies to both; this introduces no new selection mode.
- The tool cancellation step must stop after disarming the tool, even when a node or connection remains selected. It must not prompt to discard selected-object input because selection is unchanged.
- The selection step retains existing invalid selected-object input protection. If input is invalid, ask before discarding it. Accepting discards that invalid input and clears selection; declining preserves the input and selection.
- Cancellation must not add, delete, move, or reconnect mission objects, change valid property values, or alter the document's dirty state. Clearing selection removes its highlight and selected-object inspector content through the normal refresh behavior.
- Preserve the existing ability to disarm a tool by clicking its active toolbar button again. This specification changes Escape and adds canvas right-click cancellation; toolbar buttons keep their existing behavior.

### Mouse gesture

- Recognize an ordinary right-button press and release on the mission canvas as one cancel. Dispatch it on release, not on press, so that panning can be distinguished from clicking.
- Use the canvas's existing drag threshold to distinguish incidental pointer movement from a drag. Once the gesture crosses that threshold, it is a pan for the remainder of the gesture, even if the pointer returns to its starting position.
- A right-drag pans the viewport, preserves the tool, pending connection source, and selection, and does not cancel on release. A right-drag over a node must not move that node.
- A right-click over a node or connection cancels according to the current state; it does not select the object under the pointer, place a node, or choose a connection endpoint.
- Middle-button panning, left-button editing and navigation, wheel zoom, and Fit to Map retain their existing behavior.
- Only the mission canvas supplies the new mouse gesture. Right-clicks on toolbar controls, property panels, or dialogs must not propagate into canvas cancellation.
- Pointer capture loss or an interrupted gesture must not synthesize a cancel. Retain the canvas's existing pointer capture and gesture cleanup responsibilities.

### Keyboard, dialogs, and pending input

- Preserve Escape's existing dialog priority: it answers an open dialog with its safe choice, without also cancelling the underlying tool or selection.
- Preserve Escape consumption by an open dropdown. Closing the dropdown must not also cancel a tool or clear selection.
- When the editor is busy or a modal dialog blocks the canvas, a canvas gesture must not mutate tool or selection state behind that dialog.
- Once an Escape press reaches editor cancellation, apply the same ordered operation as right-click. Preserve the existing text-field focus fallback when no tool or selection can be cancelled; deselection still passes through invalid-input protection.
- Keyboard and mouse equivalence concerns editor cancellation. This feature does not make right-click dismiss dialogs or dropdowns like Escape.

## Testing Decisions

Use the existing mission editing session's public workflow as the primary automated seam, following the Edit Mode tests for tool cancellation, selection changes, and invalid-input prompts. Assert author-visible results rather than private gesture flags or rendering hierarchy. Do not introduce a new interface solely for these tests.

Cover the following session behaviors:

- Cancel Add Node with no selection; it disarms without creating a node.
- With a node selected and Add Node active, the first cancel preserves that node and the second clears it.
- Cancel Add Connection before choosing a source and after choosing one. Repeat with a selected node supplying the source. Cancellation clears the pending connection without creating an edge and preserves selection until a later cancel.
- With Select active, cancel clears either a node or connection selection. Repeated cancellation with no selection has no further effect.
- Invalid selected-object input does not prompt during the tool-only step. The deselection step prompts; accepting clears the selection and invalid input, while declining retains both.
- Cancellation leaves mission data and dirty state unchanged, including when the document is already dirty. Busy-state cancellation changes nothing.

Verify presentation behavior in the Mission Editor scene during implementation:

- Run the same sequences with Escape and right-click, including mixed input such as Escape to disarm and right-click to deselect.
- Right-click over empty canvas, a node, and a connection. Each release performs exactly one cancellation and never triggers left-click editing behavior.
- Check tool button highlights, selected-object highlights, inspector content, and pending connection preview after each step.
- Right-drag with either tool active and with a selection present, starting on empty space and over objects. Confirm panning works and release cancels nothing, including after moving away and returning to the starting point.
- Check movement below and above the existing drag threshold, interrupted gestures, and pointer capture cleanup. Confirm a simple right-click does not visibly pan.
- Verify middle-button panning, left-click selection and editing, node dragging, and zoom remain usable.
- With a dialog or dropdown open, verify Escape retains its existing priority and no input also changes underlying canvas state. Check text-field focus and invalid-input confirmation behavior.
- Right-click toolbar and property panels and confirm there is no canvas cancellation.

This PR is documentation only. These are acceptance checks for the future implementation, not claims of completed test or play-mode verification.

## Out of Scope

- Implementing the feature in this specification change.
- A new Cancel or Deselect toolbar button, context menu, or configurable mouse binding.
- Clearing an active tool and selection together with one cancel.
- Changing toolbar toggle behavior, left-click editing, mission data or JSON format, undo/redo, or multi-selection.
- Changing dialog answers, dropdown dismissal, or middle-button panning.

## Further Notes

- This specification extends the original Mission Editor specification: Escape will now clear selection when no tool is active. The original tool-only Escape description is superseded by the ordered behavior here.
- Confirmed decisions: right-click anywhere on the canvas; cancellation on release without dragging; right-drag preserves tool and selection; Escape and right-click cancel tools before selection on separate invocations.
- Assumptions to validate during implementation: the current drag threshold feels appropriate for right-click versus right-drag; runtime UI Toolkit pointer capture and focus routing support the gesture without a second cancellation or unintended field loss. Adjust implementation details within the agreed behavior if practical verification requires it.
- No Unity scenes, generated assets, runtime code, or mission files need to change for this specification PR.
