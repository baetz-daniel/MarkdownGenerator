using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MarkdownWikiGenerator
{
    public class MarkdownableType
    {
        private readonly Type                                type;
        private readonly ILookup<string, XmlDocumentComment> commentLookup;

        public string Namespace
        {
            get { return type.Namespace; }
        }

        public string Name
        {
            get { return type.Name; }
        }

        public string BeautifyName
        {
            get { return Beautifier.BeautifyType(type); }
        }

        public MarkdownableType(Type type, ILookup<string, XmlDocumentComment> commentLookup)
        {
            this.type          = type;
            this.commentLookup = commentLookup;
        }

        public override string ToString()
        {
            MarkdownBuilder mb = new MarkdownBuilder();

            mb.HeaderWithCode(2, Beautifier.BeautifyType(type));
            mb.AppendLine();

            string desc = commentLookup[type.FullName].FirstOrDefault(x => x.MemberType == MemberType.Type)?.Summary ??
                          "";
            if (desc != "")
            {
                mb.AppendLine(desc);
            }
            {
                StringBuilder sb = new StringBuilder();

                string stat = type.IsAbstract                      && type.IsSealed ? "static " : "";
                string abst = type.IsAbstract && !type.IsInterface && !type.IsSealed ? "abstract " : "";
                string classOrStructOrEnumOrInterface = type.IsInterface
                    ? "interface"
                    : type.IsEnum
                        ? "enum"
                        : type.IsValueType
                            ? "struct"
                            : "class";

                sb.AppendLine(
                    $"public {stat}{abst}{classOrStructOrEnumOrInterface} {Beautifier.BeautifyType(type, true)}");
                string impl = string.Join(
                    ", ",
                    new[] { type.BaseType }.Concat(type.GetInterfaces())
                                           .Where(x => x != null && x != typeof(object) && x != typeof(ValueType))
                                           .Select(x => Beautifier.BeautifyType(x)));
                if (impl != "")
                {
                    sb.AppendLine("    : " + impl);
                }

                mb.Code("csharp", sb.ToString());
            }

            mb.AppendLine();

            if (type.IsEnum)
            {
                Type underlyingEnumType = Enum.GetUnderlyingType(type);

                var enums = Enum.GetNames(type)
                                .Select(
                                    x => new
                                    {
                                        Name  = x,
                                        Value = Convert.ChangeType(Enum.Parse(type, x), underlyingEnumType)
                                    })
                                .OrderBy(x => x.Value)
                                .ToArray();

                BuildTable(
                    mb, "Enum", enums, x => x.Value.ToString(), x => x.Name, x => x.Name);
            }
            else
            {
                BuildTable(
                    mb, "Constructors", GetConstructors(type),
                    x => x.Name, x => "#ctor", Beautifier.ToMarkdownConstructorInfo);
                BuildTable(
                    mb, "Fields", GetFields(type),
                    x => Beautifier.BeautifyType(x.FieldType),
                    x => x.Name, x => x.Name);
                BuildTable(
                    mb, "Properties", GetProperties(type),
                    x => Beautifier.BeautifyType(x.PropertyType), x => x.Name, x => x.Name);
                BuildTable(
                    mb, "Events", GetEvents(type),
                    x => Beautifier.BeautifyType(x.EventHandlerType), x => x.Name, x => x.Name);
                BuildTable(
                    mb, "Methods", GetMethods(type),
                    x => Beautifier.BeautifyType(x.ReturnType), x => x.Name, Beautifier.ToMarkdownMethodInfo);
                BuildTable(
                    mb, "Static Fields", GetStaticFields(type),
                    x => Beautifier.BeautifyType(x.FieldType), x => x.Name, x => x.Name);
                BuildTable(
                    mb, "Static Properties", GetStaticProperties(type),
                    x => Beautifier.BeautifyType(x.PropertyType), x => x.Name, x => x.Name);
                BuildTable(
                    mb, "Static Methods", GetStaticMethods(type),
                    x => Beautifier.BeautifyType(x.ReturnType), x => x.Name, Beautifier.ToMarkdownMethodInfo);
                BuildTable(
                    mb, "Static Events", GetStaticEvents(type),
                    x => Beautifier.BeautifyType(x.EventHandlerType), x => x.Name, x => x.Name);
            }

            return mb.ToString();
        }

        private ConstructorInfo[] GetConstructors(Type t)
        {
            return t.GetConstructors(
                        BindingFlags.Public       | BindingFlags.Instance | BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly | BindingFlags.InvokeMethod)
                    .Where(
                        x => !x.GetCustomAttributes<ObsoleteAttribute>().Any() && !x.IsPrivate)
                    .ToArray();
        }

        private MethodInfo[] GetMethods(Type t)
        {
            return t.GetMethods(
                        BindingFlags.Public       | BindingFlags.Instance | BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly | BindingFlags.InvokeMethod)
                    .Where(
                        x => !x.IsSpecialName                                  &&
                             !x.GetCustomAttributes<ObsoleteAttribute>().Any() &&
                             !x.IsPrivate                                      &&
                             !x.Name.Equals("finalize", StringComparison.InvariantCultureIgnoreCase))
                    .ToArray();
        }

        private PropertyInfo[] GetProperties(Type t)
        {
            return t.GetProperties(
                        BindingFlags.Public       | BindingFlags.Instance    | BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly | BindingFlags.GetProperty | BindingFlags.SetProperty)
                    .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                    .Where(
                        y =>
                        {
                            MethodInfo get = y.GetGetMethod(true);
                            MethodInfo set = y.GetSetMethod(true);
                            if (get != null && set != null)
                            {
                                return !(get.IsPrivate && set.IsPrivate);
                            }
                            if (get != null)
                            {
                                return !get.IsPrivate;
                            }
                            if (set != null)
                            {
                                return !set.IsPrivate;
                            }

                            return false;
                        })
                    .ToArray();
        }

        private FieldInfo[] GetFields(Type t)
        {
            return t.GetFields(
                        BindingFlags.Public       | BindingFlags.Instance | BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly | BindingFlags.GetField | BindingFlags.SetField)
                    .Where(
                        x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any() && !x.IsPrivate)
                    .ToArray();
        }

        private EventInfo[] GetEvents(Type t)
        {
            return t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                    .ToArray();
        }

        private FieldInfo[] GetStaticFields(Type t)
        {
            return t.GetFields(
                        BindingFlags.Public       | BindingFlags.Static   | BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly | BindingFlags.GetField | BindingFlags.SetField)
                    .Where(
                        x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any() && !x.IsPrivate)
                    .ToArray();
        }

        private PropertyInfo[] GetStaticProperties(Type t)
        {
            return t.GetProperties(
                        BindingFlags.Public       | BindingFlags.Static      | BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly | BindingFlags.GetProperty | BindingFlags.SetProperty)
                    .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                    .Where(
                        y =>
                        {
                            MethodInfo get = y.GetGetMethod(true);
                            MethodInfo set = y.GetSetMethod(true);
                            if (get != null && set != null)
                            {
                                return !(get.IsPrivate && set.IsPrivate);
                            }
                            if (get != null)
                            {
                                return !get.IsPrivate;
                            }
                            if (set != null)
                            {
                                return !set.IsPrivate;
                            }

                            return false;
                        })
                    .ToArray();
        }

        private MethodInfo[] GetStaticMethods(Type t)
        {
            return t.GetMethods(
                        BindingFlags.Public       | BindingFlags.Static | BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly | BindingFlags.InvokeMethod)
                    .Where(
                        x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any() && !x.IsPrivate)
                    .ToArray();
        }

        private EventInfo[] GetStaticEvents(Type t)
        {
            return t.GetEvents(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                    .ToArray();
        }

        private void BuildTable<T>(MarkdownBuilder mb,   string          label, T[]             array,
                                   Func<T, string> type, Func<T, string> name,  Func<T, string> finalName)
        {
            if (array.Any())
            {
                mb.AppendLine(label);
                mb.AppendLine();

                string[] head = this.type.IsEnum
                    ? new[] { "Value", "Name", "Summary" }
                    : new[] { "Type", "Name", "Summary" };

                IEnumerable<T> seq = array;
                if (!this.type.IsEnum)
                {
                    seq = array.OrderBy(name);
                }

                IEnumerable<string[]> data = seq.Select(
                    item2 =>
                    {
                        string _lookupInterfaces(Type t)
                        {
                            string summary    = null;
                            Type[] interfaces = t.GetInterfaces();

                            for (int i = 0; i < interfaces.Length && summary == null; i++)
                            {
                                summary = commentLookup[interfaces[i].FullName]
                                          .FirstOrDefault(
                                              x => x.MemberName == name(item2) ||
                                                   x.MemberName.StartsWith(name(item2) + "`"))
                                          ?.Summary;
                                if (summary?.Trim()
                                           .Equals("inheritdoc", StringComparison.InvariantCultureIgnoreCase) ?? true)
                                {
                                    return _lookupInterfaces(interfaces[i]);
                                }
                            }

                            return summary;
                        }

                        string _lookupType(Type t)
                        {
                            if (t == null) { return string.Empty; }
                            string summary = commentLookup[t.FullName]
                                             .FirstOrDefault(
                                                 x => x.MemberName == name(item2) ||
                                                      x.MemberName.StartsWith(name(item2) + "`"))
                                             ?.Summary;
                            if (summary?.Trim().Equals("inheritdoc", StringComparison.InvariantCultureIgnoreCase) ??
                                true)
                            {
                                summary = _lookupInterfaces(t) ?? _lookupType(t.BaseType);
                            }
                            return summary ?? string.Empty;
                        }

                        return new[]
                        {
                            MarkdownBuilder.MarkdownCodeQuote(type(item2)), finalName(item2), _lookupType(this.type)
                        };
                    });

                mb.Table(head, data);
                mb.AppendLine();
            }
        }
    }

    public static class MarkdownGenerator
    {
        public static MarkdownableType[] Load(string dllPath, string namespaceMatch)
        {
            string xmlPath = Path.Combine(
                Directory.GetParent(dllPath).FullName, Path.GetFileNameWithoutExtension(dllPath) + ".xml");

            XmlDocumentComment[] comments = new XmlDocumentComment[0];
            if (File.Exists(xmlPath))
            {
                comments = VSDocParser.ParseXmlComment(XDocument.Parse(File.ReadAllText(xmlPath)), namespaceMatch);
            }
            ILookup<string, XmlDocumentComment> commentsLookup = comments.ToLookup(x => x.ClassName);

            Regex namespaceRegex =
                !string.IsNullOrEmpty(namespaceMatch) ? new Regex(namespaceMatch) : null;

            return new[] { Assembly.LoadFrom(dllPath) }
                   .SelectMany(
                       x =>
                       {
                           try
                           {
                               return x.GetTypes();
                           }
                           catch (ReflectionTypeLoadException ex)
                           {
                               return ex.Types.Where(t => t != null);
                           }
                           catch
                           {
                               return Type.EmptyTypes;
                           }
                       })
                   .Where(x => x != null)
                   .Where(
                       x => x.IsPublic && !typeof(Delegate).IsAssignableFrom(x) &&
                            !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                   .Where(x => IsRequiredNamespace(x, namespaceRegex))
                   .Select(x => new MarkdownableType(x, commentsLookup))
                   .ToArray();
        }

        private static bool IsRequiredNamespace(Type type, Regex regex)
        {
            if (regex == null)
            {
                return true;
            }
            return regex.IsMatch(type.Namespace != null ? type.Namespace : string.Empty);
        }
    }
}