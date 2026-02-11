using Horstmeier.NugetLicenses.Configuration;
using Horstmeier.NugetLicenses.Models;

namespace Horstmeier.NugetLicenses.Services;

public class LicenseValidator : ILicenseValidator
{
    private readonly HashSet<string> _permittedLicenses;
    private readonly HashSet<string> _exemptPackages;

    public LicenseValidator(LicenseCheckSettings settings)
    {
        _permittedLicenses = new HashSet<string>(settings.PermittedLicenses, StringComparer.OrdinalIgnoreCase);
        _exemptPackages = new HashSet<string>(settings.ExemptPackages, StringComparer.OrdinalIgnoreCase);
    }

    public LicenseValidationResult Validate(IReadOnlyList<LicenseInfo> licenses)
    {
        var violations = new List<LicenseViolation>();

        foreach (var license in licenses)
        {
            if (_exemptPackages.Contains(license.PackageId))
                continue;

            if (string.IsNullOrWhiteSpace(license.LicenseExpression))
            {
                violations.Add(new LicenseViolation(
                    license.PackageId,
                    license.Version,
                    license.LicenseExpression,
                    license.LicenseUrl,
                    "No SPDX license expression found"));
                continue;
            }

            if (!IsExpressionPermitted(license.LicenseExpression))
            {
                violations.Add(new LicenseViolation(
                    license.PackageId,
                    license.Version,
                    license.LicenseExpression,
                    license.LicenseUrl,
                    $"License '{license.LicenseExpression}' is not permitted"));
            }
        }

        return new LicenseValidationResult(violations, licenses.Count, licenses.Count - violations.Count);
    }

    internal bool IsExpressionPermitted(string expression)
    {
        expression = StripOuterParentheses(expression);

        // Handle OR: at least one branch must be permitted
        var orParts = SplitTopLevel(expression, "OR");
        if (orParts.Count > 1)
            return orParts.Any(IsExpressionPermitted);

        // Handle AND: all parts must be permitted
        var andParts = SplitTopLevel(expression, "AND");
        if (andParts.Count > 1)
            return andParts.All(IsExpressionPermitted);

        // Single license identifier
        return _permittedLicenses.Contains(expression.Trim());
    }

    internal static List<string> SplitTopLevel(string expression, string op)
    {
        var parts = new List<string>();
        var depth = 0;
        var tokens = expression.Split(' ');
        var segment = new List<string>();

        foreach (var token in tokens)
        {
            if (token == "(")
            {
                depth++;
                segment.Add(token);
            }
            else if (token == ")")
            {
                depth--;
                segment.Add(token);
            }
            else if (depth == 0 && string.Equals(token, op, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(string.Join(' ', segment).Trim());
                segment.Clear();
            }
            else
            {
                segment.Add(token);
            }
        }

        if (segment.Count > 0)
            parts.Add(string.Join(' ', segment).Trim());

        // Strip outer parentheses from parts
        for (var i = 0; i < parts.Count; i++)
        {
            parts[i] = StripOuterParentheses(parts[i]);
        }

        return parts;
    }

    private static string StripOuterParentheses(string s)
    {
        s = s.Trim();
        while (s.StartsWith('(') && s.EndsWith(')'))
        {
            // Make sure the parentheses actually match
            var depth = 0;
            var matched = true;
            for (var i = 0; i < s.Length - 1; i++)
            {
                if (s[i] == '(') depth++;
                else if (s[i] == ')') depth--;
                if (depth == 0)
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
                s = s[1..^1].Trim();
            else
                break;
        }

        return s;
    }
}
