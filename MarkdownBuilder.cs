using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkdownWikiGenerator
{
    public class MarkdownBuilder
    {
        private readonly StringBuilder sb = new StringBuilder();

        public static string MarkdownCodeQuote(string code)
        {
            return "<code>" + code + "</code>";
        }

        public void Append(string text)
        {
            sb.Append(text);
        }

        public void AppendLine()
        {
            sb.AppendLine();
        }

        public void AppendLine(string text)
        {
            sb.AppendLine(text);
        }

        public void Header(int level, string text)
        {
            for (int i = 0; i < level; i++)
            {
                sb.Append("#");
            }
            sb.Append(" ");
            sb.AppendLine(text);
        }

        public void HeaderWithCode(int level, string code)
        {
            for (int i = 0; i < level; i++)
            {
                sb.Append("#");
            }
            sb.Append(" ");
            CodeQuote(code);
            sb.AppendLine();
        }

        public void HeaderWithLink(int level, string text, string url)
        {
            for (int i = 0; i < level; i++)
            {
                sb.Append("#");
            }
            sb.Append(" ");
            Link(text, url);
            sb.AppendLine();
        }

        public void Link(string text, string url)
        {
            sb.Append("[");
            sb.Append(text);
            sb.Append("]");
            sb.Append("(");
            sb.Append(url);
            sb.Append(")");
        }

        public void Image(string altText, string imageUrl)
        {
            sb.Append("!");
            Link(altText, imageUrl);
        }

        public void Code(string language, string code)
        {
            sb.Append("```");
            sb.AppendLine(language);
            sb.AppendLine(code);
            sb.AppendLine("```");
        }

        public void CodeQuote(string code)
        {
            sb.Append("<code>");
            sb.Append(code);
            sb.Append("</code>");
        }

        public void Table(string[] headers, IEnumerable<string[]> items)
        {
            sb.Append("| ");
            foreach (string item in headers)
            {
                sb.Append(item);
                sb.Append(" | ");
            }
            sb.AppendLine();

            sb.Append("| ");
            foreach (string item in headers)
            {
                sb.Append("---");
                sb.Append(" | ");
            }
            sb.AppendLine();

            foreach (string[] item in items)
            {
                sb.Append("| ");
                foreach (string item2 in item)
                {
                    sb.Append(item2);
                    sb.Append(" | ");
                }
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        public class DropdownItem
        {
            public string Type { get; set; }
            public string Name { get; set; }
            public XmlDocumentComment XmlDocumentComment { get; set; }
        }
        
        public void Dropdown(IEnumerable<DropdownItem> items)
        {
            foreach (DropdownItem item in items)
            {
                if (string.IsNullOrWhiteSpace(item.XmlDocumentComment?.Summary))
                {
                    sb.AppendLine($"&nbsp;&nbsp;&nbsp;&nbsp;{item.Type} {item.Name}<br />");
                    continue;
                }
                sb.Append($"<details><summary>{item.Type} {Regex.Replace(item.Name, @"\r\n?|\n", " ")}</summary><h3>Summary:</h3><p>{Beautifier.ReplaceLinks(item.XmlDocumentComment.Summary)}</p>");
                if (item.XmlDocumentComment.Parameters != null && item.XmlDocumentComment.Parameters.Count > 0 )
                {
                    sb.Append("<h3>Parameter:</h3><p><ul>");
                    foreach ((string k, string v) in item.XmlDocumentComment.Parameters)
                    {
                        sb.Append($"<li>{MarkdownCodeQuote(k.Trim())} - {Regex.Replace(v.Trim(), @"\r\n?|\n", " ")}</li>");
                    }
                    sb.Append("</ul></p>");
                }
                if (!string.IsNullOrWhiteSpace(item.XmlDocumentComment.Remarks))
                {
                    sb.AppendLine($"<h3>Remarks:</h3><p>{Beautifier.ReplaceLinks(item.XmlDocumentComment.Remarks)}</p>");
                }
                sb.AppendLine("<hr /></details>");
            }
            sb.AppendLine();
        }

        public void List(string text) // nest zero
        {
            sb.Append("- ");
            sb.AppendLine(text);
        }

        public void ListLink(string text, string url) // nest zero
        {
            sb.Append("- ");
            Link(text, url);
            sb.AppendLine();
        }

        public override string ToString()
        {
            return sb.ToString();
        }
    }

    static class KvExt
    {
        public static void Deconstruct<TK, TV>(this KeyValuePair<TK, TV> kv, out TK k, out TV v)
        {
            k = kv.Key;
            v = kv.Value;
        }
    }
}