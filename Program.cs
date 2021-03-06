﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MarkdownGenerator
{
    class Program
    {
        // 0 = dll src path, 1 = dest root
        private static void Main(string[] args)
        {
            // put dll & xml on same diretory.
            string target         = string.Empty;
            string dest           = "md";
            string namespaceMatch = string.Empty;
            switch (args.Length) 
            {
                case 1:
                    target = args[0];
                    break;
                case 2:
                    target = args[0];
                    dest   = args[1];
                    break;
                case 3:
                    target         = args[0];
                    dest           = args[1];
                    namespaceMatch = args[2];
                    break;
            }
            AppDomain.CurrentDomain.AssemblyResolve += (s, a) =>
            {
                string archSpecificPath = Path.Combine(
                    Directory.GetParent(target).FullName,
                    Environment.Is64BitProcess ? "x64" : "x86", a.Name.Split(new[] { ',' }, 2)[0] + ".dll");
                return File.Exists(archSpecificPath)
                    ? Assembly.LoadFile(archSpecificPath)
                    : null;
            };
            MarkdownableType[] types = MarkdownGenerator.Load(target, namespaceMatch);

            // Home Markdown Builder
            MarkdownBuilder homeBuilder = new MarkdownBuilder();
            homeBuilder.Header(1, "References");
            homeBuilder.AppendLine();

            MarkdownBuilder sidebarBuilder = new MarkdownBuilder();

            foreach (IGrouping<string, MarkdownableType> g in types.GroupBy(x => x.Namespace).OrderBy(x => x.Key))
            {
                if (!Directory.Exists(dest))
                {
                    Directory.CreateDirectory(dest);
                }

                homeBuilder.HeaderWithLink(2, g.Key, g.Key);
                homeBuilder.AppendLine();

                sidebarBuilder.HeaderWithLink(5, g.Key, g.Key);
                sidebarBuilder.AppendLine();

                StringBuilder sb = new StringBuilder();
                foreach (MarkdownableType item in g.OrderBy(x => x.Name))
                {
                    homeBuilder.ListLink(
                        MarkdownBuilder.MarkdownCodeQuote(item.BeautifyName),
                        g.Key + "#" + item.BeautifyName.Replace("<", "")
                                          .Replace(">", "")
                                          .Replace(",", "")
                                          .Replace(" ", "-")
                                          .ToLower());
                    sidebarBuilder.ListLink(
                        MarkdownBuilder.MarkdownCodeQuote(item.BeautifyName),
                        g.Key + "#" + item.BeautifyName.Replace("<", "")
                                          .Replace(">", "")
                                          .Replace(",", "")
                                          .Replace(" ", "-")
                                          .ToLower());

                    sb.Append(item);
                }

                File.WriteAllText(Path.Combine(dest, g.Key + ".md"), sb.ToString());
                homeBuilder.AppendLine();
                sidebarBuilder.AppendLine();
            }

            // Gen Home
            File.WriteAllText(Path.Combine(dest, "Home.md"), homeBuilder.ToString());

            // Gen sidebar
            File.WriteAllText(Path.Combine(dest, "_Sidebar.md"), sidebarBuilder.ToString());

            File.WriteAllText(
                Path.Combine(dest, "_Footer.md"),
                $@"*** 
#### auto generated wiki!<br/>all changes can be removed within next update
_Copyright (c) {DateTime.Now.Year} exomia_
");
        }
    }
}