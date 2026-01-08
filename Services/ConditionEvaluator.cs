using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PLCDataLogger.Services
{
    /// <summary>
    /// Evaluates simple conditional expressions for log filtering
    /// Supports: Value, Reg_{FieldName}, comparison operators (==, !=, >, <, >=, <=), AND, OR
    /// Example: "Value == '1' AND Reg_Temperature > 80"
    /// </summary>
    public class ConditionEvaluator
    {
        /// <summary>
        /// Evaluates a condition expression
        /// </summary>
        /// <param name="condition">The condition string to evaluate</param>
        /// <param name="currentValue">The current value of this register</param>
        /// <param name="allRegisterValues">Dictionary of all register values keyed by FieldName</param>
        /// <returns>True if condition passes, false otherwise</returns>
        public static bool Evaluate(string condition, string currentValue, Dictionary<string, string> allRegisterValues)
        {
            // Empty condition means always true
            if (string.IsNullOrWhiteSpace(condition))
                return true;

            try
            {
                // Split by OR first (lowest precedence)
                var orParts = SplitByOperator(condition, "OR");

                // If any OR part is true, return true
                foreach (var orPart in orParts)
                {
                    // Split by AND (higher precedence)
                    var andParts = SplitByOperator(orPart, "AND");

                    // Check if all AND parts are true
                    bool allAndPartsTrue = true;
                    foreach (var andPart in andParts)
                    {
                        if (!EvaluateSingleCondition(andPart.Trim(), currentValue, allRegisterValues))
                        {
                            allAndPartsTrue = false;
                            break;
                        }
                    }

                    // If all AND parts are true, the OR part is true
                    if (allAndPartsTrue)
                        return true;
                }

                // No OR part was true
                return false;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogWarning($"Condition evaluation error: {ex.Message}. Condition: '{condition}'");
                return false; // Fail safe - don't log if condition evaluation fails
            }
        }

        /// <summary>
        /// Splits a string by an operator while respecting quoted strings
        /// </summary>
        private static List<string> SplitByOperator(string expression, string op)
        {
            var parts = new List<string>();
            var current = "";
            bool inQuotes = false;

            for (int i = 0; i < expression.Length; i++)
            {
                char c = expression[i];

                if (c == '\'' || c == '"')
                {
                    inQuotes = !inQuotes;
                    current += c;
                }
                else if (!inQuotes && i + op.Length <= expression.Length)
                {
                    // Check if we're at the operator
                    string substr = expression.Substring(i, op.Length);
                    if (substr.Equals(op, StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if it's a whole word (surrounded by whitespace or boundaries)
                        bool isWholeWord = (i == 0 || char.IsWhiteSpace(expression[i - 1])) &&
                                          (i + op.Length >= expression.Length || char.IsWhiteSpace(expression[i + op.Length]));

                        if (isWholeWord)
                        {
                            parts.Add(current);
                            current = "";
                            i += op.Length - 1; // Skip the operator (-1 because loop will increment)
                            continue;
                        }
                    }
                }

                if (!(!inQuotes && i + op.Length <= expression.Length &&
                     expression.Substring(i, op.Length).Equals(op, StringComparison.OrdinalIgnoreCase)))
                {
                    current += c;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
                parts.Add(current);

            // If no operator found, return original expression
            if (parts.Count == 0)
                parts.Add(expression);

            return parts;
        }

        /// <summary>
        /// Evaluates a single comparison expression (e.g., "Value > 80" or "Reg_Temperature == '25'")
        /// </summary>
        private static bool EvaluateSingleCondition(string condition, string currentValue, Dictionary<string, string> allRegisterValues)
        {
            // Match pattern: variable operator value
            // Operators: ==, !=, >=, <=, >, <
            var pattern = @"^\s*(\w+)\s*(==|!=|>=|<=|>|<)\s*(.+?)\s*$";
            var match = Regex.Match(condition, pattern);

            if (!match.Success)
            {
                LoggingService.Instance.LogWarning($"Invalid condition syntax: '{condition}'");
                return false;
            }

            string variable = match.Groups[1].Value;
            string op = match.Groups[2].Value;
            string rightValue = match.Groups[3].Value.Trim();

            // Remove quotes from right value if present
            if ((rightValue.StartsWith("'") && rightValue.EndsWith("'")) ||
                (rightValue.StartsWith("\"") && rightValue.EndsWith("\"")))
            {
                rightValue = rightValue.Substring(1, rightValue.Length - 2);
            }

            // Get the left value based on variable name
            string leftValue;

            if (variable.Equals("Value", StringComparison.OrdinalIgnoreCase))
            {
                leftValue = currentValue;
            }
            else if (variable.StartsWith("Reg_", StringComparison.OrdinalIgnoreCase))
            {
                // Extract field name (e.g., "Reg_Temperature" -> "Temperature")
                string fieldName = variable.Substring(4);

                if (allRegisterValues != null && allRegisterValues.TryGetValue(fieldName, out string regValue))
                {
                    leftValue = regValue;
                }
                else
                {
                    LoggingService.Instance.LogWarning($"Register '{fieldName}' not found in condition: '{condition}'");
                    return false;
                }
            }
            else
            {
                LoggingService.Instance.LogWarning($"Unknown variable '{variable}' in condition: '{condition}'");
                return false;
            }

            // Perform comparison
            return CompareValues(leftValue, op, rightValue);
        }

        /// <summary>
        /// Compares two values using the specified operator
        /// Attempts numeric comparison first, falls back to string comparison
        /// </summary>
        private static bool CompareValues(string left, string op, string right)
        {
            // Try numeric comparison first
            if (double.TryParse(left, out double leftNum) && double.TryParse(right, out double rightNum))
            {
                return op switch
                {
                    "==" => Math.Abs(leftNum - rightNum) < 0.0001, // Floating point equality
                    "!=" => Math.Abs(leftNum - rightNum) >= 0.0001,
                    ">" => leftNum > rightNum,
                    "<" => leftNum < rightNum,
                    ">=" => leftNum >= rightNum,
                    "<=" => leftNum <= rightNum,
                    _ => false
                };
            }

            // Fall back to string comparison
            int comparison = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);

            return op switch
            {
                "==" => comparison == 0,
                "!=" => comparison != 0,
                ">" => comparison > 0,
                "<" => comparison < 0,
                ">=" => comparison >= 0,
                "<=" => comparison <= 0,
                _ => false
            };
        }
    }
}
