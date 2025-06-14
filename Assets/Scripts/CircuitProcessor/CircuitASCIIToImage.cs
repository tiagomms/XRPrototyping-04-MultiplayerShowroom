using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Linq;

namespace CircuitProcessor
{
    /// <summary>
    /// Converts ASCII circuit representation to an image with proper styling and positioning
    /// </summary>
    public class CircuitASCIIToImage : MonoBehaviour
    {
        [Header("Image Settings")]
        [SerializeField] private int scale = 3;  // 1 = low, 2 = medium, 3 = high-resolution
        [SerializeField] private int baseFontSize = 16;
        [SerializeField] private int basePadding = 30;
        [SerializeField] private Color componentColor = Color.green;  // Color for component IDs and ':' symbols
        [SerializeField] private Color wireColor = Color.black;       // Color for wires and other symbols
        [SerializeField] private Color backgroundColor = Color.white;

        [Header("Font Settings")]
        [SerializeField] private Font monospaceFont;  // Assign a monospace font in the inspector

        private string outputPath;
        private string buildPrefix;
        private int charWidth;
        private int lineHeight;
        private int startX;
        private int startY;

        /// <summary>
        /// Sets the output information for the image generation
        /// </summary>
        public void SetOutputInfo(string path, string prefix)
        {
            outputPath = path;
            buildPrefix = prefix;
        }

        /// <summary>
        /// Generates an image from the ASCII circuit representation
        /// </summary>
        public void GenerateImage(CircuitData circuitData)
        {
            if (circuitData.ascii == null || circuitData.ascii.Count == 0)
            {
                Debug.LogError("No ASCII data to convert to image");
                return;
            }

            // Calculate dimensions
            int fontSize = baseFontSize * scale;
            int scaledPadding = basePadding * scale;

            // Get font metrics
            if (monospaceFont == null)
            {
                Debug.LogError("No monospace font assigned!");
                return;
            }

            // Calculate character dimensions for monospace font
            charWidth = GetCharacterWidth(fontSize);
            lineHeight = fontSize + 2;  // Add small spacing between lines

            // Calculate image dimensions
            int maxLineLength = circuitData.ascii.Max(line => line.Length);
            int textWidth = maxLineLength * charWidth;
            int textHeight = circuitData.ascii.Count * lineHeight;

            int imgWidth = textWidth + 2 * scaledPadding;
            int imgHeight = textHeight + 2 * scaledPadding;

            // Calculate starting position to center the text
            startX = (imgWidth - textWidth) / 2;
            startY = (imgHeight - textHeight) / 2;

            // Update component and wire pixel positions
            UpdateRectPositions(circuitData);

            // Create texture and draw text
            Texture2D texture = CreateTextureWithASCII(imgWidth, imgHeight, circuitData.ascii);

            // Apply changes and save
            texture.Apply();
            SaveImage(texture, circuitData);
        }

        /// <summary>
        /// Gets the character width for monospace font by measuring a sample string
        /// </summary>
        private int GetCharacterWidth(int fontSize)
        {
            // Create a test string with common characters used in circuit diagrams
            string testString = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789:-+|";
            
            // Create a temporary texture to measure text
            RenderTexture tempRT = RenderTexture.GetTemporary(1024, 64, 0);
            RenderTexture.active = tempRT;
            
            // Create temporary objects for measurement
            GameObject tempCameraGO = new GameObject("TempMeasureCamera");
            Camera tempCamera = tempCameraGO.AddComponent<Camera>();
            tempCamera.targetTexture = tempRT;
            tempCamera.orthographic = true;
            tempCamera.orthographicSize = 32;
            tempCamera.transform.position = new Vector3(512, 32, -10);
            tempCamera.backgroundColor = Color.black;
            tempCamera.clearFlags = CameraClearFlags.SolidColor;

            GameObject tempCanvasGO = new GameObject("TempMeasureCanvas");
            Canvas tempCanvas = tempCanvasGO.AddComponent<Canvas>();
            tempCanvas.renderMode = RenderMode.WorldSpace;
            tempCanvas.worldCamera = tempCamera;
            
            RectTransform canvasRect = tempCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1024, 64);
            canvasRect.position = new Vector3(512, 32, 0);

            // Create text object
            GameObject textGO = new GameObject("MeasureText");
            textGO.transform.SetParent(tempCanvasGO.transform, false);
            
            UnityEngine.UI.Text text = textGO.AddComponent<UnityEngine.UI.Text>();
            text.font = monospaceFont;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.text = testString;
            text.alignment = TextAnchor.MiddleLeft;
            
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.zero;
            textRect.sizeDelta = new Vector2(1024, 64);
            textRect.anchoredPosition = new Vector2(0, 0);

            // Force update and render
            Canvas.ForceUpdateCanvases();
            tempCamera.Render();

            // Read the rendered text to measure actual width
            Texture2D measureTexture = new Texture2D(1024, 64, TextureFormat.RGB24, false);
            measureTexture.ReadPixels(new Rect(0, 0, 1024, 64), 0, 0);
            measureTexture.Apply();

            // Find the actual text width by scanning for non-black pixels
            int textWidth = 0;
            Color[] pixels = measureTexture.GetPixels();
            
            for (int x = 1023; x >= 0; x--) // Scan from right to left
            {
                for (int y = 0; y < 64; y++)
                {
                    Color pixel = pixels[y * 1024 + x];
                    if (pixel.r > 0.1f || pixel.g > 0.1f || pixel.b > 0.1f) // Non-black pixel found
                    {
                        textWidth = x + 1;
                        goto WidthFound;
                    }
                }
            }
            WidthFound:

