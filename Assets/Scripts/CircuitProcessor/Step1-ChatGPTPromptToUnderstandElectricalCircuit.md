# XR Circuit Digitizer Prompt - Canonical Document

---

## 🎯 Goal

Given a clear photo of a hand-drawn electrical circuit (captured with Meta Quest camera), analyze the circuit's logic and return a complete JSON output. This includes:

- All detected components with IDs and values
- Conditional branches via switches
- A symbolic Ohm's Law formula using cond(SX, (...)) where needed
- A verbal plan for human inspection
- Any fallback or error notes for transparency

This process is atomic and **must be completed in one pass**, with **no interruptions**.

-----------------------------------------------------------------------------------------------------------

## 📷 Image Input & Structural Logic Extraction

This prompt focuses on understanding **what the circuit does**, not how it looks.

⚡️ **Electric Circuit Logic Expert Mode**  
All parsing, formula construction, and error handling must reflect correct DC electrical circuit theory. This includes Ohm’s law, series/parallel behavior, conditional paths via switches, and proper exclusion of unaffected components. **No formula or logic may violate the laws of electrical connectivity.** This mode is active by default in this prompt.

### 🧾  I/O Structure

**Input:**
- Image of a hand-drawn circuit.

**Output (in JSON Format):**
- `components` array
- `conditionalBranches` array
- `formula` (symbolic)
- `verbalPlan`
- `notes`

### 📸 Image Assumptions

- Very clear white-paper image
- Single, simple circuit diagram
- No overlapping components
- No noise, no complex backgrounds
- Circuit layout should resemble how a student would draw it by hand

#### 🧭 Visual Tip  
To represent a **closed switch**, draw a **line that clearly bridges two open wire ends**. This line:
- Must be **long enough** to visibly span the gap  
- Should ideally have a **slight diagonal or horizontal orientation**  
- Must **not be mistaken for a wire segment** (avoid drawing too close to the wire axis)  
- Drawing **two black dots** at the ends of the switch (where it connects) can help clearly anchor it  
- A clearly drawn bridging line between open wire ends helps distinguish the switch from a normal wire segment

✅ This improves visibility  
❌ Ambiguous strokes that resemble wire bends may be ignored

⚠️ Mandatory: If a diagonal or slanted line clearly bridges two open wire ends, and is anchored with dots or visibly spans a wire break, it must be interpreted as a **switch** component. **Unless**, if the interpreted diagonal line lies within a known battery structure, it must not be classified as a switch. 
 - Vertical pairs of long and short lines must be interpreted only as a battery, regardless of orientation or position.
 - Do not create a switch if the surrounding structure matches battery geometry.

The system must create a `switch` in `components`, and associate it with the conditional path it controls.
Missing a switch in such cases is considered a parsing failure.


### 🧩 Component Identification

Detect the following elements:

- 🔋 Battery  
- 🟫 Resistors  
- 💡 Lightbulbs  
- 🔘 Switches  
- 📏 Axis-aligned wires (horizontal/vertical only)

#### 🧩 Component Recognition

Use the following rules:

- Resistor: zigzag or rectangular block  
- Lightbulb: circle with filament or cross  
- Switch: diagonal line between broken wire ends  
- Battery: long-short vertical pair  
- Wire: clean horizontal or vertical line

❗ You must treat every symbolic component (zigzag, circle, etc.) as distinct unless clearly connected as part of the same symbol.

⚠️ Resistor Stacking Rule
If two or more resistor-like shapes (zigzags or rectangles) appear aligned on the same wire path, and there is no visible perpendicular line between them, you must:
- Treat them as distinct resistors
- Assign each a default or separate ID and value
- Do not merge them unless clearly part of a labeled block (e.g., R10, 2M)

This applies even if they are stacked vertically or horizontally or tightly spaced — visual closeness does not imply symbolic unity.

---

### 🆔 ID Naming Convention

| Type         | Prefix | Example |
|--------------|--------|---------|
| Battery      | `V`    | `V01`    |
| Resistor     | `R`    | `R02`    |
| Lightbulb    | `L`    | `L01`    |
| Switch       | `S`    | `S01`    |

IDs are always a capital letter followed by a **2-digit number padded with a leading zero if needed** (e.g., `R01`, `R02`, `S05`, `V10`). IDs must always contain exactly 3 characters.

If a handwritten component is labeled `R1`, it must be parsed as `R01` from that moment on in the JSON, and this padded format must be preserved in all steps.
 
Once a canonical ID is generated, it must never be altered again. All future references (in grid layout, ASCII, pixel logic, formulas, etc.) must use the padded 3-character version.

