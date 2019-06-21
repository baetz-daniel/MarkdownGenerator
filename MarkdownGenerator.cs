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

            mb.HeaderWithCode(1, Beautifier.BeautifyType(type));
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
                    mb, "Enum", enums, x => x.Value.ToString(), x => x.Name, x => x.Name, (info, comment) => true);
            }
            else
            {
                BuildTable(
                    mb, "Constructors", GetConstructors(type),
                    x => x.Name, x => "#ctor", Beautifier.ToMarkdownMethodInfo, (info, comment) =>
                    {
                        ParameterInfo[] param = info.GetParameters();
                        return param.Length == comment.Parameters.Count && param.All(
                                   p =>
                                   {
                                       return comment.Parameters.ContainsKey(p.Name);
                                   });
                    });
                BuildTable(
                    mb, "Fields", GetFields(type),
                    x => Beautifier.BeautifyType(x.FieldType),
                    x => x.Name, x => x.Name, (info, comment) => true);
                BuildTable(
                    mb, "Properties", GetProperties(type),
                    x => Beautifier.BeautifyType(x.PropertyType), x => x.Name, x => x.Name, (info, comment) => true);
                BuildTable(
                    mb, "Events", GetEvents(type),
                    x => Beautifier.BeautifyType(x.EventHandlerType), x => x.Name, x => x.Name,
                    (info, comment) => true);
                BuildTable(
                    mb, "Methods", GetMethods(type),
                    x => Beautifier.BeautifyType(x.ReturnType), x => x.Name, Beautifier.ToMarkdownMethodInfo,
                    (info, comment) =>
                    {
                        ParameterInfo[] param = info.GetParameters();
                        return param.Length == comment.Parameters.Count && param.All(
                                   p =>
                                   {
                                       return comment.Parameters.ContainsKey(p.Name);
                                   });
                    });
                BuildTable(
                    mb, "Static Fields", GetStaticFields(type),
                    x => Beautifier.BeautifyType(x.FieldType), x => x.Name, x => x.Name, (info, comment) => true);
                BuildTable(
                    mb, "Static Properties", GetStaticProperties(type),
                    x => Beautifier.BeautifyType(x.PropertyType), x => x.Name, x => x.Name, (info, comment) => true);
                BuildTable(
                    mb, "Static Methods", GetStaticMethods(type),
                    x => Beautifier.BeautifyType(x.ReturnType), x => x.Name, Beautifier.ToMarkdownMethodInfo,
                    (info, comment) =>
                    {
                        ParameterInfo[] param = info.GetParameters();
                        return param.Length == comment.Parameters.Count && param.All(
                                   p =>
                                   {
                                       return comment.Parameters.ContainsKey(p.Name);
                                   });
                    });
                BuildTable(
                    mb, "Static Events", GetStaticEvents(type),
                    x => Beautifier.BeautifyType(x.EventHandlerType), x => x.Name, x => x.Name,
                    (info, comment) => true);
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

        private void BuildTable<T>(MarkdownBuilder                   mb,   string          label, T[] array,
                                   Func<T, string>                   type, Func<T, string> name,
                                   Func<T, string>                   finalName,
                                   Func<T, XmlDocumentComment, bool> get)
        {
            if (array.Any())
            {
                mb.Header(2, label);
                mb.AppendLine();

                IEnumerable<T> seq = array;
                if (!this.type.IsEnum)
                {
                    seq = array.OrderBy(name);
                }

                IEnumerable<MarkdownBuilder.DropdownItem> data = seq.Select(
                    item2 =>
                    {
                        XmlDocumentComment _lookupInterfaces(Type t)
                        {
                            XmlDocumentComment lookup     = null;
                            Type[]             interfaces = t.GetInterfaces();
                            for (int i = 0; i < interfaces.Length && lookup == null; i++)
                            {
                                lookup = commentLookup[interfaces[i].FullName]
                                    .FirstOrDefault(
                                        x => (x.MemberName == name(item2) ||
                                              x.MemberName.StartsWith(name(item2) + "`")) && get(item2, x));
                                if (lookup == null || (lookup.Summary?.Trim()
                                                             .Equals(
                                                                 "inheritdoc",
                                                                 StringComparison.InvariantCultureIgnoreCase) ?? true))
                                {
                                    lookup = _lookupInterfaces(interfaces[i]);
                                }
                            }

                            return lookup;
                        }

                        XmlDocumentComment _lookupType(Type t)
                        {
                            if (t == null) { return null; }
                            XmlDocumentComment lookup = commentLookup[t.FullName]
                                .FirstOrDefault(
                                    x => (x.MemberName == name(item2) ||
                                          x.MemberName.StartsWith(name(item2) + "`")) && get(item2, x));
                            if (lookup == null || (lookup.Summary?.Trim()
                                                         .Equals(
                                                             "inheritdoc",
                                                             StringComparison.InvariantCultureIgnoreCase) ?? true))
                            {
                                lookup = _lookupInterfaces(t) ?? _lookupType(t.BaseType);
                            }
                            return lookup;
                        }

                        return new MarkdownBuilder.DropdownItem
                        {
                            Type               = MarkdownBuilder.MarkdownCodeQuote(type(item2)),
                            Name               = finalName(item2),
                            XmlDocumentComment = _lookupType(this.type)
                        };
                    });

                mb.Dropdown(data);
                mb.AppendLine("___");
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
            return regex == null || regex.IsMatch(type.Namespace ?? string.Empty);
        }
    }
}