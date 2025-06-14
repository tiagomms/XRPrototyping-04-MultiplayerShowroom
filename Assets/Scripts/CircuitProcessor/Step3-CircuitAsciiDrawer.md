## Step 3: ASCII Diagram Construction

### 🔠 Goal

Transform the grid-based circuit defined in Step 2 into an accurate ASCII diagram. This diagram is a human-readable visual layout of the full circuit using text characters.

📥 **Input:**

- Entire JSON structure from Step 2, particularly:
  - `components[*].gridPosition`
  - `wires[*].fromGrid` and `wires[*].toGrid`

📤 **Output:**
To our input JSON structure, add:
- `ascii`: array of strings, each representing a row of the final ASCII circuit.
- Updated `components[*].asciiPosition` and `wires[*].fromASCII`/`wires[*].toASCII` with calculated values

### 🧮 ASCII Rendering Principles

#### 📏 Layout Parameters
These are set globally and **must not be overridden**:
- `ASCII_CELL_WIDTH = 5` 
- `ASCII_CELL_HEIGHT = 3` 
- `ASCII_HORIZONTAL_PADDING = ASCII_CELL_WIDTH` (1 cellWidth on each side)
- `ASCII_VERTICAL_PADDING = 1` (1 row at top and bottom)

#### 🧱 Symbols Used
| Purpose            | Symbol |
|--------------------|--------|
| Component anchor   | `:`    |
| Horizontal wire    | `-`    |
| Vertical wire      | `|`    |
| Fork or junction   | `+`    |

#### Rules
The following rules are now canon:

1. **ASCII Position Calculation**
   - Calculate ASCII positions for all components and wires at the start:
     ```python
     asciiPosition = [
         gridPosition[0] * ASCII_CELL_WIDTH + ASCII_HORIZONTAL_PADDING,
         gridPosition[1] * ASCII_CELL_HEIGHT + ASCII_VERTICAL_PADDING
     ]
     ```
   - This applies to both component positions and wire endpoints
   - The padding is included in the ASCII positions to ensure consistent spacing

2. **Canvas Dimensions**
   - The ASCII canvas must be initialized with dimensions:
     ```python
     canvas_width = max_ascii_x + ASCII_HORIZONTAL_PADDING
     canvas_height = max_ascii_y + ASCII_VERTICAL_PADDING
     ```
   - Where `max_ascii_x` and `max_ascii_y` are the maximum values from all ASCII positions
   - Note: Only one padding is added to width since positions already include left padding. The same thing as for the height since positions already include top padding.
   - These dimensions must be stored in the output JSON as `asciiSize: [width, height]`

3. **Drawing Order**
   - Wires must be drawn **first**, using calculated ASCII positions
   - Each **component ID** must be exactly **3 characters long** and placed above its anchor
   - Component anchors (`:`) are drawn last and may overwrite wire segments

4. **Violation Detection**
   - Record violations when component IDs overlay existing wire segments
   - Do not record violations when component anchors overwrite wire segments

#### 🔁 Wires

- Wires should be rendered as part of the ASCII structure.
- For each wire, both the calculated `fromASCII` and `toASCII` positions are used.
- Wires are **not interpolated** — their drawing is derived **directly from these fixed ASCII positions**.

##### 📊 Wire Drawing Order and Precedence

1. **Horizontal Wires (First Priority)**
   - Drawn first and take precedence over all other elements
   - Once a position is filled by a horizontal wire, it cannot be overwritten by subsequent wires
   - Component anchors (`:`) are drawn as part of horizontal wires when `startTouchesComponent` or `endTouchesComponent` is true
   - No special treatment for rightmost components - all component anchors are drawn consistently

2. **Vertical Wires (Second Priority)**
   - Drawn after all horizontal wires
   - Skip the first character of the vertical segment (start_y + 1)
   - Only draw in empty spaces
   - Do not overwrite existing horizontal wire segments