### 📛 Label-Value Fidelity Rule

If a component has both a visible ID and value (e.g. `R5`, `2M`):
- the value must be preserved exactly as in written.
- the ID must be normalized to follow the **ID Naming Convention** if it is not doing so already.

After this step is completed, do not reassign, reorder, or remap IDs or values at any later step.
This ensures 1-to-1 traceability from drawing → parsed output → formula logic, and allows accurate reconstruction from the JSON alone.

### 🔧 Defaults

- Resistor: 10.0
- Battery: 5.0
- Lightbulb: 0.0
- Switch: 0 or 1 (depending if switch is off or on respectively)

---

### 🧊 Deterministic Output

Same image = same output. No creativity or variation.

---

### 🔍 Structural Topology Analysis

1. Identify all components (with IDs).
2. Trace all valid paths from the battery’s positive terminal to negative return, accounting for forks, branches, and conditionals.
3. Classify series and parallel segments.
4. Evaluate switch states visually:
   - Assign `value: 0` for open switches  
   - Assign `value: 1` for closed switches  
5. Determine bulb placement (series, inside branch, after branch).
6. Assign IDs and/or values based on **Label-Value Fidelity Rule**, and if none exist, assign IDs based on **ID Naming Convention** and/or values based on **Defaults**. Do not reassign, reorder, or remap IDs or values during any later step.

🔍 If two or more distinct symbolic structures (e.g., resistor + lightbulb) appear on the same wire segment, they must be treated as separate components, even if close together.

#### 🧮 Topology Trace Rule (Series vs Parallel Detection)

To correctly classify connections as series or parallel, the system must perform a full logical trace of the circuit path from the battery's positive terminal to its return. This must not rely on visual alignment or spatial layout.

❗ Do not assume components are in parallel based on vertical or horizontal placement alone.

✅ You must walk the actual path segment-by-segment using electrical connectivity, not visual grouping.

- If all components lie on the same continuous path without forks, they must be classified as series.
- Only when a node has **multiple outgoing connections** to distinct component paths should a parallel configuration be evaluated.

This rule supersedes any inference based on drawing shape, line orientation, or diagram symmetry.

Parsing failure will occur if:
- A linear (series) path is incorrectly wrapped in a reciprocal (parallel) expression.
- Two components are grouped as a branch when only one route for current exists.

#### 💡 Lightbulb Ordering Rule
Lightbulbs (`L#`) must be placed **last** in both the `verbalPlan` and `formula`, as long as this does not break electrical correctness. This ordering is preferred and must be enforced unless the circuit would break.

If the lightbulb is not the last physical component in the traced loop, it should still be moved to the end of the `verbalPlan` and `formula` **for readability** — as long as series/parallel grouping is not violated. This ensures consistent symbolic representation across examples and avoids confusion in visual-digital alignment.

Examples:
- ✅ Good: `V01 -> R01 + S01 || R02 -> L01`
- ❌ Avoid: `V01 -> L01 -> R01 + S01 || R02` unless structure requires it

#### 🔙 Return Phrase Simplification
In `verbalPlan`, **do not include** the string `-> return`. The return path is always implied unless omitting it would cause ambiguity. This avoids redundancy and keeps the verbal plan clean.

Examples:
- ✅ Good: `V01 -> R01 -> L01`
- ❌ Avoid: `V01 -> R01 -> L01 -> return`

These rules must be applied universally across all output examples, formula string construction, and verbal plans.

#### Verbal Plan Example:
`V01 -> R05 -> [ R01 + S01 || R02 ] -> L01`  
`S01 is open -> R01 path is conditional`  
`R02 is always active`

`Values`:
- V01: 5 (default)
- R05: 2M (retrieved from image)
- R01: 10 (default)
- S01: 0 (open based on image)
- R02: 1M (retrieved from image)
- L01: 0 (default)

{
  "components": [
    { "id": "V01", "type": "battery", "value": 5 },
    { "id": "R01", "type": "resistor", "value": 10 },
    { "id": "S01", "type": "switch", "value": 0 },
    { "id": "R02", "type": "resistor", "value": 20 },
    { "id": "L01", "type": "lightbulb", "value": 0 }
  ],
  "conditionalBranches": [
    {
      "switchId": "S01",
      "affects": ["R02"],
      "formulaFragment": "(R02)",
      "parallelGroup": ["R02", "R03"],
      "alwaysIncluded": ["R03"]
    }
  ],
  "formula": "V01 / (R05 + (1 / (S01 * (1 / R01) + (1 / R02))) + L01)"
  "verbalPlan": "V01 -> R05 -> [ R01 + S01 || R02 ] -> L01"
  "notes": "S01 is open -> R01 path is conditional. R02 is always active"
}

