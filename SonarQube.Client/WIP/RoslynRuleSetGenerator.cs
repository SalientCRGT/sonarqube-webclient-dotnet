using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SonarQube.Client.Models;

namespace SonarQube.Client.WIP
{
    public enum RuleAction
    {
        None,
        Hidden,
        Info,
        Warning,
        Error,
    }

    public class RoslynRuleSetGenerator
    {
        private const string SONARANALYZER_PARTIAL_REPO_KEY = "sonaranalyzer-{0}";
        private const string ROSLYN_REPOSITORY_PREFIX = "roslyn.";

        private readonly IDictionary<string, string> sonarProperties;
        private readonly string inactiveRuleActionText = GetActionText(RuleAction.None);

        private string activeRuleActionText = GetActionText(RuleAction.Warning);
        private RuleAction activeRuleAction = RuleAction.Warning;

        public RoslynRuleSetGenerator(IDictionary<string, string> sonarProperties)
        {
            this.sonarProperties = sonarProperties ?? throw new ArgumentNullException(nameof(sonarProperties));
        }

        public RuleAction ActiveRuleAction
        {
            get
            {
                return activeRuleAction;
            }
            set
            {
                activeRuleAction = value;
                activeRuleActionText = GetActionText(value);
            }
        }

        /// <summary>
        /// Generates a RuleSet that is serializable (XML).
        /// The ruleset can be empty if there are no active rules belonging to the repo keys "vbnet", "csharpsquid" or "roslyn.*".
        /// </summary>
        /// <exception cref="AnalysisException">if required properties that should be associated with the repo key are missing.</exception>
        public RuleSet Generate(string language, IEnumerable<SonarQubeRule> activeRules, IEnumerable<SonarQubeRule> inactiveRules)
        {
            if (activeRules == null)
            {
                throw new ArgumentNullException(nameof(activeRules));
            }
            if (inactiveRules == null)
            {
                throw new ArgumentNullException(nameof(inactiveRules));
            }
            if (language == null)
            {
                throw new ArgumentNullException(nameof(language));
            }

            var rulesElements = activeRules.Concat(inactiveRules)
                .GroupBy(
                    rule => GetPartialRepoKey(rule, language),
                    rule => rule)
                .Where(IsSupportedRuleRepo)
                .Select(CreateRulesElement);

            var ruleSet = new RuleSet
            {
                Name = "Rules for SonarQube",
                Description = "This rule set was automatically generated from SonarQube",
                ToolsVersion = "14.0"
            };

            ruleSet.Rules.AddRange(rulesElements);

            return ruleSet;
        }

        private static bool IsSupportedRuleRepo(IGrouping<string, SonarQubeRule> analyzerRules)
        {
            var partialRepoKey = analyzerRules.Key;
            return !string.IsNullOrEmpty(partialRepoKey);
        }

        private Rules CreateRulesElement(IGrouping<string, SonarQubeRule> analyzerRules)
        {
            var partialRepoKey = analyzerRules.Key;
            return new Rules
            {
                AnalyzerId = GetRequiredPropertyValue($"{partialRepoKey}.analyzerId"),
                RuleNamespace = GetRequiredPropertyValue($"{partialRepoKey}.ruleNamespace"),
                RuleList = analyzerRules.Select(CreateRuleElement).ToList()
            };
        }

        private Rule CreateRuleElement(SonarQubeRule sonarRule) =>
            new Rule(sonarRule.Key, sonarRule.IsActive ? activeRuleActionText : inactiveRuleActionText);

        private static string GetActionText(RuleAction ruleAction)
        {
            switch (ruleAction)
            {
                case RuleAction.None:
                    return "None";
                case RuleAction.Info:
                    return "Info";
                case RuleAction.Warning:
                    return "Warning";
                case RuleAction.Error:
                    return "Error";
                case RuleAction.Hidden:
                    return "Hidden";
                default:
                    throw new NotSupportedException($"{ruleAction} is not a supported RuleAction.");
            }
        }

        private static string GetPartialRepoKey(SonarQubeRule rule, string language)
        {
            if (rule.RepositoryKey.StartsWith(ROSLYN_REPOSITORY_PREFIX))
            {
                return rule.RepositoryKey.Substring(ROSLYN_REPOSITORY_PREFIX.Length);
            }
            else if ("csharpsquid".Equals(rule.RepositoryKey) || "vbnet".Equals(rule.RepositoryKey))
            {
                return string.Format(SONARANALYZER_PARTIAL_REPO_KEY, language);
            }
            else
            {
                return null;
            }
        }

        private string GetRequiredPropertyValue(string propertyKey)
        {
            if (!this.sonarProperties.TryGetValue(propertyKey, out var propertyValue))
            {
                var message = $"Property does not exist: {propertyKey}. This property should be set by the plugin in SonarQube.";

                if (propertyKey.StartsWith(string.Format(SONARANALYZER_PARTIAL_REPO_KEY, "vbnet")))
                {
                    message = message + " Possible cause: this Scanner is not compatible with SonarVB 2.X. If necessary, upgrade SonarVB latest in SonarQube.";
                }

                // duncanp                throw new AnalysisException(message);
                throw new InvalidProgramException(message);
            }

            return propertyValue;
        }
    }
}
