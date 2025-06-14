using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using CircuitProcessor;
using Newtonsoft.Json.Linq;

namespace CircuitProcessor
{
    /// <summary>
    /// Handles the conversion of circuit data into ASCII representation
    /// </summary>
    public class CircuitASCIIDrawer : MonoBehaviour
    {
        // Fixed layout parameters from specification
        [SerializeField] private int ASCII_CELL_WIDTH = 5;
        [SerializeField] private int ASCII_CELL_HEIGHT = 3;
        [SerializeField] private int ASCII_HORIZONTAL_PADDING = 5;  // 1 cellWidth on each side
        [SerializeField] private int ASCII_VERTICAL_PADDING = 1;  // 1 row top and bottom

        private char[,] canvas;
        private int canvasWidth;
        private int canvasHeight;

        /// <summary>
        /// Draws the complete circuit
        /// </summary>
        public List<string> InitializeDrawASCIICircuit(CircuitData circuitData)
        {
            InitializeCanvas(circuitData);

            // Draw all wires first
            foreach (var wire in circuitData.wires)
            {
                if (wire.isHorizontal)
                {
                    DrawHorizontalWire(circuitData, wire);
                }
                else
                {
                    DrawVerticalWire(circuitData, wire);
                }
            }

            // Draw all components
            foreach (var component in circuitData.components)
            {
                DrawComponent(circuitData, component);
            }

            // Convert canvas to string array
            var asciiOutput = new List<string>();
            for (int y = 0; y < canvasHeight; y++)
            {
                var line = new StringBuilder();
                for (int x = 0; x < canvasWidth; x++)
                {
                    line.Append(canvas[y, x]);
                }
                asciiOutput.Add(line.ToString());
            }
            
            /*
            // Remove empty lines at the end
            while (asciiOutput.Count > 0 && string.IsNullOrWhiteSpace(asciiOutput[asciiOutput.Count - 1]))
            {
                asciiOutput.RemoveAt(asciiOutput.Count - 1);
            }
            */
            return asciiOutput;
        }

        /// <summary>
        /// Calculates ASCII position based on grid position with padding
        /// </summary>
        private Vector2Int CalculateAsciiPosition(Vector2Int gridPosition)
        {
            return new Vector2Int(
                gridPosition.x * ASCII_CELL_WIDTH + ASCII_HORIZONTAL_PADDING,
                gridPosition.y * ASCII_CELL_HEIGHT + ASCII_VERTICAL_PADDING
            );
        }

        /// <summary>
        /// Initializes the ASCII canvas based on circuit data
        /// </summary>
        private void InitializeCanvas(CircuitData circuitData)
        {
            // Calculate ASCII positions for all components and wires
            foreach (var component in circuitData.components)
            {
                component.asciiPosition = CalculateAsciiPosition(component.gridPosition);
            }

            foreach (var wire in circuitData.wires)
            {
                wire.fromASCII = CalculateAsciiPosition(wire.fromGrid);
                wire.toASCII = CalculateAsciiPosition(wire.toGrid);
            }

            // Find maximum ASCII positions
            var maxAsciiX = Math.Max(
                circuitData.components.Max(c => c.asciiPosition.x),
                circuitData.wires.Max(w => Math.Max(w.fromASCII.x, w.toASCII.x))
            );
            var maxAsciiY = Math.Max(
                circuitData.components.Max(c => c.asciiPosition.y),
                circuitData.wires.Max(w => Math.Max(w.fromASCII.y, w.toASCII.y))
            );

            // Calculate canvas dimensions with padding
            canvasWidth = maxAsciiX + ASCII_HORIZONTAL_PADDING;
            canvasHeight = maxAsciiY + ASCII_VERTICAL_PADDING;

            // Set the ASCII size in the circuit data
            circuitData.asciiSize = new Vector2Int(canvasWidth, canvasHeight);

            // Initialize canvas with spaces
            canvas = new char[canvasHeight, canvasWidth];
            for (int y = 0; y < canvasHeight; y++)
            {
                for (int x = 0; x < canvasWidth; x++)
                {
                    canvas[y, x] = ' ';
                }
            }
        }