            // Calculate average character width
            int charWidth = textWidth / testString.Length;

            // Cleanup
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(tempRT);
            DestroyImmediate(tempCameraGO);
            DestroyImmediate(tempCanvasGO);
            DestroyImmediate(measureTexture);

            Debug.Log($"Measured character width: {charWidth} pixels for font size {fontSize}");
            return charWidth;
        }

        /// <summary>
        /// Converts ASCII coordinates to pixel coordinates
        /// </summary>
        private Vector2Int AsciitoRect(int asciiX, int asciiY)
        {
            int baseX = startX + asciiX * charWidth;
            int baseY = startY + asciiY * lineHeight;
            
            // Add half character width and half line height to center the position
            int pixelX = baseX + charWidth / 2;
            int pixelY = baseY + lineHeight / 2;
            
            return new Vector2Int(pixelX, pixelY);
        }

        /// <summary>
        /// Updates pixel positions for components and wires
        /// </summary>
        private void UpdateRectPositions(CircuitData circuitData)
        {
            // Update component positions
            foreach (var component in circuitData.components)
            {
                Vector2Int pixelPos = AsciitoRect(component.asciiPosition.x, component.asciiPosition.y);
                component.rectPosition = new Vector2(pixelPos.x, pixelPos.y);
            }

            // Update wire positions
            foreach (var wire in circuitData.wires)
            {
                Vector2Int fromRect = AsciitoRect(wire.fromASCII.x, wire.fromASCII.y);
                Vector2Int toRect = AsciitoRect(wire.toASCII.x, wire.toASCII.y);
                
                wire.fromRect = new Vector2(fromRect.x, fromRect.y);
                wire.toRect = new Vector2(toRect.x, toRect.y);
            }
        }

        /// <summary>
        /// Creates a texture with ASCII text using Unity's font rendering (simplified approach)
        /// </summary>
        private Texture2D CreateTextureWithASCII(int width, int height, List<string> asciiLines)
        {
            // Create a render texture to draw on
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = renderTexture;

            // Clear with background color
            GL.Clear(true, true, backgroundColor);

            // Create a temporary camera for rendering text
            GameObject cameraGO = new GameObject("TempCamera");
            Camera camera = cameraGO.AddComponent<Camera>();
            camera.targetTexture = renderTexture;
            camera.orthographic = true;
            camera.orthographicSize = height / 2f;
            camera.transform.position = new Vector3(width / 2f, height / 2f, -10);
            camera.backgroundColor = backgroundColor;
            camera.clearFlags = CameraClearFlags.SolidColor;

            // Create canvas for UI text rendering
            GameObject canvasGO = new GameObject("TempCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            
            // Set canvas size to match our render texture
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(width, height);
            canvasRect.position = new Vector3(width / 2f, height / 2f, 0);

            // Draw each line of ASCII text
            for (int i = 0; i < asciiLines.Count; i++)
            {
                string line = asciiLines[i];
                float yPos = height - startY - (i * lineHeight) - lineHeight; // Flip Y coordinate

                // Draw each character with appropriate color
                for (int j = 0; j < line.Length; j++)
                {
                    char c = line[j];
                    if (c == ' ') continue; // Skip spaces

                    float xPos = startX + (j * charWidth);

                    // Determine color based on character
                    Color color = (c == ':' || char.IsLetterOrDigit(c)) ? componentColor : wireColor;

                    // Create text object for this character
                    GameObject textGO = new GameObject($"Char_{i}_{j}");
                    textGO.transform.SetParent(canvasGO.transform, false);

                    UnityEngine.UI.Text text = textGO.AddComponent<UnityEngine.UI.Text>();
                    text.font = monospaceFont;
                    text.fontSize = baseFontSize * scale;
                    text.color = color;
                    text.text = c.ToString();
                    text.alignment = TextAnchor.UpperLeft;

                    RectTransform textRect = text.GetComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.zero;
                    textRect.sizeDelta = new Vector2(charWidth, lineHeight);
                    textRect.anchoredPosition = new Vector2(xPos, yPos);
                }
            }

            // Force canvas update and render
            Canvas.ForceUpdateCanvases();
            camera.Render();

            // Read pixels from render texture
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);

            // Cleanup
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(renderTexture);
            DestroyImmediate(cameraGO);
            DestroyImmediate(canvasGO);

            return texture;
        }

        /// <summary>
        /// Saves the generated image and updates the circuit data
        /// </summary>
        private void SaveImage(Texture2D texture, CircuitData circuitData)
        {
            try
            {
                // Save image
                string imagePath = Path.Combine(outputPath, $"{buildPrefix}_circuit.png");
                byte[] pngData = texture.EncodeToPNG();
                File.WriteAllBytes(imagePath, pngData);

                // Update circuit data with image resolution
                circuitData.imageResolution = new Vector2Int(texture.width, texture.height);

                // Save updated JSON
                string jsonPath = Path.Combine(outputPath, $"{buildPrefix}_with_pixels.json");
                string json = JsonConvert.SerializeObject(circuitData, Formatting.Indented);
                File.WriteAllText(jsonPath, json);

                Debug.Log($"Image saved to: {imagePath}");
                Debug.Log($"Updated JSON saved to: {jsonPath}");
                Debug.Log($"Image dimensions: {texture.width}x{texture.height}");
                Debug.Log($"Character dimensions: {charWidth}x{lineHeight}");
                Debug.Log($"Text starts at pixel: ({startX}, {startY})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error saving image: {e.Message}");
            }
            finally
            {
                // Clean up texture
                DestroyImmediate(texture);
            }
        }
    }
}