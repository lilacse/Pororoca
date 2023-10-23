using System.Text.RegularExpressions;
using Pororoca.Domain.Features.Entities.Pororoca;

namespace Pororoca.Domain.Features.VariableResolution;

public partial interface IPororocaVariableResolver
{
    public static readonly Regex PororocaVariableRegex = GeneratePororocaVariableRegex();

    [GeneratedRegex("\\{\\{(?<k>[\\w\\d_\\-\\.]+)\\}\\}")]
    private static partial Regex GeneratePororocaVariableRegex();

    List<PororocaVariable> Variables { get; } // collection variables
    PororocaRequestAuth? CollectionScopedAuth { get; } // collection scoped auth
    List<PororocaEnvironment> Environments { get; } // collection environments

    // Example of templated string:
    // "https://{{MyApiHost}}/api/location?city=Campinas"
    // If there is a variable with the key "MyApiHost" and with the value "www.api.com.br" (all without quotes)
    // Then this method should return:
    // "https://www.api.com.br/api/location?city=Campinas"
    // The variable resolution depends on collection and environment variables.
    // Environment variables have precedence over collection variables.
    // If the variable key is not declared or the variable is not enabled, then the raw key should be used as is.

    public PororocaRequestAuth? GetAuthForRequest(PororocaRequestAuth? reqAuth) =>
        reqAuth == PororocaRequestAuth.InheritedFromCollection ?
        CollectionScopedAuth : reqAuth;

    public IDictionary<string, string> ResolveKeyValueParams(IEnumerable<PororocaKeyValueParam>? kvParams) =>
        kvParams == null ?
        new() :
        kvParams.Where(h => h.Enabled)
                .Select(h => new KeyValuePair<string, string>(
                    ReplaceTemplates(h.Key),
                    ReplaceTemplates(h.Value)
                ))
                .DistinctBy(h => h.Key) // Avoid duplicated pairs by key
                .ToDictionary(h => h.Key, h => h.Value);

    public string ReplaceTemplates(string? strToReplaceTemplatedVariables)
    {
        if (string.IsNullOrEmpty(strToReplaceTemplatedVariables))
        {
            return strToReplaceTemplatedVariables ?? string.Empty;
        }
        else
        {
            var effectiveVars = GetEffectiveVariables();
            return PororocaVariableRegex.Replace(strToReplaceTemplatedVariables, match =>
            {
                string keyName = match.Groups["k"].Value;
                var effectiveVar = effectiveVars.FirstOrDefault(v => v.Key == keyName);
                return effectiveVar?.Value ?? match.Value;
            });
        }
    }

    internal IEnumerable<PororocaVariable> GetEffectiveVariables()
    {
        var currentEnv = Environments.FirstOrDefault(e => e.IsCurrent);
        var effectiveEnvVars = currentEnv?.Variables?.Where(sev => sev.Enabled) ?? Enumerable.Empty<PororocaVariable>();
        var effectiveColVars = Variables.Where(cv => cv.Enabled);
        var effectiveColVarsNotInEnv =
            effectiveColVars.Where(ecv => !effectiveEnvVars.Any(eev => eev.Key == ecv.Key));

        // Environment variable overrides collection variable
        return effectiveEnvVars.Concat(effectiveColVarsNotInEnv);
    }
}