        /// <summary>
        /// Draws a horizontal wire segment
        /// </summary>
        private void DrawHorizontalWire(CircuitData circuitData, Wire wire)
        {
            int x1 = wire.fromASCII.x;
            int y = wire.fromASCII.y;
            int x2 = wire.toASCII.x;

            // Calculate number of cells needed
            int totalWidth = x2 - x1;
            int numCells = (totalWidth + ASCII_CELL_WIDTH - 1) / ASCII_CELL_WIDTH;

            // Ensure we're within canvas bounds
            if (y >= canvasHeight || x1 >= canvasWidth)
                return;

            // For each cell in the wire
            for (int cell = 0; cell < numCells; cell++)
            {
                int cellStart = x1 + cell * ASCII_CELL_WIDTH;

                // All wire segments are now just dashes - no special symbols
                // Components will overwrite with '+' where needed
                string wireString = new string('-', ASCII_CELL_WIDTH);

                // Draw the wire, but only if the position is empty
                for (int i = 0; i < wireString.Length; i++)
                {
                    if (cellStart + i < canvasWidth && canvas[y, cellStart + i] == ' ')
                    {
                        canvas[y, cellStart + i] = wireString[i];
                    }
                }
            }
        }

        /// <summary>
        /// Draws a vertical wire segment
        /// </summary>
        private void DrawVerticalWire(CircuitData circuitData, Wire wire)
        {
            int x = wire.fromASCII.x;
            int y1 = wire.fromASCII.y;
            int y2 = wire.toASCII.y;

            // Add padding offset
            int startY = Math.Min(y1, y2);
            int endY = Math.Max(y1, y2);

            // Ensure we're within canvas bounds
            if (x >= canvasWidth)
                return;

            canvas[y1, x] = '-';
            // Draw vertical line between the two points, skipping the first character
            for (int y = startY + 1; y < endY; y++)
            {
                if (y < canvasHeight && canvas[y, x] == ' ')  // Draw only on Empty space
                {
                    canvas[y, x] = '|';
                }
            }
        }

        /// <summary>
        /// Draws a component on the canvas
        /// </summary>
        private void DrawComponent(CircuitData circuitData, Component component)
        {
            int x = component.asciiPosition.x;
            int y = component.asciiPosition.y;

            // Ensure we're within canvas bounds
            if (y >= canvasHeight || x >= canvasWidth)
                return;

            // Check if this is a fork or merge component
            if (component.type == "fork" || component.type == "merge")
            {
                // For fork and merge, just place a '+' at the position (always overwrite)
                canvas[y, x] = '+';
            }
            else
            {
                // Regular component behavior
                // Component ID must be exactly 3 characters
                string compId = component.id;
                if (compId.Length != 3)
                {
                    compId = compId.Length > 3 ? compId.Substring(0, 3) : compId.PadRight(3);
                }

                // Place component ID above the ':' anchor (center-aligned)
                int compY = y - 1;
                if (compY >= 0)
                {
                    // Center-align the 3-character ID on the ':' position
                    int idStartX = x - 1;
                    for (int i = 0; i < compId.Length; i++)
                    {
                        if (idStartX + i >= 0 && idStartX + i < canvasWidth)
                        {
                            // Check if we're modifying an existing wire segment
                            if (canvas[compY, idStartX + i] != ' ')
                            {
                                circuitData.violations.Add(new Violation
                                {
                                    type = "component_overlay",
                                    message = $"Component {component.id} overlays existing content at position [{idStartX + i}, {compY}]"
                                });
                            }
                            canvas[compY, idStartX + i] = compId[i];
                        }
                    }
                }

                // Place the component anchor ':' (always overwrite whatever is there)
                canvas[y, x] = ':';
            }
        }
    }
}