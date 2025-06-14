## 🟦 Step 2: Grid Layout Assignment and Wire Definition

This step receives the JSON with the structural circuit plan and returns a fully defined logical grid.

### 🧾 Step 2 I/O Structure

**Input:**
- Full Step 1 output JSON (`components`, `formula`, etc.)

**Output:**
- To our input JSON structure, add:
  - to all `components`:
    - `gridPosition`
    - `asciiPosition` (placeholder, calculated in Step 3)
    - `rectPosition` placeholder
  - create `fork` and `merge` as components with assigned `gridPosition`, and placeholders `asciiPosition` and `rectPosition`
  - `wires` array: with routing, metadata, and from/to positions

### ✅ Component Grid Mapping

Each component must now receive a `gridPosition` field in the format:

***json***
"gridPosition": [x, y]

This reflects the logical layout of the circuit, not the visual position.

* Series paths should increment along the X-axis
* Parallel branches should vary along the Y-axis
* IDs must not be changed from Step 1

⚠️ Every time a parallel branch begins or ends, apply a +2 increment to the X-axis in gridPosition to allow wire routing space. This must be reflected in both the components and the wires.

Each component **must** also include:

* `asciiPosition`: placeholder with default value `[0, 0]`. Actual values are calculated in Step 3.
* `rectPosition`: calculated later (in Step 4), but placeholder must be present with default value of `[0, 0]` in this step. Actual values are calculated in Step 4.

> Step 3 will compute an `asciiPosition` per component, based strictly on each element's `gridPosition`. This mapping is deterministic.
> Step 4 will compute a `rectPosition` per component, based strictly on each element's `asciiPosition`. This mapping is deterministic.

---

### 🧭 Handling Parallel Branches (Updated Logic)

Parallel branches occur when the `verbalPlan` contains rectangular brackets `[ ]` and within them the parallel operator `||`. To handle them, we insert structural markers (`fork`, `merge`) and compute their spatial layout accordingly.

---

#### 🔁 Fork Node Creation

When we detect a parallel branch:

1. A `fork` node is inserted **before** the first component inside the brackets.
2. The fork is placed at:
   - `gridX = previousComponent.gridX + 1`
   - `gridY = previousComponent.gridY`
3. Forks are assigned IDs like `"F01"`, `"F02"`, etc.
4. Forks must be added to the `components` list with:
   - `id`: unique, with prefix `F`
   - `type`: `"fork"`
   - `gridPosition`: as computed
   - `asciiPosition`: `[0, 0]`
   - `rectPosition`: `[0, 0]`

---

#### 🔀 Splitting Branches and Calculating maxBranchLength

After inserting a fork:

1. Split the inside of the brackets using the parallel operator `||`.
2. `Branches = (number of || within the rectangular brackets) + 1`
3. For each branch, count the number of `+` operators:
   - Each `+` represents a component in series.
   - `maxBranchLength = max(number of + in branch) + 1`
   - This length defines how wide each branch extends horizontally.

---

#### 🧭 Grid Assignment for Branches

Once maxBranchLength is known:

1. Distribute the branches vertically:
   - If 2 branches: `y = forkY - 1`, `forkY + 1`
   - If 3 branches: `y = forkY - 1`, `forkY`, `forkY + 1`
   - And so on, symmetric around forkY.

2. For each branch:
   - Place first component at:
     - `gridX = forkX + 1`
     - `gridY = assigned offset`
   - Each subsequent component in series increases `gridX += 1`

---

#### 🔁 Merge Node Creation

After processing all branches:

1. Insert a `merge` node at:
   - `gridX = forkX + maxBranchLength + 1`
   - `gridY = forkY`
2. Merge IDs follow the pattern `"M01"`, `"M02"`, etc.
3. Merges must be added to the `components` list with:
   - `id`: unique, with prefix `M`
   - `type`: `"merge"`
   - `gridPosition`: as computed
   - `asciiPosition`: `[0, 0]`
   - `rectPosition`: `[0, 0]`

---

#### 📌 Final Notes

- Forks and merges must always be created **before** routing wires.
- Wires must not be routed until all components (including forks and merges) are assigned valid `gridPosition`.
- This layout ensures symmetric, orthogonal structures and correct alignment of all branches.

---

### 🔌 Wire Path Definition

> All wires must route using strictly horizontal and vertical paths. Diagonal connections are prohibited.

If a wire needs to make an L-shaped path, it must be split into **two orthogonal wires** (horizontal + vertical). These can optionally meet at an **implicit junction**.

Each wire must connect components using their `gridPosition`, and also include:
- `fromGrid` and `toGrid`
- `fromASCII` and `toASCII` (placeholders with default value `[0, 0]`, calculated in Step 3)
- `fromRect` and `toRect` (placeholders at this stage)