3. **Component IDs (Final Layer)**
   - Drawn last, after all wires are in place
   - Overlay component IDs above their anchor positions
   - May modify existing wire segments, but if that is the case a note should be written in the JSON document in the `violation`.

##### ⬇ Vertical Wire Rules
- Vertical wires start drawing from the second character position (start_y + 1)
- They appear as `|` in each vertical ASCII cell row
- They only draw in empty spaces
- They do not overwrite existing horizontal wire segments

##### 🪄 Horizontal Wire Rules

They must respect additional metadata fields from the `wires` array produced in Step 2. These fields define how endpoints are rendered using ASCII characters. The logic is as follows:

###### 🔡 Wire Symbol Encoding Rules

Each horizontal wire is composed of one or more cells, where each cell spans `ASCII_CELL_WIDTH` characters. The total width of a wire is determined by its calculated `fromASCII` and `toASCII` positions, and may span multiple cells.

For each cell in the wire, the layout is:

***LEFT_SYMBOL + CENTER_FILLERS + RIGHT_SYMBOL***

Where:
- `LEFT_SYMBOL` is determined by:
  - For the first cell only:
    - `isPartOfMerge = true` → `+`
    - Else → `-`
  - For subsequent cells: Always `-`
- `RIGHT_SYMBOL` is determined by:
  - For the last cell only:
    - `isPartOfFork = true` → `+`
    - Else → `-`
  - For other cells: Always `-`
- `CENTER_FILLERS` are always `cellWidth - 2` dashes: `'-' * (ASCII_CELL_WIDTH - 2)`

###### 📏 Wire Width Calculation

The total width of a horizontal wire is determined by its calculated `fromASCII` and `toASCII` positions:
1. Calculate the total width in ASCII units: `end_x - start_x`
2. Calculate the number of cells needed: `(total_width + ASCII_CELL_WIDTH - 1) // ASCII_CELL_WIDTH`
3. Each cell follows the wire symbol encoding rules above
4. The wire is drawn cell by cell, maintaining consistent spacing

Example - Wire spanning multiple cells:
```
   :----:----:    (3 cells, component anchors at start and end)
```

###### 🎯 Component Anchor Rules

- Component anchors (`:`) are drawn during the component phase, not the wire phase
- Each component's anchor position is marked with `:` regardless of what was drawn there by wires
- This ensures consistent component representation and avoids duplicate `:` characters
- The component ID is placed above the anchor position, center-aligned on the `:`
- Overwriting wire segments with component anchors (`:`) is not considered a violation

###### ⚠️ Violation Rules

Violations are only recorded when:
- Component IDs overlay existing wire segments
- Any other unintended modifications to the ASCII canvas

Violations are NOT recorded when:
- Component anchors (`:`) overwrite wire segments (this is expected behavior)
- Wires are drawn in their designated positions

##### 🔁 Symmetry Enforcement

To ensure alignment for parallel branches:

- All vertical segments must **start and end** at the same asciiY level across all forks.
- All horizontal branches must maintain **equal wire segment widths**, even if it requires appending extra `-`.

### 📐 Examples 

#### Example 1 (Series Circuit) Json output
ascii: [
"                   ",
"   V01  R01  L01   ",
"    :----:----:    ",
"                   "
]

This represents a series circuit with 3 components, each connected using 5-character wires, with visible ASCII structure and padding.

#### Example 2 (Parallel Circuit) Json output
ascii: [
"                  R15  R22             ",
"              -----:----:----          ",
"              |             |          ",
"   V02  R12   |             |   L02    ",
"    :----:---+              +----:     ",
"              |             |          ",
"              |   R04       |          ",
"              -----:---------          ",
]

V02 → R12 → fork → [R15 -> R22 || R04] → merge → L02 → return

### 🔒 Determinism Enforcement

Given the same input JSON (Step 2), the output ASCII diagram (Step 3) **must always be identical**. Any nondeterministic behavior or deviation violates this protocol.

-----------------------------------------------------------------------------------------------------------
