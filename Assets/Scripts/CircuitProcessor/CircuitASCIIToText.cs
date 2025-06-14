using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CircuitProcessor;

namespace CircuitProcessor
{
    public class CircuitASCIIToText : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private TextMeshProUGUI circuitText;

        [Header("Colors")]
        [SerializeField] private Color componentColor = Color.green;
        [SerializeField] private Color wireColor = Color.white;

        [Header("Debug")]
        [SerializeField] private bool showDebugMarkers = false;
        [SerializeField] private Color debugMarkerColor = Color.red;
        [SerializeField] private Transform markersParent;
        [SerializeField] private bool sendToXRDebugLogViewer = true;
        [SerializeField] private bool sendToDebugLog = true;

        private CircuitData circuitData;
        private RectTransform canvasRect;
        private float charWidth;
        private float charHeight;
        private Vector2 startPosition;
        private List<GameObject> debugMarkers = new List<GameObject>();

        void Start()
        {
            if (targetCanvas == null)
            {
                Debug.LogError("Target Canvas is not assigned!");
                return;
            }

            if (circuitText == null)
            {
                Debug.LogError("Circuit Text component is not assigned!");
                return;
            }

            canvasRect = targetCanvas.GetComponent<RectTransform>();
            targetCanvas.gameObject.SetActive(false);
        }

        /// <summary>
        /// Sets the circuit data and updates the display
        /// </summary>
        /// <param name="data">The circuit data to display</param>
        public void InitializeASCIIToText(CircuitData data)
        {
            ClearDebugMarkers();
            circuitData = data;
            if (circuitData == null)
            {
                targetCanvas.gameObject.SetActive(false);
                return;
            }

            targetCanvas.gameObject.SetActive(true);
            SetupDisplay();
            UpdateRectPositions();
        }

        void SetupDisplay()
        {
            if (circuitData == null || circuitData.ascii == null || circuitData.ascii.Count == 0)
            {
                Debug.LogError("No ASCII data to display!");
                return;
            }

            // Calculate character dimensions
            CalculateCharacterDimensions();

            // Create the complete ASCII text with color tags
            string coloredText = CreateColoredASCIIText();

            // Set the text and update the display
            circuitText.text = coloredText;
            circuitText.ForceMeshUpdate();
        }

        void CalculateCharacterDimensions()
        {
            // Force update to get accurate measurements
            Canvas.ForceUpdateCanvases();
            circuitText.ForceMeshUpdate();

            // Use a single character to measure dimensions
            circuitText.text = "M";
            circuitText.ForceMeshUpdate();

            // Use TextMeshPro's built-in character spacing and line spacing
            charWidth = circuitText.preferredWidth * circuitText.characterSpacing;
            charHeight = circuitText.preferredHeight * circuitText.lineSpacing;
        }

        string CreateColoredASCIIText()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            // Get the maximum line length to ensure consistent grid
            int maxLineLength = circuitData.ascii.Max(line => line.Length);

            for (int y = 0; y < circuitData.ascii.Count; y++)
            {
                string line = circuitData.ascii[y];
                
                // Process each character in the line, including padding spaces
                for (int x = 0; x < maxLineLength; x++)
                {
                    char character = x < line.Length ? line[x] : ' ';
                    string colorTag = (character == ':' || char.IsLetterOrDigit(character)) 
                        ? $"<color=#{ColorUtility.ToHtmlStringRGB(componentColor)}>"
                        : $"<color=#{ColorUtility.ToHtmlStringRGB(wireColor)}>";

                    sb.Append(colorTag);
                    sb.Append(character);
                    sb.Append("</color>");
                }

                // Add newline if not the last line
                if (y < circuitData.ascii.Count - 1)
                {
                    sb.Append("\n");
                }
            }