> Step 3 will compute `fromASCII` and `toASCII` per wire, based strictly on each element's `fromGrid` and `toGrid` respectively. This mapping is deterministic.
> Step 4 will compute a `fromRect` and `toRect` per wire, based strictly on each element's `fromASCII` and `toASCII` respectively. This mapping is deterministic.

* Wires must connect only between defined `gridPosition` components
* Diagonal paths must be broken into at least three orthogonal wires (horizontal + vertical + horizontal)
* Wire list is mandatory for circuit to be rendered in future steps

#### ➕ Wire Metadata Fields (Horizontal Wires)

During Step 2, each wire object in the JSON must be extended with the following additional fields:

- `isHorizontal`: boolean
  - `true` if the wire only spans horizontally (i.e. only the X axis changes between `fromGrid` and `toGrid`)
  - `false` otherwise

If the wire is horizontal (`isHorizontal: true`), then you must also compute:

- `startTouchesComponent`: boolean
  - `true` if any component's `gridPosition` exactly matches the wire's `fromGrid`
  - `false` otherwise

- `endTouchesComponent`: boolean
  - `true` if any component's `gridPosition` exactly matches the wire's `toGrid`
  - `false` otherwise

- `isPartOfFork`: boolean
  - `true` **if its `toGrid` value matches the `gridPosition` of any component of type `"fork"`**.
  - `false` otherwise

- `isPartOfMerge`: boolean
  - `true` **if its `fromGrid` value matches the `gridPosition` of any component of type `"merge"`**.
  - `false` otherwise

This ensures wires are aware of when they participate in a structural branching or convergence point and allows rendering systems to visually adapt.

> ⚠️ These checks must be performed **only after forks and merges have been appended to the `components` list** and all positions finalized.  
> Do not attempt to infer fork/merge status based solely on wire topology — rely on the component definitions instead.

All these fields must be placed **alongside each wire object** in the `wires` array inside the Step 2 JSON output. They must not be stored in separate structures.

##### 🧷 Mandatory Metadata Fields for Horizontal Wires
For every wire where `isHorizontal` is `true`, the following fields are required and must always be included in the JSON output:

- `isHorizontal`: must be explicitly set to `true`
- `startTouchesComponent`: whether the wire's starting point touches a component gridPosition
- `endTouchesComponent`: whether the wire's ending point touches a component gridPosition
- `isPartOfFork`: whether this wire originates from a branching point
- `isPartOfMerge`: whether this wire ends at a merging point

Even if these values are all `false`, they must still be declared. Omitting them for horizontal wires is a protocol violation.

##### 🎯 Contact Detection Clarification
If a wire starts or ends **at the same gridPosition as any component**, the corresponding contact flags must be set to `true`.

For example:
- If a wire's `toGrid` is `[2, 0]` and there is a resistor with `gridPosition: [2, 0]`, then `endTouchesComponent: true`
- If a wire's `fromGrid` is `[2, 0]` and there is a resistor with `gridPosition: [2, 0]`, then `startTouchesComponent: true`

All wires must follow this logic regardless of merge/fork presence.

##### 🔄 Wire Processing Rules

1. **Wire ID Generation**:
   - Each wire must have a unique ID in the format "W01", "W02", etc.
   - IDs must be assigned sequentially based on wire creation order

2. **Wire Deduplication**:
   - Duplicate wires (same fromGrid and toGrid) must be removed
   - Only the first occurrence of a wire should be kept

3. **Wire Distance Validation**:
   - The Manhattan distance between fromGrid and toGrid must be >= 1
   - Wires with distance < 1 must be discarded

4. **Y-axis Normalization**:
   - After all components are placed, y positions must be normalized
   - The minimum y value must be adjusted to 0
   - All component and wire positions must be adjusted accordingly

***json***
{
  "fromGrid": [x1, y1],
  "toGrid": [x2, y2],
  "fromASCII": [00 b1],  // derived in Step 3
  "toASCII": [0 0],    // derived in Step 3
  "fromRect": [px1, py1], // derived in Step 4
  "toRect": [px2, py2]    // derived in Step 4
  "isHorizontal": true/false,
  "startTouchesComponent": true/false,
  "endTouchesComponent": true/false,
  "isPartOfFork": true/false,
  "isPartOfMerge": true/false,
}

### 🛠️ Wire Generation Order Requirement

- Wire definitions must occur **strictly after** all grid positions for components (including `fork` and `merge`) have been calculated and committed.
- Do **not** assign `isPartOfFork` or `isPartOfMerge` during a preliminary pass. Always calculate them once component structure is stable.

