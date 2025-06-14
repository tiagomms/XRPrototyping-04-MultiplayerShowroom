## Step 4: ASCII to Image Conversion

### 🎨 Goal

Transform the ASCII circuit diagram from Step 3 into a high-quality image while calculating and storing pixel positions for all components and wires. This step bridges the gap between ASCII representation and visual rendering.

📥 **Input:**
- Entire JSON structure from Step 3, particularly:
  - `ascii`: array of strings containing the ASCII diagram
  - `asciiSize`: [width, height] dimensions of the ASCII canvas
  - `components[*].asciiPosition`
  - `wires[*].fromASCII` and `wires[*].toASCII`

📤 **Output:**
To our input JSON structure, add:
- `components[*].rectPosition`: [x, y] coordinates in pixels
- `wires[*].fromRect` and `wires[*].toRect`: [x, y] coordinates in pixels
- `imageResolution`: [width, height] dimensions of the output image in pixels
- A PNG image file containing the rendered ASCII diagram

### 🎯 Image Generation Principles

#### 📏 Layout Parameters
These are configurable but have recommended defaults:
- `SCALE = 3` (1 = low, 2 = medium, 3 = high-resolution)
- `BASE_FONT_SIZE = 12`
- `BASE_PADDING = 30`
- Final values are calculated as: `value * SCALE`

#### 🖼️ Font Selection
The system attempts to load fonts in this order:
1. DejaVuSansMono
2. Consolas
3. Courier
4. Arial (fallback)

#### Rules
The following rules are now canon:

1. **Character Dimension Calculation**
   - Calculate average character width using a test string:
     ```python
     test_text = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789:-+|"
     avg_char_width = total_width / len(test_text)
     ```
   - Line height is calculated as: `font_size * 1.2`

2. **Image Dimensions**
   - The image dimensions are calculated using the ASCII canvas size from Step 3:
     ```python
     text_width = ascii_size[0] * char_width
     text_height = ascii_size[1] * line_height
     img_width = text_width + 2 * padding
     img_height = text_height + 2 * padding
     ```
   - The text is centered within these dimensions
   - The final dimensions are stored in the output JSON as `imageResolution: [width, height]`

3. **Pixel Position Calculation**
   - Convert ASCII positions to pixel positions:
     ```python
     pixel_x = start_x + ascii_x * char_width
     pixel_y = start_y + ascii_y * line_height
     ```
   - Where `start_x` and `start_y` are the centering offsets
   - This applies to both component positions and wire endpoints

4. **Drawing Order**
   - Create a white background
   - Draw black text using the selected monospace font
   - Maintain exact character spacing and alignment
   - Preserve all ASCII characters exactly as they appear

### 🔄 Implementation Details

#### 🎨 Image Generation Process

1. **Initialization**
   - Load and configure the font
   - Calculate character dimensions
   - Determine image size and centering offsets

2. **Canvas Creation**
   - Create a new image with white background
   - Calculate text starting position for centering

3. **Text Rendering**
   - Draw each line of ASCII text
   - Maintain exact character spacing
   - Preserve all special characters (:, -, |, +)

4. **Position Updates**
   - Calculate and store pixel positions for all components
   - Calculate and store pixel positions for all wire endpoints
   - Update the JSON structure with new pixel positions

#### 💾 Output Generation

1. **Image File**
   - Save as PNG with high resolution
   - Maintain exact character spacing and alignment
   - Preserve all ASCII characters

2. **JSON Updates**
   - Add pixel positions to all components and wires
   - Maintain all existing data
   - Save with proper formatting

### 📐 Example Output

```json
{
  "components": [
    {
      "id": "V01",
      "asciiPosition": [5, 2],
      "rectPosition": [150, 72]  // Example pixel coordinates
    }
  ],
  "wires": [
    {
      "id": "W01",
      "fromASCII": [5, 3],
      "toASCII": [10, 3],
      "fromRect": [150, 108],    // Example pixel coordinates
      "toRect": [300, 108]       // Example pixel coordinates
    }
  ],
  "asciiSize": [30, 8],           // Dimensions of ASCII canvas
  "imageResolution": [900, 300]   // Dimensions of output image
}
```

### 🔒 Determinism Enforcement

Given the same input JSON (Step 3), the output image and pixel positions (Step 4) **must always be identical**. This is ensured by:
- Fixed font selection order
- Consistent character dimension calculation
- Deterministic pixel position calculation
- Exact centering of text in the image

### 📝 Notes

1. **Font Selection**
   - Monospace fonts are preferred for consistent character spacing
   - Font fallback system ensures compatibility across systems

2. **Resolution**
   - Higher scale values produce better quality images
   - Scale of 3 is recommended for most use cases

3. **Performance**
   - Character dimension calculation is done once at startup
   - Pixel position calculation is linear with the number of components and wires

4. **Compatibility**
   - Output PNG format ensures wide compatibility
   - JSON structure maintains backward compatibility with previous steps 