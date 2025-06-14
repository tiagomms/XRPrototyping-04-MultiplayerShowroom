using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using UnityEngine.Events;
using TMPro;
using Utils;

namespace CircuitProcessor
{
    /// <summary>
    /// Evaluates circuit formulas by substituting component values and calculating the result
    /// </summary>
    public class CircuitFormulaEvaluator : MonoBehaviour
    {
        // TODO: nice class that writes Formula for each lightbulb
        [SerializeField] private TextMeshProUGUI formulaText;
        [SerializeField] private TextMeshProUGUI intensityText;
        [Header("Debug")]
        [SerializeField] private bool sendToXRDebugLogViewer = true;
        [SerializeField] private bool sendToDebugLog = true;
        private CircuitData circuitData;
        private Dictionary<Component, double> componentValues = new ();
        public UnityEvent<float> OnCalculatingFormula;

        private float _intensity;
        public float Intensity => _intensity;

        public void Initialize(CircuitData circuitData)
        {
            XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Initializing with formula: {circuitData.formula}", sendToXRDebugLogViewer, sendToDebugLog);
            this.circuitData = circuitData;
            if (formulaText != null)
            {
                formulaText.text = circuitData.formula;
                XRDebugLogViewer.Log("CircuitFormulaEvaluator: Formula text updated in UI", sendToXRDebugLogViewer, sendToDebugLog);
            }
            else
            {
                XRDebugLogViewer.LogError("CircuitFormulaEvaluator: Formula text UI component is null");
            }
            RefreshComponentValues();
        }

        /// <summary>
        /// Refreshes the component values dictionary from the circuit data
        /// </summary>
        public void RefreshComponentValues()
        {
            ClearComponentValues();
            XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Refreshing component values - amount {circuitData.components.Count}", sendToXRDebugLogViewer, sendToDebugLog);
            foreach (Component component in circuitData.components)
            {
                componentValues.Add(component, (double)component.Value);
                XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Component {component.id} value set to {component.Value}", sendToXRDebugLogViewer, sendToDebugLog);
            }
            foreach (Component c in componentValues.Keys)
            {
                c.OnValueChanged.AddListener(UpdateComponentValue);
            }
            XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Refreshed component values - amount {componentValues.Keys.Count}", sendToXRDebugLogViewer, sendToDebugLog);
            EvaluateFormula();
        }

        private void ClearComponentValues()
        {
            foreach(var component in componentValues)
            {
                component.Key.OnValueChanged.RemoveListener(UpdateComponentValue);
            }
            componentValues.Clear();
            XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Cleared component values", sendToXRDebugLogViewer, sendToDebugLog);
        }

        /// <summary>
        /// Evaluates the circuit formula and returns the result
        /// </summary>
        /// <returns>The calculated value of the formula</returns>
        public float EvaluateFormula()
        {
            if (string.IsNullOrEmpty(circuitData.formula))
            {
                XRDebugLogViewer.LogError("CircuitFormulaEvaluator: Formula is null or empty");
                return 0f;
            }

            try
            {
                string substitutedFormula = SubstituteComponentValues(circuitData.formula);
                XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Substituted formula: {substitutedFormula}", sendToXRDebugLogViewer, sendToDebugLog);

                var result = EvaluateExpression(substitutedFormula);
                _intensity = (float)result;
                XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Formula evaluated to: {_intensity}", sendToXRDebugLogViewer, sendToDebugLog);
                WriteIntensityUIText();

                OnCalculatingFormula.Invoke(_intensity);
                return _intensity;
            }
            catch (Exception ex)
            {
                XRDebugLogViewer.LogError($"CircuitFormulaEvaluator: Error evaluating formula: {ex.Message}");
                WriteIntensityUIText();

                OnCalculatingFormula.Invoke(-1f);
                return -1f;
            }
        }

