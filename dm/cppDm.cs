

using System.Diagnostics;

namespace JsonArchitect.dm
{
    internal abstract class CppDm
    {
        /// <summary>
        /// The desired character for a placeholder in a repeated name
        /// </summary>
        private const string RptPlaceHolder = "_";

        /// <summary>
        /// Optional flag to indicate if an optional mem var is present
        /// </summary>
        private static bool _optional;


        public static readonly string[] ReservedWords =
        [
            "alignas", "alignof", "and", "and_eq", "asm", "auto", "bitand", "bitor", "bool",
            "break", "case", "catch", "char", "char16_t", "char32_t", "class", "compl", "const",
            "const_cast", "continue", "decltype", "default", "delete", "do", "double", "dynamic_cast",
            "else", "enum", "explicit", "export", "extern", "false", "final", "float", "for", "friend",
            "goto", "if", "inline", "int", "long", "mutable", "namespace", "new", "noexcept", "not", "not_eq",
            "nullptr", "operator", "or", "or_eq", "private", "protected", "public", "register", "reinterpret_cast",
            "return", "short", "signed", "sizeof", "static", "static_assert", "static_cast", "struct", "switch",
            "template", "this", "throw", "true", "try", "typedef", "typeid", "typename", "union", "unsigned",
            "using", "virtual", "void", "volatile", "wchar_t", "while"
        ];

        public static string BuildRoot(HashSet<Element> elements, string baseName, bool allOptional = false)
        {
            var classDefinitions = new List<string>
            {
                //imports
                "#include \"json.hpp\"\n" +
                "using json = nlohmann::json;\n\n" +
                "#include <vector>\n" +
                "#include <string>"
            };
            var forwards = new HashSet<Element>();
            var rootClass = $"\nclass {baseName}\n{{\npublic:\n";
            Debug.WriteLine(baseName);
            var classFirst = baseName[0].ToString().ToLower();
            _ = $"void from_json(const json& j, {baseName}& {classFirst})\n{{\n";
            var root = new Element(Element.Types.Object, baseName);
            foreach (var element in elements)
            {
                var headerType = GetPrintType(element, false);
                string? nullable;
                if (element.Nullable || element.Inconsistent)
                {
                    nullable = "std::optional<" + headerType + ">";
                    _optional = true;
                }
                else
                {
                    nullable = headerType;
                }
                
                root.AddChild(element);
                var fqName = element.LegalName('_', HasReserved(element.Name));
                rootClass += $"    {nullable} {fqName};\n";
            }
            forwards.Add(root);
            rootClass += $"    friend void from_json(const json& j, {baseName}& {classFirst});\n";
            rootClass += "};\n";
            //root class added after others to avoid circular deps
            

            var visited = new HashSet<Element?>();

            foreach (var element in elements.Where(element =>
                    !IsPrimitive(element.Prim.ToString()?.ToLower()) && !IsPrimitive(element.Type.ToString()?.ToLower())))
            {
                BuildSubDm(element, visited, classDefinitions, forwards,new Element(Element.Types.Object, baseName),allOptional);
            }
            classDefinitions.Add(rootClass);
            classDefinitions.Insert(1, _optional ? "#include <optional>\n" : "\n");

            foreach (var i in forwards)
            {
                classFirst = i.Name[0].ToString().ToLower();
                classDefinitions.Insert(2, $"class {GetPrintType(i, true)};");
                var currClass = $"void from_json(const json& j, {GetPrintType(i, true)}& {classFirst})\n{{\n";
                foreach (var e in i.Children)
                {
                    var headerType = GetPrintType(e, false);
                    var fqName = e.LegalName('_', HasReserved(e.Name));
                    if (e.Nullable) currClass += $"    if (j.contains(\"{e.Name}\")) {classFirst}.{fqName} = j.at(\"{e.Name}\").is_null() ? std::nullopt : std::make_optional(j.at(\"{e.Name}\").get<{headerType}>());\n";
                    else currClass += $"    if (j.contains(\"{e.Name}\")) j.at(\"{e.Name}\").get_to({classFirst}.{fqName});\n";
                }
                currClass += "\n}\n";
                classDefinitions.Add(currClass);
            }

            return string.Join("\n", classDefinitions);
        }

        /// <summary>
        /// Recursively builds child classes for nested elements.
        /// </summary>
        /// <param name="element">The Element to be searched</param>
        /// <param name="visited">The currently visited nested locations (stored by type)</param>
        /// <param name="classDefinitions">
        /// A list of class definitions, representing the child classes that are created
        /// </param>
        /// <param name="forwards">A list of forward class declarations</param>
        /// <param name="parent"></param>
        /// <param name="allOptional"></param>
        /// 
        private static void BuildSubDm(Element element, HashSet<Element?> visited, List<string> classDefinitions, HashSet<Element> forwards, Element parent, bool allOptional)
        {
            if (element.Type == null) return;
            var type = GetPrintType(element, true);
            if(element.Prim == null) forwards.Add(element);

            // Avoid re-creating classes
            if (IsPrimitive(type) || !visited.Add(element))
            {
                if (!visited.Add(element))
                {
                    if (visited.TryGetValue(element, out var match))
                    {
                        if (match == null || element.MatchingChildren(match)) return;
                        else
                        {
                            for (int i = 0; i <= match.AtCount; i++)
                                type = RptPlaceHolder + type;

                            visited.Remove(match);
                            match.AtCount += 1;
                            visited.Add(match);
                        }

                    }
                }

            }

            var classDef = $"class {type}\n{{\npublic:\n" +
                $"    {GetPrintType(parent,false)}* {parent.LegalName('_', HasReserved(parent.Name))};\n";
            var classFirst = element.Name[0].ToString().ToLower();
            
            foreach (var child in element.Children)
            {
                
                child.Type ??= Element.Types.Null;
                string? childType;
                if (visited.TryGetValue(child, out var match) && match != null)
                {

                    match.List = child.List; //match list and type mem vars (not needed in normal TryGetValue override in Element)
                    match.Type = child.Type;
                    childType = GetPrintType(match, false);
                    for (int i = 0; i <= match.AtCount; i++)
                        childType = RptPlaceHolder + childType;
                }
                else
                    childType = GetPrintType(child, false);
                string? nullable;
                if (child.Nullable || element.Inconsistent || allOptional)
                {
                    nullable = "std::optional<" + childType + ">";
                    _optional = true;
                }
                else
                {
                    nullable = childType;
                }
                var fqName = child.LegalName('_', HasReserved(child.Name));
                classDef += $"    {nullable} {fqName};\n";
                //const {GetPrintType(element,false)} & get_{element.Name}() const {{ return {fqName}; }}\n
                if (child.Children.Count > 0)
                {
                    BuildSubDm(child, visited, classDefinitions, forwards,element,allOptional); // Recursive call for nested children

                }
            }//friend void from_json(const json& j, Feature& f);

            classDef += $"    friend void from_json(const json& j, {type}& {classFirst});\n";
            classDef += "};\n";
            classDefinitions.Add(classDef);
            
            
        }
        