--- 

This ensures that fork/merge wiring metadata remains deterministic, readable, and can be traced back from `wires` to their corresponding `components`.

### 🔄 Positional Field Notes (Clarification)

- `rectPosition`, `fromRect`, and `toRect` fields are **placeholders only** in Step 2.  
  These values will be computed in **Step 4** after pixel coordinate extraction and scaling.

- `asciiPosition` is a placeholder in Step 2 with default value `[0, 0]`.

- `fromASCII` and `toASCII` are placeholders in Step 2 with default value `[0, 0]`.

These calculated fields ensure consistency in ASCII rendering, and avoid reliance on raw pixel values too early in the pipeline.

--- 

### ✅ Canonical Examples Reference

#### ⚡ Example 1: Basic Series Circuit

##### ASCII Layout

   V01  R01  L01
    :----:----:


Grid positions:

* V01: [0,0]
* R01: [1,0]
* L01: [2,0]

##### Example JSON Output (Step 2)

{
"components": [
{ "id": "V01", "type": "battery", "gridPosition": [0,0], "asciiPosition": [0,0], "rectPosition": [0,0] },
{ "id": "R01", "type": "resistor", "gridPosition": [1,0], "asciiPosition": [0,0], "rectPosition": [0,0] },
{ "id": "L01", "type": "lightbulb", "gridPosition": [2,0], "asciiPosition": [0,0], "rectPosition": [0,0] }
],
"wires": [
{ "id": "W01", "fromGrid": [0,0], "toGrid": [1,0], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": true, "connectsToComponentStart": true, "connectsToComponentEnd": true, "isPartOfFork": false, "isPartOfMerge": false },
{ "id": "W02", "fromGrid": [1,0], "toGrid": [2,0], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": true, "connectsToComponentStart": true, "connectsToComponentEnd": true, "isPartOfFork": false, "isPartOfMerge": false }
]
}

---

#### ⚡ Example 2: Basic Parallel Circuit

##### ASCII Layout

            R01            
        -----:----         
        |        |         
   V01  |        |  L01    
    :---+        +---:     
        |        |         
        |   R02  |         
        -----:----         



Grid positions:

* V01: [0,1]
* R01: [2,0]
* R02: [2,2]
* L01: [4,1]

##### Example JSON Output (Step 2)

{
"components": [
{ "id": "V01", "type": "battery", "value": 5, "gridPosition": [0,1], "asciiPosition": [0,0], "rectPosition": [0,0] },
{ "id": "R01", "type": "resistor", "value": 10, "gridPosition": [2,0], "asciiPosition": [0,0], "rectPosition": [0,0] },
{ "id": "R02", "type": "resistor", "value": 10, "gridPosition": [2,2], "asciiPosition": [0,0], "rectPosition": [0,0] },
{ "id": "L01", "type": "lightbulb", "value": 0, "gridPosition": [4,1], "asciiPosition": [0,0], "rectPosition": [0,0] },
{ "id": "F01", "type": "fork", "gridPosition": [1,1], "asciiPosition": [0,0], "rectPosition": [0,0] },
{ "id": "M01", "type": "merge", "gridPosition": [3,1], "asciiPosition": [0,0], "rectPosition": [0,0] },
],
"wires": [
{ "id": "W01", "fromGrid": [0,1], "toGrid": [1,1], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": true, "startTouchesComponent": true, "endTouchesComponent": false, "isPartOfFork": true, "isPartOfMerge": false },
{ "id": "W02", "fromGrid": [1,1], "toGrid": [1,0], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": false },
{ "id": "W03", "fromGrid": [1,1], "toGrid": [1,2], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": false },
{ "id": "W04", "fromGrid": [1,0], "toGrid": [2,0], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": true, "startTouchesComponent": false, "endTouchesComponent": true, "isPartOfFork": false, "isPartOfMerge": false },
{ "id": "W05", "fromGrid": [1,2], "toGrid": [2,2], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": true, "startTouchesComponent": false, "endTouchesComponent": true, "isPartOfFork": false, "isPartOfMerge": false },
{ "id": "W06", "fromGrid": [2,0], "toGrid": [3,0], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": true, "startTouchesComponent": true, "endTouchesComponent": false, "isPartOfFork": false, "isPartOfMerge": false },
{ "id": "W07", "fromGrid": [2,2], "toGrid": [3,2], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": true, "startTouchesComponent": true, "endTouchesComponent": false, "isPartOfFork": false, "isPartOfMerge": false },
{ "id": "W08", "fromGrid": [3,0], "toGrid": [3,1], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": false },
{ "id": "W09", "fromGrid": [3,2], "toGrid": [3,1], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": false },
{ "id": "W10", "fromGrid": [3,1], "toGrid": [4,1], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": true, "startTouchesComponent": false, "endTouchesComponent": true, "isPartOfFork": false, "isPartOfMerge": true }
],
  "formula": "V01 / (1 / (1/R01 + 1/R02) + L01)",
  "verbalPlan": "V01 → [ R01 || R02 ] → L01 → return",
}

#### ⚡ Example 3: Asymmetric Parallel Branches

##### ASCII Layout

            R01  R02            
        -----:----:----         
        |             |         
   V01  |             |  L01    
    :---+             +---:     
        |             |         
        |   R03       |         
        -----:---------         



Grid positions:

* V01: [0,1]
* R01: [2,0]
* R02: [3,0]
* R03: [2,2]
* L01: [5,1]

##### Example JSON Output (Step 2)

{
  "components": [
    { "id": "V01", "type": "battery", "value": 5, "gridPosition": [0,1], "asciiPosition": [0,0], "rectPosition": [0,0] },
    { "id": "R01", "type": "resistor", "value": 10, "gridPosition": [2,0], "asciiPosition": [0,0], "rectPosition": [0,0] },
    { "id": "R02", "type": "resistor", "value": 10, "gridPosition": [3,0], "asciiPosition": [0,0], "rectPosition": [0,0] },
    { "id": "R03", "type": "resistor", "value": 10, "gridPosition": [2,2], "asciiPosition": [0,0], "rectPosition": [0,0] },
    { "id": "L01", "type": "lightbulb", "value": 0, "gridPosition": [5,1], "asciiPosition": [0,0], "rectPosition": [0,0] },
    { "id": "F01", "type": "fork", "gridPosition": [1,1], "asciiPosition": [0,0], "rectPosition": [0,0] },
    { "id": "M01", "type": "merge", "gridPosition": [4,1], "asciiPosition": [0,0], "rectPosition": [0,0] },
  ],
  "wires": [
    { "id": "W01", "fromGrid": [0,1], "toGrid": [1,1], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0],
      "isHorizontal": true, "startTouchesComponent": true, "endTouchesComponent": false, "isPartOfFork": true, "isPartOfMerge": false },
    { "id": "W02", "fromGrid": [1,1], "toGrid": [1,0], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": false },
    { "id": "W03", "fromGrid": [1,1], "toGrid": [1,2], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": false },
    { "id": "W04", "fromGrid": [1,0], "toGrid": [2,0], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0],
      "isHorizontal": true, "startTouchesComponent": false, "endTouchesComponent": true, "isPartOfFork": false, "isPartOfMerge": false },
    { "id": "W05", "fromGrid": [1,2], "toGrid": [2,2], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0],
      "isHorizontal": true, "startTouchesComponent": false, "endTouchesComponent": true, "isPartOfFork": false, "isPartOfMerge": false },
    { "id": "W06", "fromGrid": [2,0], "toGrid": [3,0], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0],
      "isHorizontal": true, "startTouchesComponent": true, "endTouchesComponent": false, "isPartOfFork": false, "isPartOfMerge": false },
    { "id": "W07", "fromGrid": [2,2], "toGrid": [3,2], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0],
      "isHorizontal": true, "startTouchesComponent": true, "endTouchesComponent": false, "isPartOfFork": false, "isPartOfMerge": false },
    { "id": "W08", "fromGrid": [3,0], "toGrid": [3,1], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": false },
    { "id": "W09", "fromGrid": [3,2], "toGrid": [3,1], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0], "isHorizontal": false },
    { "id": "W10", "fromGrid": [3,1], "toGrid": [4,1], "fromASCII": [0,0], "toASCII": [0,0], "fromRect": [0,0], "toRect": [0,0],
      "isHorizontal": true, "startTouchesComponent": false, "endTouchesComponent": true, "isPartOfFork": false, "isPartOfMerge": true }
  ],
  "formula": "V01 / (1 / (1/(R01 + R02) + 1/R03) + L01)",
  "verbalPlan": "V01 → [ R01 + R02 || R03 ] → L01 → return",
}

---

✅ These three examples must be used to test and validate all future Step 2 logic.
We treat these as canonical patterns for grid generation.

More complex examples must build upon this logic.

---

### 🔒 Enforcement Reminder

All future generated layouts **must exactly match** the grid spacing, offsets, and structure of Examples 1–3.  
Failure to apply the +2 X-axis increment on both forks and merges in parallel layouts will result in **invalid wiring logic**. These examples are not illustrative — they are **prescriptive**.

-----------------------------------------------------------------------------------------------------------
