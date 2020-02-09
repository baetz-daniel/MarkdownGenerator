using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MarkdownGenerator
{
    public enum MemberType
    {
        Field    = 'F',
        Property = 'P',
        Type     = 'T',
        Event    = 'E',
        Method   = 'M',
        None     = 0
    }

    public class XmlDocumentComment
    {
        public MemberType                 MemberType { get; set; }
        public string                     ClassName  { get; set; }
        public string                     MemberName { get; set; }
        public string                     Summary    { get; set; }
        public string                     Remarks    { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
        public string                     Returns    { get; set; }

        public override string ToString()
        {
            return MemberType + ":" + ClassName + "." + MemberName;
        }
    }

    public static class VSDocParser
    {
        public static XmlDocumentComment[] ParseXmlComment(XDocument xDocument)
        {
            return ParseXmlComment(xDocument, null);
        }

        // cheap, quick hack parser:)
        internal static XmlDocumentComment[] ParseXmlComment(XDocument xDocument, string namespaceMatch)
        {
            string assemblyName = xDocument.Descendants("assembly").First().Elements("name").First().Value;

            return xDocument.Descendants("member")
                            .Select(
                                x =>
                                {
                                    Match match = Regex.Match(
                                        x.Attribute("name").Value, @"(.):(.+)\.([^.()]+)?(\(.+\)|$)");
                                    if (!match.Groups[1].Success)
                                    {
                                        return null;
                                    }

                                    MemberType memberType = (MemberType)match.Groups[1].Value[0];
                                    if (memberType == MemberType.None)
                                    {
                                        return null;
                                    }

                                    string summary = ((string)x.Element("summary")
                                                   ?? x.Element("inheritdoc")?.Name.LocalName ?? string.Empty).Trim();
                                    summary = Regex.Replace(summary, @"<para\s*/>", "<br />");
                                    summary = Regex.Replace(
                                        summary, @"\r?\n", "<br />");
                                    summary = Regex.Replace(
                                        summary, @"\t|\\t", "&nbsp;&nbsp;&nbsp;&nbsp;");
                                    summary = Regex.Replace(
                                        summary, @"<see cref=""\w:([^\""]*)""\s*\/>",
                                        m => ResolveSeeElement(m, assemblyName));
                                    summary = Regex.Replace(
                                        summary, @"<(type)*paramref name=""([^\""]*)""\s*\/>",
                                        e => $"`{e.Groups[1].Value}`");

                                    string remarks = Regex.Replace(
                                        ((string)x.Element("remarks") ?? "").Trim(), @"<para\s*/>", "<br />");
                                    remarks = Regex.Replace(
                                        remarks, @"\r?\n", "<br />");
                                    remarks = Regex.Replace(
                                        remarks, @"\t|\\t", "&nbsp;&nbsp;&nbsp;&nbsp;");
                                    remarks = Regex.Replace(
                                        remarks, @"<see cref=""\w:([^\""]*)""\s*\/>",
                                        m => ResolveSeeElement(m, assemblyName));

                                    return new XmlDocumentComment
                                    {
                                        MemberType = memberType,
                                        ClassName = memberType == MemberType.Type
                                            ? match.Groups[2].Value + "." + match.Groups[3].Value
                                            : match.Groups[2].Value,
                                        MemberName = match.Groups[3].Value,
                                        Summary    = summary.Trim(),
                                        Remarks    = remarks.Trim(),
                                        Parameters = x.Elements("param")
                                                      .Select(
                                                          e => Tuple.Create(
                                                              e.Attribute("name").Value, e))
                                                      .Distinct(
                                                          new Item1EqualityComparer<string,
                                                              XElement>())
                                                      .ToDictionary(
                                                          e => e.Item1, e => e.Item2.Value),
                                        Returns = ((string)x.Element("returns") ?? "").Trim()
                                    };
                                })
                            .Where(x => x != null)
                            .ToArray();
        }

        private static string ResolveSeeElement(Match m, string ns)
        {
            string typeName = m.Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(ns))
            {
                if (typeName.StartsWith(ns))
                {
                    return
                        $"<a href=\"{Regex.Replace(typeName, "\\.(?:.(?!\\.))+$", me => me.Groups[0].Value.Replace(".", "#").ToLower())}\">{typeName}</a>";
                }
            }
            return $"`{typeName}`";
        }

        private class Item1EqualityComparer<T1, T2> : EqualityComparer<Tuple<T1, T2>>
        {
            public override bool Equals(Tuple<T1, T2> x, Tuple<T1, T2> y)
            {
                return x.Item1.Equals(y.Item1);
            }

            public override int GetHashCode(Tuple<T1, T2> obj)
            {
                return obj.Item1.GetHashCode();
            }
        }
    }
}