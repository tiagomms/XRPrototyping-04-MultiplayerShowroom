using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CircuitProcessor;

namespace CircuitProcessor
{
    /// <summary>
    /// Handles the assignment of grid positions and wire generation for circuit components.
    /// This is a direct port of the Python circuit_grid_assigner.py script.
    /// </summary>
    public class CircuitGridAssigner : MonoBehaviour
    {
        [Tooltip("Normalize all grid positions so minimum is 0 on both axes")]
        [SerializeField] private bool isGridNormalized = true;

        [Header("Debug")]
        [SerializeField] private bool sendToXRDebugLogViewer = true;
        [SerializeField] private bool sendToDebugLog = true;
        /// <summary>
        /// Main entry point for processing the circuit data
        /// </summary>
        public CircuitData InitializeGridAssigner(CircuitData data)
        {
            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Starting grid assignment with {data.components?.Count ?? 0} components", sendToXRDebugLogViewer, sendToDebugLog);

            if (data == null)
            {
                XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] ERROR: Input data is null", sendToXRDebugLogViewer, sendToDebugLog);
                return null;
            }

            if (string.IsNullOrEmpty(data.verbalPlan))
            {
                XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] ERROR: Verbal plan is empty", sendToXRDebugLogViewer, sendToDebugLog);
                return null;
            }

            var orderedIds = ParseVerbalPlan(data.verbalPlan);
            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Parsed {orderedIds.Count} components from verbal plan", sendToXRDebugLogViewer, sendToDebugLog);

            // Process the verbal plan and create components with fork/merge nodes
            var allComponents = ProcessVerbalPlanWithForkMerge(orderedIds, data.components);
            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Created {allComponents.Count} total components (including forks/merges)", sendToXRDebugLogViewer, sendToDebugLog);

            // Generate wires after all components are positioned
            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Starting wire generation for {allComponents.Count} components", sendToXRDebugLogViewer, sendToDebugLog);
            var wires = GenerateWires(allComponents);
            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Generated {wires.Count} wires", sendToXRDebugLogViewer, sendToDebugLog);

            // Normalize all grid positions so minimum is 0 on both axes
            if (isGridNormalized)
            {
                NormalizeGridPositions(allComponents, wires);
                XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Normalized all grid positions", sendToXRDebugLogViewer, sendToDebugLog);
            }

