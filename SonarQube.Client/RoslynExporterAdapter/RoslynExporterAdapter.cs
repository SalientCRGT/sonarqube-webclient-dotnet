/*
 * SonarQube Client
 * Copyright (C) 2016-2020 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using SonarQube.Client.Api;
using SonarQube.Client.Logging;
using SonarQube.Client.Messages;
using SonarQube.Client.Models;
using SonarQube.Client.Requests;

namespace SonarQube.Client.RoslynExporterAdapter
{
    public class RoslynExporterAdapterX : IGetRoslynExportProfileRequest
    {
        public ILogger Logger { get; set; }
        public string LanguageKey { get; set; }
        public string QualityProfileName { get; set; }
        public string OrganizationKey { get; set; }
        public string QualityProfileKey { get; set; }
        public string ProjectKey { get; set; }

        async Task<RoslynExportProfileResponse> IRequest<RoslynExportProfileResponse>.InvokeAsync(HttpClient httpClient,
            ISonarQubeService service, CancellationToken token)
        {
            var qpKey = QualityProfileKey;

            var properties = await service.GetAllPropertiesAsync(null, CancellationToken.None);
            var sonarProperties = properties.ToDictionary(x => x.Key, x => x.Value);

            var activeRules = await service.GetRulesAsync(true, qpKey, CancellationToken.None);
            var inactiveRules = await service.GetRulesAsync(false, qpKey, CancellationToken.None);

            var ruleSetXml = GetRulesetXml(activeRules, inactiveRules, sonarProperties);
            var additionalFiles = GetAdditionalFiles(LanguageKey, activeRules, sonarProperties);
            var nugetPackages = NuGetPackageInfoGenerator.GetNuGetPackageInfos(activeRules, sonarProperties);

            var response = new RoslynExportProfileResponse
            {
                Configuration = new ConfigurationResponse
                {
                    RuleSet = ruleSetXml,
                    AdditionalFiles = additionalFiles
                },
                Deployment = new DeploymentResponse { NuGetPackages = nugetPackages }
            };

            return response;
        }

        private XmlElement GetRulesetXml(IEnumerable<SonarQubeRule> activeRules, IEnumerable<SonarQubeRule> inactiveRules, Dictionary<string, string> sonarProperties)
        {
            // Generate the ruleset
            var rulesetGenerator = new RoslynRuleSetGenerator(sonarProperties);
            var ruleSet = rulesetGenerator.Generate(LanguageKey, activeRules, inactiveRules);
            var data = Serializer.ToUTF8String(ruleSet);
            var xml = new XmlDocument();
            xml.LoadXml(data);

            return xml.DocumentElement;
        }

        private List<AdditionalFileResponse> GetAdditionalFiles(string language, IList<SonarQubeRule> activeRules, Dictionary<string, string> sonarProperties)
        {
            var sonarLintConfig = SonarLintConfigGenerator.Generate(activeRules, sonarProperties, language);
            var data = Serializer.ToBase64(sonarLintConfig);

            var utf8String = Serializer.ToUTF8String(sonarLintConfig);

            var file = new AdditionalFileResponse()
            {
                FileName = "SonarLint.xmlAAA",
                Content = data
            };

            var files = new List<AdditionalFileResponse>();
            files.Add(file);
            return files;
        }
    }
}