        private void WriteIntensityUIText()
        {
            string result = "I = ";
            if (Mathf.Approximately(_intensity, -1f))
            {
                result += $"ERROR - Please try again later";
            }
            else
            {
                result += $"{NumberFormatter.FormatWithUnit(_intensity, "A", 2)}";
            }
            XRDebugLogViewer.Log($"[{nameof(CircuitFormulaEvaluator)}] - INTENSITY VALUE: {result}");

            intensityText.text = result;
        }

        /// <summary>
        /// Updates a specific component's value and optionally recalculates the formula
        /// </summary>
        /// <param name="component">The component to update</param>
        public void UpdateComponentValue(Component component)
        {
            XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Updating Formula component {component}", sendToXRDebugLogViewer, sendToDebugLog);
            if (componentValues.ContainsKey(component))
            {
                componentValues[component] = (double)component.Value;
                EvaluateFormula();
            }
            else
            {
                XRDebugLogViewer.LogError($"CircuitFormulaEvaluator: Component {component} not found in values dictionary");
            }
        }

        /// <summary>
        /// Gets the current calculated value without recalculating
        /// </summary>
        public float GetCurrentValue()
        {
            try
            {
                return EvaluateFormula();
            }
            catch (Exception ex)
            {
                XRDebugLogViewer.LogError($"CircuitFormulaEvaluator: Error getting current value: {ex.Message}");
                return -1f;
            }
        }

        /// <summary>
        /// Substitutes component IDs in the formula with their actual values
        /// </summary>
        private string SubstituteComponentValues(string formula)
        {
            XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Substituting values in formula: {formula}", sendToXRDebugLogViewer, sendToDebugLog);
            string result = formula;

            // Sort component IDs by length (descending) to avoid partial matches
            // e.g., if we have R1 and R10, we want to replace R10 first
            var sortedIds = componentValues.Keys.OrderByDescending(c => c.id);
            XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Substituting values - number of components in circuitData: {componentValues.Keys.Count}", sendToXRDebugLogViewer, sendToDebugLog);

            foreach (var component in sortedIds)
            {
                double value = componentValues[component];
                string id = component.id;

                // Use word boundary regex to ensure we only replace complete component IDs
                string pattern = @"\b" + Regex.Escape(id) + @"\b";

                // Format double values to handle scientific notation properly
                string valueString = FormatDoubleForSubstitution(value);
                result = Regex.Replace(result, pattern, valueString);
                XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Replaced {id} with {valueString}", sendToXRDebugLogViewer, sendToDebugLog);
            }

            return result;
        }

        /// <summary>
        /// Formats a double value for substitution in formulas, handling scientific notation and range checks
        /// </summary>
        private string FormatDoubleForSubstitution(double value)
        {
            const double maxAllowed = 1e9;
            const double minAllowed = 1e-9;

            // Handle special cases
            if (double.IsNaN(value)) return "-1";
            if (double.IsInfinity(value)) throw new InvalidOperationException("Infinite value encountered.");
            if (value == 0.0) return "0";

            double abs = Math.Abs(value);
            if (abs > maxAllowed || (abs < minAllowed && abs > 0))
                throw new InvalidOperationException($"Value {value} is out of acceptable range for circuit evaluation.");

            string formatted = value.ToString("0.#############################", CultureInfo.InvariantCulture);

            if (formatted.Contains("."))
                formatted = formatted.TrimEnd('0').TrimEnd('.');

            return formatted;
        }