---

### 🔁 Conditional Logic & Switch Behavior

Every switch in the drawing must be interpreted and represented in three places:

1. The `components` array (with `value: 0` or `1`)
2. The `conditionalBranches` array
3. The `formula`, referencing its `switchId`

---

#### 🔒 Switch Detection Rules

- Switches are always parsed based on **visual input**
- All visible switch-like structures (open or closed) must be interpreted as switches
- If a switch clearly spans a wire break, it must become a `switch` component
- Every such component must also affect circuit logic and appear in `conditionalBranches`
- A parsing failure occurs if any visible switch is ignored

---

#### 🧠 Conditional Path Logic

- Switches imply conditional logic — regardless of `value`
- `"value"` controls **evaluation**, not inclusion in structure
- Always include the affected components in the `formulaFragment`, even if `value = 1`
- Use `S01 * (R02 + L01)` to wrap affected subgraphs

Switch-controlled paths must appear in all 3 locations:
- `components` entry with `type: switch` and `value: 0` or `1`
- `conditionalBranches` entry with:
  - `switchId`
  - `affects` - array of components disconnected by that switch when `"value": 0`
  - `formulaFragment` - if switch was closed `"value": 1`, `formulaFragment` would represent the Ohm's Law formula of all components affected by the switch within the entire electrical circuit
- `formula` referencing the `conditionalBranches` block, as stated in the summary table below.

##### ✅ Summary Table

| Switch Placement | Formula Behavior             |
|------------------|------------------------------|
| Series           | `S01 * (entire loop)`    |
| Parallel         | `S01 * (branch only)`    |

Always trace return paths to the generator.  
If no alternate path exists, the switch disables the entire circuit and must wrap all downstream elements.

---

#### 📐 Metadata for Parallel Branches

Example JSON structure:

  {
    "switchId": "S01",
    "affects": ["R02"],
    "formulaFragment": "(R02)",
    "parallelGroup": ["R02", "R03", "R04"],
    "alwaysIncluded": ["R04"]
  }

Optional fields (for parallel readability only):

- `parallelGroup`: all resistors/lightbulbs in same fork
- `alwaysIncluded`: resistors in the same group unaffected by any switches

Use only if:
- Group has at least one conditional and one always-included member
- Metadata helps human parsers understand logic without re-tracing diagram
Every switch in the drawing must be interpreted and represented in three places:

1. The `components` array (with `value: 0` or `1`)  
2. The `conditionalBranches` array  
3. The `formula`, referencing its `switchId`

### 🧮 Formula Construction

The final `formula` field must represent total current using symbolic Ohm’s Law:

I = V / R_eq  
Or structured as:  
I = V01 / (R01 + R02)

#### Series Connections

Components in series are added linearly:  
I = V01 / (R01 + R02 + L01)

#### Parallel Connections

Parallel components must use reciprocal structure:  
I = V01 / (R03 + 1 / (1/R01 + 1/R02))

All components in the same parallel fork must be listed inside the same reciprocal block.

#### Switch-Controlled Components (Multiplicative Form)

Switches control conditional branches using multiplicative behavior.  
Every switch has a `value` of either `0` (open) or `1` (closed), and this is used **as a multiplier** in formulas.

Instead of wrapping components in `cond(...)`, use the format:  
S01 * (R02 + L01)

This represents:  
- If `S01` is closed (value = 1), the entire expression `(R02 + L01)` is included.  
- If `S01` is open (value = 0), the whole expression evaluates to 0 (i.e. excluded from the circuit).

This format applies both inside series and reciprocal expressions.

Correct:
I = V01 / (R01 + S01 * (R02))

Incorrect:
I = V01 / (R01 + cond(S01, (R02)))

⚠️ This form must always begin with the switch ID, followed by the affected expression in parentheses. Do not move the switch multiplier to the middle or end of an expression. Also, open switches (value = 0) must always be included in the formula. The formula is symbolic, not value based.

#### 💡 Lightbulb Behavior in Formulas

Lightbulbs (`L#`) must always be treated as resistive components.  
- Follow same rules as `R#`
- Use default value `0.0` unless labeled
- If controlled by a switch, appear **inside** the switch’s multiplier expression

#### 📐 Formula Grammar & Expression Rules

- Use symbolic expressions only — never numerical substitutions at this stage.
- All formulas must conform to the pattern:

  I = V01 / ( ... )

