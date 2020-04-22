using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using SonarQube.Client.Api;
using SonarQube.Client.Messages;
using SonarQube.Client.Models;

namespace SonarQube.Client.WIP
{
    public class RoslynExporterAdapter
    {
        private ISonarQubeService service;

        public void Initialize(ISonarQubeService service)
        {
            this.service = service;
        }
        internal async Task<RoslynExportProfileResponse> GetResponse(IGetRoslynExportProfileRequest request, CancellationToken cancellationToken)
        {
            var qpKey = request.QualityProfileName; // TODO - fix

            var properties = await service.GetAllPropertiesAsync(null, CancellationToken.None);

            var serverProperties = properties.ToDictionary(x => x.Key, x => x.Value);

            var rules = await service.GetAllRulesAsync(request.QualityProfileName, CancellationToken.None);
            var activeRules = await service.GetRulesAsync(true, qpKey, CancellationToken.None);
            var inactiveRules = await service.GetRulesAsync(false, qpKey, CancellationToken.None);

            var rulesetGenerator = new RoslynRuleSetGenerator(serverProperties);
            var ruleSet = rulesetGenerator.Generate(request.LanguageKey, activeRules, inactiveRules);
            var data = Serializer.ToString(ruleSet);
            var xml = new XmlDocument();
            xml.LoadXml(data);

            var nuGetPackages = FetchAnalyzerPlugins(request.LanguageKey, activeRules, serverProperties);

            var response = new RoslynExportProfileResponse
            {
                Configuration = new ConfigurationResponse { RuleSet = xml.DocumentElement },
                Deployment = new DeploymentResponse { NuGetPackages = nuGetPackages }
            };

            return response;
        }


        #region Copied from S4MSB

        private const string SONARANALYZER_PARTIAL_REPO_KEY_PREFIX = "sonaranalyzer-";
        private const string SONARANALYZER_PARTIAL_REPO_KEY = SONARANALYZER_PARTIAL_REPO_KEY_PREFIX + "{0}";
        private const string ROSLYN_REPOSITORY_PREFIX = "roslyn.";

        public const string CSharpLanguage = "cs";
        public const string CSharpPluginKey = "csharp";
        public const string CSharpRepositoryKey = "csharp";

        public const string VBNetLanguage = "vbnet";
        public const string VBNetPluginKey = "vbnet";
        public const string VBNetRepositoryKey = "vbnet";

        private List<NuGetPackageInfoResponse> FetchAnalyzerPlugins(string language, IEnumerable<SonarQubeRule> activeRules,
            IDictionary<string, string> sonarProperties)
        {
            var partialRepoKeys = ActiveRulesPartialRepoKeys(activeRules);
            var plugins = new List<NuGetPackageInfoResponse>();

            foreach (var partialRepoKey in partialRepoKeys)
            {
                if (!sonarProperties.TryGetValue($"{partialRepoKey}.analyzerId", out var analyzerId) ||
                    !sonarProperties.TryGetValue($"{partialRepoKey}.pluginVersion", out var pluginVersion))

                //if (!sonarProperties.TryGetValue($"{partialRepoKey}.pluginKey", out var pluginkey) ||
                //    !sonarProperties.TryGetValue($"{partialRepoKey}.pluginVersion", out var pluginVersion) ||
                //    !sonarProperties.TryGetValue($"{partialRepoKey}.staticResourceName", out var staticResourceName))
                {
                    //if (!partialRepoKey.StartsWith(SONARANALYZER_PARTIAL_REPO_KEY_PREFIX))
                    //{
                    //    this.logger.LogInfo(Resources.RAP_NoAssembliesForRepo, partialRepoKey, language);
                    //}
                    continue;
                }

                plugins.Add(new NuGetPackageInfoResponse { Id = analyzerId, Version = pluginVersion });
            }

            return plugins;
            //if (plugins.Count == 0)
            //{
            //    this.logger.LogInfo(Resources.RAP_NoAnalyzerPluginsSpecified, language);
            //    return Enumerable.Empty<AnalyzerPlugin>();
            //}
            //else
            //{
            //    this.logger.LogInfo(Resources.RAP_ProvisioningAnalyzerAssemblies, language);
            //    return this.analyzerInstaller.InstallAssemblies(plugins);
            //}
        }

        private static ICollection<string> ActiveRulesPartialRepoKeys(IEnumerable<SonarQubeRule> rules)
        {
            var partialRepoKeys = new HashSet<string>
            {
                // Always add SonarC# and SonarVB to have at least tokens...
                string.Format(SONARANALYZER_PARTIAL_REPO_KEY, "cs"),
                string.Format(SONARANALYZER_PARTIAL_REPO_KEY, "vbnet")
            };

            // Add the Roslyn SDK rules' partial repo keys, if any
            partialRepoKeys.UnionWith(
                rules
                    .Where(rule => rule.RepositoryKey.StartsWith(ROSLYN_REPOSITORY_PREFIX))
                    .Select(rule => rule.RepositoryKey.Substring(ROSLYN_REPOSITORY_PREFIX.Length)));

            return partialRepoKeys;
        }

        #endregion

        public void BuildRuleset()
        {

        }

        // COPIED
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
    }
}