        /// <summary>
        /// Evaluates a mathematical expression string
        /// </summary>
        private double EvaluateExpression(string expression)
        {
            try
            {
                XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Evaluating expression: {expression}", sendToXRDebugLogViewer, sendToDebugLog);

                // Remove whitespace
                expression = expression.Replace(" ", "");

                // Handle the case where the expression is just a number
                if (double.TryParse(expression, NumberStyles.Float | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double simpleResult))
                {
                    XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Expression is a simple number: {simpleResult}", sendToXRDebugLogViewer, sendToDebugLog);
                    return simpleResult;
                }

                return EvaluateRecursive(expression);
            }
            catch (Exception ex)
            {
                XRDebugLogViewer.LogError($"CircuitFormulaEvaluator: Error evaluating expression '{expression}': {ex.Message}");
                throw new InvalidOperationException($"Error evaluating expression '{expression}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Recursively evaluates mathematical expressions with proper operator precedence
        /// </summary>
        private double EvaluateRecursive(string expression)
        {
            if (string.IsNullOrEmpty(expression))
            {
                return 0.0;
            }

            XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Recursively evaluating: {expression}", sendToXRDebugLogViewer, sendToDebugLog);

            // Handle parentheses first
            while (expression.Contains("("))
            {
                int lastOpen = expression.LastIndexOf('(');
                int firstClose = expression.IndexOf(')', lastOpen);

                if (firstClose == -1)
                {
                    XRDebugLogViewer.LogError("CircuitFormulaEvaluator: Mismatched parentheses in expression");
                    throw new InvalidOperationException("Mismatched parentheses");
                }

                string innerExpression = expression.Substring(lastOpen + 1, firstClose - lastOpen - 1);
                XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Evaluating inner expression: {innerExpression}", sendToXRDebugLogViewer, sendToDebugLog);

                double innerResult = EvaluateRecursive(innerExpression);
                string resultString = FormatDoubleForSubstitution(innerResult);

                expression = expression.Substring(0, lastOpen) +
                           resultString +
                           expression.Substring(firstClose + 1);

                XRDebugLogViewer.Log($"CircuitFormulaEvaluator: After parentheses evaluation: {expression}", sendToXRDebugLogViewer, sendToDebugLog);
            }

            // Handle addition and subtraction (lowest precedence)
            for (int i = expression.Length - 1; i >= 0; i--)
            {
                if (expression[i] == '+' || expression[i] == '-')
                {
                    // Skip if this is a unary minus at the beginning or after an operator
                    if (expression[i] == '-' && (i == 0 || "+-*/".Contains(expression[i - 1])))
                        continue;

                    string left = expression.Substring(0, i);
                    string right = expression.Substring(i + 1);

                    double leftValue = EvaluateRecursive(left);
                    double rightValue = EvaluateRecursive(right);

                    return expression[i] == '+' ? leftValue + rightValue : leftValue - rightValue;
                }
            }

            // Handle multiplication and division (higher precedence)
            for (int i = expression.Length - 1; i >= 0; i--)
            {
                if (expression[i] == '*' || expression[i] == '/')
                {
                    string left = expression.Substring(0, i);
                    string right = expression.Substring(i + 1);

                    double leftValue = EvaluateRecursive(left);
                    double rightValue = EvaluateRecursive(right);

                    if (expression[i] == '*')
                    {
                        return leftValue * rightValue;
                    }
                    else // division
                    {
                        // Handle division by zero as specified: 1/0 = max double, 1/infinity = 0
                        if (rightValue == 0.0)
                            return double.MaxValue;
                        if (double.IsInfinity(rightValue))
                            return 0.0;

                        return leftValue / rightValue;
                    }
                }
            }

            // Handle unary minus
            if (expression.StartsWith("-"))
            {
                return -EvaluateRecursive(expression.Substring(1));
            }

            // If we get here, it should be a number
            // Try parsing with different number styles to handle scientific notation
            if (double.TryParse(expression, NumberStyles.Float | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double result))
            {
                XRDebugLogViewer.Log($"CircuitFormulaEvaluator: Final number result: {result}", sendToXRDebugLogViewer, sendToDebugLog);
                return result;
            }

            XRDebugLogViewer.LogError($"CircuitFormulaEvaluator: Unable to parse expression: {expression}");
            throw new InvalidOperationException($"Unable to parse expression: {expression}");
        }

        /// <summary>
        /// Gets a formatted string representation of the formula with current values
        /// </summary>
        public string GetFormulaWithValues()
        {
            return SubstituteComponentValues(circuitData.formula);
        }

        /// <summary>
        /// Gets the original formula string
        /// </summary>
        public string GetOriginalFormula()
        {
            return circuitData.formula;
        }
    }
}