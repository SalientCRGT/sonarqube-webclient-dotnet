using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SonarQube.Client.WIP
{
    public class RuleSet
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string Description { get; set; }

        [XmlAttribute]
        public string ToolsVersion { get; set; }

        [XmlElement(ElementName = "Include")]
        public List<Include> Includes { get; set; }

        [XmlElement]
        public List<Rules> Rules { get; set; } = new List<Rules>();

        public void Save(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            Serializer.SaveModel(this, fileName);
        }

        public static RuleSet Load(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            return Serializer.LoadModel<RuleSet>(fileName);
        }
    }


    public class Include
    {
        [XmlAttribute]
        public string Path { get; set; }

        [XmlAttribute]
        public string Action { get; set; }
    }

    public class Rule
    {
        public Rule()
        {
        }

        public Rule(string id, string action)
        {
            Id = id;
            Action = action;
        }

        [XmlAttribute]
        public string Id { get; set; }

        [XmlAttribute]
        public string Action { get; set; }
    }

    public class Rules
    {
        [XmlAttribute]
        public string AnalyzerId { get; set; }

        [XmlAttribute]
        public string RuleNamespace { get; set; }

        [XmlElement("Rule")]
        public List<Rule> RuleList { get; set; }
    }
}