            // Create final output by copying all original data
            var finalOutput = new CircuitData
            {
                components = allComponents,
                wires = wires,
                formula = data.formula,
                verbalPlan = data.verbalPlan,
                conditionalBranches = data.conditionalBranches,
                notes = data.notes,
                additionalData = data.additionalData
            };

            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Completed grid assignment. Output contains {finalOutput.components.Count} components and {finalOutput.wires.Count} wires", sendToXRDebugLogViewer, sendToDebugLog);
            return finalOutput;
        }

        /// <summary>
        /// Normalizes all grid positions so minimum X and Y are 0
        /// </summary>
        private void NormalizeGridPositions(List<Component> components, List<Wire> wires)
        {
            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Starting grid position normalization", sendToXRDebugLogViewer, sendToDebugLog);

            // Find minimum X and Y across all components and wires
            var allPositions = new List<Vector2Int>();

            // Add component positions
            foreach (var comp in components)
            {
                allPositions.Add(comp.gridPosition);
            }

            // Add wire positions
            foreach (var wire in wires)
            {
                allPositions.Add(wire.fromGrid);
                allPositions.Add(wire.toGrid);
            }

            if (allPositions.Count == 0)
            {
                XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] No positions to normalize", sendToXRDebugLogViewer, sendToDebugLog);
                return;
            }

            var minX = allPositions.Min(p => p.x);
            var minY = allPositions.Min(p => p.y);

            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Minimum positions - X: {minX}, Y: {minY}", sendToXRDebugLogViewer, sendToDebugLog);

            // Only normalize if there are negative positions
            if (minX < 0 || minY < 0)
            {
                var offsetX = minX < 0 ? -minX : 0;
                var offsetY = minY < 0 ? -minY : 0;

                XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Applying offset - X: {offsetX}, Y: {offsetY}", sendToXRDebugLogViewer, sendToDebugLog);

                // Normalize component positions
                foreach (var comp in components)
                {
                    var oldPos = comp.gridPosition;
                    comp.gridPosition = new Vector2Int(oldPos.x + offsetX, oldPos.y + offsetY);
                    XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Normalized component {comp.id}: {oldPos} -> {comp.gridPosition}", sendToXRDebugLogViewer, sendToDebugLog);
                }

                // Normalize wire positions
                foreach (var wire in wires)
                {
                    var oldFrom = wire.fromGrid;
                    var oldTo = wire.toGrid;
                    wire.fromGrid = new Vector2Int(oldFrom.x + offsetX, oldFrom.y + offsetY);
                    wire.toGrid = new Vector2Int(oldTo.x + offsetX, oldTo.y + offsetY);
                    XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Normalized wire {wire.id}: {oldFrom}->{oldTo} to {wire.fromGrid}->{wire.toGrid}", sendToXRDebugLogViewer, sendToDebugLog);
                }
            }
            else
            {
                XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] No normalization needed - all positions are non-negative", sendToXRDebugLogViewer, sendToDebugLog);
            }
        }

        /// <summary>
        /// Parses the verbal plan into an ordered list of component IDs and parallel branch tokens
        /// </summary>
        private List<string> ParseVerbalPlan(string plan)
        {
            if (string.IsNullOrEmpty(plan))
            {
                XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] ERROR: Empty verbal plan", sendToXRDebugLogViewer, sendToDebugLog);
                return new List<string>();
            }

            // Replace arrow types with consistent delimiter
            plan = plan.Replace("→", "->").Replace("⇒", "->");

            // Remove return part if present
            plan = plan.Replace(" -> return", "");
            var tokens = plan.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim())
                            .Where(t => !string.IsNullOrEmpty(t))
                            .ToList();

            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Parsed verbal plan: {plan}", sendToXRDebugLogViewer, sendToDebugLog);
            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Found {tokens.Count} tokens: {string.Join(", ", tokens)}", sendToXRDebugLogViewer, sendToDebugLog);
            return tokens;
        }

        /// <summary>
        /// Checks if a token represents a parallel branch
        /// </summary>
        private bool IsParallelBranch(string token)
        {
            return token.StartsWith("[") && token.EndsWith("]");
        }

        /// <summary>
        /// Parses components within a parallel branch and returns branches with their series components
        /// </summary>
        private List<List<string>> ParseParallelBranches(string token)
        {
            // Remove brackets
            var inner = token.Trim('[', ']');
            var branches = new List<List<string>>();

            if (inner.Contains("||"))
            {
                // Split by parallel operator
                var parallelBranches = inner.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(b => b.Trim())
                                           .ToList();

                foreach (var branch in parallelBranches)
                {
                    // Each branch may contain series components separated by '+'
                    if (branch.Contains("+"))
                    {
                        var seriesComponents = branch.Split(new[] { "+" }, StringSplitOptions.RemoveEmptyEntries)
                                                   .Select(c => c.Trim())
                                                   .ToList();
                        branches.Add(seriesComponents);
                    }
                    else
                    {
                        branches.Add(new List<string> { branch.Trim() });
                    }
                }
            }

            return branches;
        }

        /// <summary>
        /// Processes the verbal plan and creates all components including fork/merge nodes
        /// </summary>
        private List<Component> ProcessVerbalPlanWithForkMerge(List<string> tokens, List<Component> originalComponents)
        {
            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Processing verbal plan with fork/merge logic", sendToXRDebugLogViewer, sendToDebugLog);

            var allComponents = new List<Component>();
            var componentDict = originalComponents.ToDictionary(c => c.id, c => c);
            var currentX = 0;
            var currentY = 0;
            var forkCounter = 1;
            var mergeCounter = 1;

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (IsParallelBranch(token))
                {
                    XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Processing parallel branch: {token}", sendToXRDebugLogViewer, sendToDebugLog);

                    // Parse parallel branches
                    var branches = ParseParallelBranches(token);
                    var branchCount = branches.Count;
                    var maxBranchLength = branches.Max(b => b.Count);

                    XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Found {branchCount} branches, max length: {maxBranchLength}", sendToXRDebugLogViewer, sendToDebugLog);

                    // Create fork node
                    var forkId = $"F{forkCounter:D2}";
                    var fork = new Component
                    (
                        forkId,
                        "fork",
                        0,
                        new Vector2Int(currentX, currentY),
                        Vector2Int.zero,
                        Vector2.zero
                    );
                    allComponents.Add(fork);
                    forkCounter++;

                    XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Created fork {forkId} at position {fork.gridPosition}", sendToXRDebugLogViewer, sendToDebugLog);

                    // Calculate Y positions for branches (symmetric around fork Y)
                    var branchYPositions = new List<int>();
                    if (branchCount == 1)
                    {
                        branchYPositions.Add(currentY);
                    }
                    else if (branchCount == 2)
                    {
                        branchYPositions.Add(currentY - 1);
                        branchYPositions.Add(currentY + 1);
                    }
                    else
                    {
                        // FIXME: this logic is incorrect. fix later
                        // For 3+ branches, distribute symmetrically
                        int offset = (branchCount - 1) / 2;
                        for (int j = 0; j < branchCount; j++)
                        {
                            branchYPositions.Add(currentY + (j - offset));
                        }
                    }

                    // Place components in each branch
                    for (int branchIndex = 0; branchIndex < branches.Count; branchIndex++)
                    {
                        var branch = branches[branchIndex];
                        var branchY = branchYPositions[branchIndex];
                        var branchX = currentX + 1; // Start after fork

                        XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Processing branch {branchIndex} with {branch.Count} components at Y={branchY}", sendToXRDebugLogViewer, sendToDebugLog);

                        foreach (var compId in branch)
                        {
                            if (componentDict.ContainsKey(compId))
                            {
                                var originalComp = componentDict[compId];
                                var branchComp = new Component
                                (
                                    originalComp.id,
                                    originalComp.type,
                                    originalComp.Value,
                                    new Vector2Int(branchX, branchY),
                                    Vector2Int.zero,
                                    Vector2.zero
                                );
                                allComponents.Add(branchComp);
                                XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Placed component {compId} at position {branchComp.gridPosition}", sendToXRDebugLogViewer, sendToDebugLog);
                                branchX++; // Move to next X position for series components
                            }
                            else
                            {
                                XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] WARNING: Component {compId} not found in original components", sendToXRDebugLogViewer, sendToDebugLog);
                            }
                        }
                    }

                    // Create merge node
                    var mergeX = currentX + maxBranchLength + 1;
                    var mergeId = $"M{mergeCounter:D2}";
                    var merge = new Component
                    (
                        mergeId,
                        "merge",
                        0,
                        new Vector2Int(mergeX, currentY),
                        Vector2Int.zero,
                        Vector2.zero
                    );
                    allComponents.Add(merge);
                    mergeCounter++;

                    XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Created merge {mergeId} at position {merge.gridPosition}", sendToXRDebugLogViewer, sendToDebugLog);

                    // Update current X position (add +1 for spacing as per documentation)
                    currentX = mergeX + 1;
                }
                else
                {
                    // Handle series component
                    if (componentDict.ContainsKey(token))
                    {
                        var originalComp = componentDict[token];
                        var seriesComp = new Component
                        (
                            originalComp.id,
                            originalComp.type,
                            originalComp.Value,
                            new Vector2Int(currentX, currentY),
                            Vector2Int.zero,
                            Vector2.zero
                        );
                        allComponents.Add(seriesComp);
                        XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Placed series component {token} at position {seriesComp.gridPosition}", sendToXRDebugLogViewer, sendToDebugLog);
                        currentX += 1;
                    }
                    else
                    {
                        XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] WARNING: Component {token} not found in original components", sendToXRDebugLogViewer, sendToDebugLog);
                    }
                }
            }

            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Completed processing. Created {allComponents.Count} total components", sendToXRDebugLogViewer, sendToDebugLog);
            return allComponents;
        }

        /// <summary>
        /// Creates a wire with basic properties
        /// </summary>
        private Wire CreateWire(Vector2Int start, Vector2Int end, int wireId)
        {
            return new Wire
            {
                id = $"W{wireId:D2}",
                fromGrid = start,
                toGrid = end,
                fromASCII = Vector2Int.zero, // ASCII position placeholder
                toASCII = Vector2Int.zero,   // ASCII position placeholder
                fromRect = Vector2.zero,
                toRect = Vector2.zero,
                isHorizontal = start.y == end.y
            };
        }

        /// <summary>
        /// Checks if wire has valid distance between fromGrid and toGrid
        /// </summary>
        private bool IsValidWire(Wire wire)
        {
            var fromX = wire.fromGrid.x;
            var fromY = wire.fromGrid.y;
            var toX = wire.toGrid.x;
            var toY = wire.toGrid.y;

            // Calculate Manhattan distance
            var distance = Math.Abs(toX - fromX) + Math.Abs(toY - fromY);
            return distance >= 1;
        }

        /// <summary>
        /// Removes duplicate wires (same fromGrid and toGrid)
        /// </summary>
        private List<Wire> RemoveDuplicateWires(List<Wire> wires)
        {
            var seen = new HashSet<(Vector2Int, Vector2Int)>();
            var uniqueWires = new List<Wire>();

            foreach (var wire in wires)
            {
                var key = (wire.fromGrid, wire.toGrid);
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    uniqueWires.Add(wire);
                }
            }

            return uniqueWires;
        }

        /// <summary>
        /// Generates wires between components based on their positions and types
        /// Fixed to prevent cross-branch connections
        /// </summary>
        private List<Wire> GenerateWires(List<Component> components)
        {
            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Starting wire generation for {components.Count} components", sendToXRDebugLogViewer, sendToDebugLog);

            var wires = new List<Wire>();
            var wireId = 1;

            // Separate components by type
            var regularComponents = components.Where(c => c.type != "fork" && c.type != "merge").OrderBy(c => c.gridPosition.x).ThenBy(c => c.gridPosition.y).ToList();
            var forks = components.Where(c => c.type == "fork").ToList();
            var merges = components.Where(c => c.type == "merge").ToList();

            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Found {regularComponents.Count} regular components, {forks.Count} forks, {merges.Count} merges", sendToXRDebugLogViewer, sendToDebugLog);

            // 1. Connect regular components in series within the same branch (same Y coordinate)
            for (int i = 0; i < regularComponents.Count; i++)
            {
                var currentComp = regularComponents[i];

                // Find the next component in the same branch (same Y coordinate) and consecutive X position
                var nextInBranch = regularComponents
                    .Where(c => c.gridPosition.y == currentComp.gridPosition.y && // Same branch (Y coordinate)
                            c.gridPosition.x == currentComp.gridPosition.x + 1) // Next X position
                    .FirstOrDefault();

                if (nextInBranch != null)
                {
                    // Create horizontal wire within the same branch
                    wires.Add(CreateWire(currentComp.gridPosition, nextInBranch.gridPosition, wireId++));
                    XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Created series wire from {currentComp.id} to {nextInBranch.id}", sendToXRDebugLogViewer, sendToDebugLog);
                }
            }

            // 2. Connect components to forks
            foreach (var fork in forks)
            {
                // Find component immediately before this fork, preferring same Y position
                var prevComp = regularComponents
                    .Where(c => c.gridPosition.x < fork.gridPosition.x)
                    .OrderByDescending(c => c.gridPosition.x)
                    .ThenBy(c => c.gridPosition.y == fork.gridPosition.y ? 0 : 1) // Prioritize same Y position
                    .FirstOrDefault();

                if (prevComp != null)
                {
                    // If Y positions are different, create a vertical wire first
                    if (prevComp.gridPosition.y != fork.gridPosition.y)
                    {
                        // Create vertical wire to align Y positions
                        wires.Add(CreateWire(
                            prevComp.gridPosition,
                            new Vector2Int(prevComp.gridPosition.x, fork.gridPosition.y),
                            wireId++
                        ));
                        // Then create horizontal wire to fork
                        wires.Add(CreateWire(
                            new Vector2Int(prevComp.gridPosition.x, fork.gridPosition.y),
                            fork.gridPosition,
                            wireId++
                        ));
                    }
                    else
                    {
                        // Direct horizontal connection if Y positions match
                        wires.Add(CreateWire(prevComp.gridPosition, fork.gridPosition, wireId++));
                    }
                }

                // Connect fork to components in parallel branches
                var branchComponents = regularComponents.Where(c => c.gridPosition.x == fork.gridPosition.x + 1).ToList();
                foreach (var branchComp in branchComponents)
                {
                    // Vertical wire from fork to branch level
                    if (fork.gridPosition.y != branchComp.gridPosition.y)
                    {
                        wires.Add(CreateWire(fork.gridPosition, new Vector2Int(fork.gridPosition.x, branchComp.gridPosition.y), wireId++));
                    }
                    // Horizontal wire to component
                    wires.Add(CreateWire(new Vector2Int(fork.gridPosition.x, branchComp.gridPosition.y), branchComp.gridPosition, wireId++));
                }
            }

            // 3. Connect components to merges
            foreach (var merge in merges)
            {
                // Extract fork number from merge ID (e.g., "M02" -> 2)
                int mergeNumber = int.Parse(merge.id.Substring(1));
                string forkId = $"F{mergeNumber:D2}";

                // Find the corresponding fork
                var fork = forks.FirstOrDefault(f => f.id == forkId);
                if (fork == null)
                {
                    XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] WARNING: No matching fork found for merge {merge.id}", sendToXRDebugLogViewer, sendToDebugLog);
                    continue;
                }

                // Get all components between fork and merge
                var branchComponents = regularComponents
                    .Where(c => c.gridPosition.x > fork.gridPosition.x && c.gridPosition.x < merge.gridPosition.x)
                    .ToList();

                // Group components by Y position and get the rightmost component for each Y
                var finalBranchComponents = branchComponents
                    .GroupBy(c => c.gridPosition.y)
                    .Select(g => g.OrderByDescending(c => c.gridPosition.x).First())
                    .ToList();

                XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Found {finalBranchComponents.Count} final branch components for merge {merge.id}", sendToXRDebugLogViewer, sendToDebugLog);

                foreach (var branchComp in finalBranchComponents)
                {
                    // Horizontal wire from component
                    wires.Add(CreateWire(branchComp.gridPosition, new Vector2Int(merge.gridPosition.x, branchComp.gridPosition.y), wireId++));
                    // Vertical wire to merge level
                    if (branchComp.gridPosition.y != merge.gridPosition.y)
                    {
                        wires.Add(CreateWire(new Vector2Int(merge.gridPosition.x, branchComp.gridPosition.y), merge.gridPosition, wireId++));
                    }
                }

                // Connect merge to next component, preferring same Y position
                var nextComp = regularComponents
                    .Where(c => c.gridPosition.x > merge.gridPosition.x)
                    .OrderBy(c => c.gridPosition.x)
                    .ThenBy(c => c.gridPosition.y == merge.gridPosition.y ? 0 : 1) // Prioritize same Y position
                    .FirstOrDefault();

                if (nextComp != null)
                {
                    // If Y positions are different, create horizontal wire first
                    if (nextComp.gridPosition.y != merge.gridPosition.y)
                    {
                        // Create horizontal wire to align X positions
                        wires.Add(CreateWire(
                            merge.gridPosition,
                            new Vector2Int(nextComp.gridPosition.x, merge.gridPosition.y),
                            wireId++
                        ));
                        // Then create vertical wire to component
                        wires.Add(CreateWire(
                            new Vector2Int(nextComp.gridPosition.x, merge.gridPosition.y),
                            nextComp.gridPosition,
                            wireId++
                        ));
                    }
                    else
                    {
                        // Direct horizontal connection if Y positions match
                        wires.Add(CreateWire(merge.gridPosition, nextComp.gridPosition, wireId++));
                    }
                }
            }

            // Filter out invalid wires (distance < 1)
            wires = wires.Where(IsValidWire).ToList();
            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Filtered to {wires.Count} valid wires", sendToXRDebugLogViewer, sendToDebugLog);

            // Remove duplicate wires
            wires = RemoveDuplicateWires(wires);
            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Removed duplicates, current wire count: {wires.Count}", sendToXRDebugLogViewer, sendToDebugLog);

            // Calculate metadata for wires
            foreach (var wire in wires)
            {

                // Set horizontal property
                wire.isHorizontal = wire.fromGrid.y == wire.toGrid.y;

                if (wire.isHorizontal)
                {
                    // Check if wire touches components
                    wire.startTouchesComponent = components.Any(
                        comp => comp.gridPosition.x == wire.fromGrid.x &&
                            comp.gridPosition.y == wire.fromGrid.y
                    );
                    wire.endTouchesComponent = components.Any(
                        comp => comp.gridPosition.x == wire.toGrid.x &&
                            comp.gridPosition.y == wire.toGrid.y
                    );

                    // Check if wire is part of fork (toGrid matches fork position)
                    wire.isPartOfFork = components.Any(
                        comp => comp.type == "fork" &&
                            comp.gridPosition.x == wire.toGrid.x &&
                            comp.gridPosition.y == wire.toGrid.y
                    );

                    // Check if wire is part of merge (fromGrid matches merge position)
                    wire.isPartOfMerge = components.Any(
                        comp => comp.type == "merge" &&
                            comp.gridPosition.x == wire.fromGrid.x &&
                            comp.gridPosition.y == wire.fromGrid.y
                    );
                }
            }

            // Renumber wire IDs in order
            for (int i = 0; i < wires.Count; i++)
            {
                wires[i].id = $"W{i + 1:D2}";
            }

            XRDebugLogViewer.Log($"[{nameof(CircuitGridAssigner)}] Completed wire generation with {wires.Count} wires", sendToXRDebugLogViewer, sendToDebugLog);
            return wires;
        }
    }
}