- Use `Sx * (...)` for any branch affected by a switch.
  - Example: `V01 / (S01 * (R01 + R02 + L01))`
  - This represents: if S01 = 1, R01, R02 and L01 are included; otherwise, the whole term evaluates to 0.
- Do not use `cond(...)` expressions.
- Do not nest switch multipliers.
- Always keep the switch ID on the **left side** of the multiplication.

All components in the same parallel fork must be listed inside the same reciprocal block, and switch-controlled segments inside those forks must also use the `Sx * (...)` form.

> 📎 Optional: In future versions, formulas may be converted to pseudocode or structured JSON tables for executable use. This document assumes symbolic math but is compatible with structured transformations.

---

### 📤 JSON Output

Must produce:

- A **verbal plan** (`verbalPlan`) string describing the circuit logic in human-readable sequence.
- A `components` array with objects:
  - `id`: unique component ID (preserve from drawing if visible)
  - `type`: `"battery"`, `"resistor"`, `"lightbulb"`, or `"switch"`
  - `value`: numeric (float or int), no units
- A `conditionalBranches` array (one per switch), with:
  - `switchId`: ID of the controlling switch
  - `affects`: array of component IDs this switch disconnects when `"value": 0`
  - `formulaFragment`: symbolic formula string representing affected sub-path
  - `parallelGroup` *(optional)*: other resistors/lightbulbs in the same fork
  - `alwaysIncluded` *(optional)*: resistors always active in that group
- A symbolic `formula` string for total resistance, using `cond(S01, (...))` for conditionals.
- A `notes` field *(optional)* with plain explanation for human review or debugging.

⚠️ Do **not** include:
- `position`, `gridPosition`, or `wire` entries
- Any layout or drawing information


#### 🔡 ASCII Safety Compliance Note

All string-based fields such as `verbalPlan`, formula strings, or circuit annotations **must avoid** non-standard ASCII characters.

- Unicode arrows must be converted to standard ASCII: use `->` in place of unicode arrows like `→`.
- Curly quotes or smart symbols are not permitted.
- This rule must be enforced in all mode outputs.

This ensures compatibility with text parsers, visualization engines, and markdown processors.

---

#### ✅ Canonical Example Output

{
  "components": [
    { "id": "V01", "type": "battery", "value": 5 },
    { "id": "R01", "type": "resistor", "value": 10 },
    { "id": "S01", "type": "switch", "value": 0 },
    { "id": "R02", "type": "resistor", "value": 20 },
    { "id": "L01", "type": "lightbulb", "value": 0 }
  ],
  "conditionalBranches": [
    {
      "switchId": "S01",
      "affects": ["R02"],
      "formulaFragment": "(R02)",
      "parallelGroup": ["R02", "R03"],
      "alwaysIncluded": ["R03"]
    }
  ],
  "formula": "V01 / (R01 + S01 * (R02) + L01)",
  "verbalPlan": "V01 -> R01 + S01 || R02 -> L01",
  "notes": "S01 is open -> R02 path is disabled. R01 and L01 are in series."
}
---

### 🛑 Bias Reset & Second Scan

Before analyzing each new image:

- Reset memory and discard prior assumptions.
- Perform a **second scan** of all forks and diagonals.
- Detect possible missed switches or junctions.

> A missed switch in a fork or bridge is a **fatal error** — stop immediately.

---

### ❌ Error Handling

If circuit structure is invalid, return only:

**JSON**
{ "error": "Reason" }

Valid Reasons:
- "Short circuit: no resistance in loop"
- "Circuit not closed: return path to battery missing"
- "Unsupported: more than one battery"
- "Open switch disables all return paths"
- "Ambiguous: shared node affects multiple paths"

**Do not continue to the next steps in case of error**.

### ✅ For the user only - Best prompt template to proceed with test

MODE: ONE-PASS TEST EXECUTION
DOCUMENT: XR Circuit Digitizer Prompt - Canonical Document

Read the document titled above.

Then follow these exact instructions:

- Analyze the attached image using the document as the sole source of truth.
- Complete the full parsing and logic extraction process in a single pass.
- Do not pause, do not ask clarifying questions, do not summarize or list intentions.
- The output must be:
  - One complete JSON object
  - Following the rules, logic, and format from the document
  - Deterministic and reproducible
  - No internal execution fields should ever be included.
- You must respect the defaults, ID naming, conditional logic, and formula structure as specified.
- If errors occur, return the appropriate error JSON as defined.

Now process the image using full one-pass mode and return the output JSON only.
