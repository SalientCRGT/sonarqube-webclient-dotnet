﻿/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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
using SonarQube.Client.Models;

// Logically equivalent to the SonarScanner for MSBuild class "RoslynSonarLint"
// See https://github.com/SonarSource/sonar-scanner-msbuild/blob/9ccfdb648a0411014b29c7aee8e347aeab87ea71/src/SonarScanner.MSBuild.PreProcessor/Roslyn/RoslynSonarLint.cs#L29

namespace SonarQube.Client.RoslynExporterAdapter
{
    internal static class SonarLintConfigGenerator
    {
        private const string CSharpRepoKey = "csharpsquid";
        private const string VBRepoKey = "vbnet";

        public static SonarLintConfiguration Generate(IEnumerable<SonarQubeRule> rules, IDictionary<string, string> sonarProperties,
            string language)
        {
            if (rules == null) { throw new ArgumentNullException(nameof(rules)); }
            if (sonarProperties == null) { throw new ArgumentNullException(nameof(sonarProperties)); }
            if (language == null) { throw new ArgumentNullException(nameof(language)); }

            var slvsSettings = GetSettingsForLanguage(language, sonarProperties);

            // We don't expect third-party rules to look for their settings in SonarLint.xml so we
            // only fetch parameters for SonarC#/VB rules.
            var sonarRepoKey = GetSonarRepoKey(language);
            var slvsRules = GetRulesForRepo(sonarRepoKey, rules);

            return new SonarLintConfiguration()
            {
                Settings = slvsSettings,
                Rules = slvsRules
            };
        }

        private static string GetSonarRepoKey(string language)
        {
            if (language == SonarQubeLanguage.CSharp.Key)
            {
                return CSharpRepoKey;
            }

            if (language == SonarQubeLanguage.VbNet.Key)
            {
                return VBRepoKey;
            }

            throw new ArgumentOutOfRangeException(nameof(language));
        }

        private static List<SonarLintKeyValuePair> GetSettingsForLanguage(string language, IDictionary<string, string> sonarProperties) =>
            sonarProperties.Where(kvp => IsSettingForLanguage(language, kvp.Key))
                .Select(ToSonarLintKeyValue)
                .ToList();

        private static bool IsSettingForLanguage (string language, string propertyKey)
        {
            var prefix = $"sonar.{language}.";

            return propertyKey.StartsWith(prefix) &&
                propertyKey.Length > prefix.Length;
        }

        private static List<SonarLintRule> GetRulesForRepo(string sonarRepoKey, IEnumerable<SonarQubeRule> sqRules) =>
            sqRules.Where(ar => sonarRepoKey.Equals(ar.RepositoryKey))
                .Select(ToSonarLintRule)
                .ToList();

        private static SonarLintRule ToSonarLintRule(SonarQubeRule sqRule)
        {
            List<SonarLintKeyValuePair> slvsParameters = null;
            if (sqRule.Parameters != null && sqRule.Parameters.Count > 0 )
            {
                slvsParameters = sqRule.Parameters.Select(ToSonarLintKeyValue).ToList();
            }

            return new SonarLintRule()
            {
                Key = sqRule.Key,
                Parameters = slvsParameters
            };
        }

        private static SonarLintKeyValuePair ToSonarLintKeyValue(KeyValuePair<string, string> setting) =>
            new SonarLintKeyValuePair() { Key = setting.Key, Value = setting.Value };
    }
}