            return sb.ToString();
        }

        void UpdateRectPositions()
        {
            if (circuitData == null) return;

            // Update component pixel positions
            foreach (var component in circuitData.components)
            {
                Vector2 pixelPos = ASCIIToRectTransformPosition(component.asciiPosition.x, component.asciiPosition.y);
                component.rectPosition = pixelPos;

                if (showDebugMarkers)
                {
                    CreateDebugMarker(pixelPos, component.id, debugMarkerColor);
                }
            }

            // Update wire pixel positions
            foreach (var wire in circuitData.wires)
            {
                Vector2 fromRect = ASCIIToRectTransformPosition(wire.fromASCII.x, wire.fromASCII.y);
                Vector2 toRect = ASCIIToRectTransformPosition(wire.toASCII.x, wire.toASCII.y);

                wire.fromRect = fromRect;
                wire.toRect = toRect;
            }

            // Log some example conversions for verification
            XRDebugLogViewer.Log("Example pixel position conversions:", sendToXRDebugLogViewer, sendToDebugLog);
            for (int i = 0; i < Mathf.Min(3, circuitData.components.Count); i++)
            {
                var component = circuitData.components[i];
                XRDebugLogViewer.Log($"{component.id}: ASCII [{component.asciiPosition.x}, {component.asciiPosition.y}] -> Pixel [{component.rectPosition.x:F2}, {component.rectPosition.y:F2}]", sendToXRDebugLogViewer, sendToDebugLog);
            }
        }

        Vector2 ASCIIToRectTransformPosition(int asciiX, int asciiY)
        {
            if (asciiX < 0 || asciiX >= circuitData.asciiSize.x ||
                asciiY < 0 || asciiY >= circuitData.asciiSize.y)
            {
                XRDebugLogViewer.LogError($"[{nameof(CircuitASCIIToText)}] Invalid ASCII position: [{asciiX}, {asciiY}]");
                return Vector2.zero;
            }

            // Get the character index in the text
            int charIndex = 0;
            int maxLineLength = circuitData.ascii.Max(line => line.Length);

            // Calculate total characters up to the target line
            for (int y = 0; y < asciiY; y++)
            {
                charIndex += maxLineLength + 1; // +1 for newline
            }
            // Add the x position
            charIndex += asciiX;

            XRDebugLogViewer.Log($"[{nameof(CircuitASCIIToText)}] ASCII [{asciiX}, {asciiY}] -> charIndex: {charIndex}", sendToXRDebugLogViewer, sendToDebugLog);

            // Get the character info from TMP
            TMP_TextInfo textInfo = circuitText.textInfo;
            if (textInfo == null || charIndex >= textInfo.characterCount)
            {
                XRDebugLogViewer.LogError($"[{nameof(CircuitASCIIToText)}] Invalid character index: {charIndex} (total chars: {textInfo?.characterCount ?? 0})");
                return Vector2.zero;
            }

            TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];
            
            // Get the center position of the character in local space
            Vector3 charCenter = (charInfo.bottomLeft + charInfo.topRight) * 0.5f;
            return new Vector2(charCenter.x, charCenter.y);
            /*
            XRDebugLogViewer.Log($"[{nameof(CircuitASCIIToText)}] ASCII [{asciiX}, {asciiY}] -> CharCenter [{charCenter.x:F2}, {charCenter.y:F2}]", sendToXRDebugLogViewer, sendToDebugLog);

            Vector3 worldPos = circuitText.transform.TransformPoint(charCenter);
            
            // Convert to canvas local position
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(targetCanvas.worldCamera, worldPos);
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                targetCanvas.worldCamera,
                out localPos);

            //XRDebugLogViewer.Log($"[{nameof(CircuitASCIIToText)}] ASCII [{asciiX}, {asciiY}] -> Pixel [{localPos.x:F2}, {localPos.y:F2}]", sendToXRDebugLogViewer, sendToDebugLog);
            return localPos;
            */
        }

        void ClearDebugMarkers()
        {
            foreach (var marker in debugMarkers)
            {
                if (marker != null)
                {
                    Destroy(marker);
                }
            }
            debugMarkers.Clear();
        }

        void CreateDebugMarker(Vector2 position, string label, Color color)
        {
            GameObject marker = new GameObject($"Debug_Marker_{label}");
            marker.transform.SetParent(markersParent.transform, false);
            debugMarkers.Add(marker);

            // Create marker using UI Image
            Image markerImage = marker.AddComponent<Image>();
            markerImage.color = color;
            markerImage.raycastTarget = false;

            RectTransform markerRect = marker.GetComponent<RectTransform>();
            markerRect.anchorMin = Vector2.one * 0.5f;
            markerRect.anchorMax = Vector2.one * 0.5f;
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.anchoredPosition = position;
            markerRect.sizeDelta = new Vector2(10, 10);

            // Add label text
            GameObject labelObj = new GameObject($"Label_{label}");
            labelObj.transform.SetParent(marker.transform, false);

            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 8;
            labelText.color = color;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.raycastTarget = false;

            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = new Vector2(0, 15);
        }

        public CircuitData GetCircuitData()
        {
            return circuitData;
        }
    }
}