        /// <summary>
        /// If a type is a primitive or null (<b>o</b>bject) type, it won't create a class. This method returns a bool response in accordance to the type
        /// of the passed in variable
        /// </summary>
        /// <param name="type">The string representation of the type</param>
        /// <returns><c>true</c> if the type string is a primitive or null (<b>o</b>bject) type, otherwise <c>false</c></returns>
        private static bool IsPrimitive(string? type)
        {
            var primitives = new HashSet<string?>
        {
            "string", "integer","int", "long", "float", "double", "boolean", "bool"
        };
            return primitives.Contains(type);
        }

        /// <summary>
        /// Make the given text "friendly". If the given text was from a list object, return a capitalized and singular version of the word. Otherwise, capitalize and return.
        /// </summary>
        /// <param name="text">The text to convert</param>
        /// <param name="list"><c>true</c> if the caller is editing a List <c>Element</c> name</param>
        /// <param name="prim"></param>
        /// <returns>The "friendly" name</returns>
        private static string? MakeFriendly(string? text, bool list = false, bool prim = false)
        {
            if (string.IsNullOrEmpty(text)) return text;

            //check against basic plural rules
            if (!prim)
            {
                var cap = text[0].ToString().ToUpper();

                if (text.EndsWith("ies"))
                {
                    text = string.Concat(cap, text.AsSpan(1, text.Length - 4), "y");
                }
                //s -> remove s (except special cases)
                else if (text.EndsWith("es"))
                    text = string.Concat(cap, text.AsSpan(1, text.Length - 2));
                else if (text.EndsWith('s'))
                    text = string.Concat(cap, text.AsSpan(1, text.Length - 2));
                else
                {
                    text = string.Concat(cap, text.AsSpan(1, text.Length - 1));
                }
            }
            if (list) text = "std::vector<" + text + ">";

            return text;
        }

        /// <summary>
        /// Translates a <c>Element.Types...</c> enum to the appropriate string
        /// </summary>
        /// <param name="elem">The element to get a "print" type for</param>
        /// <param name="className">
        /// <c>true</c> if the <c>Element</c> object being passed in is a class name (removes any List tags)
        /// </param>
        /// <returns>Appropriate C# type string</returns>
        /// <exception cref="Exception">The type was not recognized as an <c>Element.Types...</c> type</exception>
        private static string? GetPrintType(Element elem, bool className)
        {

            var type = elem.Type.ToString();
            switch (elem.Type)
            {
                case Element.Types.Array:
                case Element.Types.Object:
                    var isList = elem.List;
                    var name = elem.Name;
                    if (className) isList = false;
                    var isPrim = IsPrimitive(elem.Prim.ToString()?.ToLower());
                    if (isPrim && isList)
                    {
                        if(elem.Prim == Element.Types.Integer) name = "int";
                        else if (elem.Prim == Element.Types.Float) name = "float";
                        else if (elem.Prim == Element.Types.Double) name = "double";
                        else if (elem.Prim == Element.Types.Long) name = "long";
                        else if (elem.Prim == Element.Types.String) name = "string";
                        else if (elem.Prim == Element.Types.Boolean) name = "bool";
                        else
                            name = elem.Prim.ToString()?.ToLower();
                    }
               
                    if (name != null) type = MakeFriendly(name, isList, isPrim);
                    break;
                case Element.Types.Null:
                    type = "json";
                    break;
                case Element.Types.Integer:
                    type = "int";
                    break;
                case Element.Types.Double:
                case Element.Types.Float:
                case Element.Types.Long:
                    type = type?.ToLower();
                    break;
                case Element.Types.String:
                    type = "std::string";
                    break;
                case Element.Types.Boolean:
                    type = "bool";
                    break;
                default:
                    throw new Exception("Unknown type");
            }

            return type;
        }

        /// <summary>
        /// Reserved words cannot be a variable name. To address this, a "@" is placed at the beginning of the text string
        /// </summary>
        /// <param name="text">The text to be checked</param>
        /// <returns>
        /// The original string if it is not a reserved word, or the original string with an "@" symbol appended to the
        /// front.
        /// </returns>
        private static bool HasReserved(string text)
        {
            return ReservedWords.Any(s => string.Equals(s, text, StringComparison.OrdinalIgnoreCase));
        }
    }
}
