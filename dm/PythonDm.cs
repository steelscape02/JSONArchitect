namespace JsonArchitect.dm
{
    internal abstract class PythonDm
    {

        private const string RptPlaceHolder = "_";


        public static readonly string[] ReservedWords =
        [
            "False", "await", "else", "import", "pass",
            "None", "break", "except", "in", "raise",
            "True", "class", "finally", "is", "return",
            "and", "continue", "for", "lambda", "try",
            "as", "def", "from", "nonlocal", "while",
            "assert", "del", "global", "not", "with",
            "async", "elif", "if", "or", "yield"
        ];

        public static string BuildRoot(HashSet<Element> elements, string baseName, bool allOptional = false)
        {
            var classDefinitions = new List<string>
            {
                //imports
                "import json" +
                "\nfrom dataclasses import dataclass" +
                "\nfrom typing import List" +
                "\n"
            };
            
            var rootClass = $"@dataclass\nclass {baseName}:\n";
            foreach(var element in elements)
            {
                var headerType = GetPrintType(element, false);
                if (HasReserved(element.Name))
                    rootClass += $"    {element.LegalName('_', HasReserved(element.Name))} : {headerType} = field(metadata={{\"json_name\": \"{element.Name}\"}})\n";
                else
                    rootClass += $"    {element.Name} : {headerType}\n";
            }
            rootClass += "    @staticmethod\r\n" +
                "    def from_json(json_string: str) -> \"Root\":\r\n" +
                "        \"\"\"Parses a JSON string into a Root object.\"\"\"\r\n" +
                "        data = json.loads(json_string)\r\n        return Root(**data)\n";
            classDefinitions.Add(rootClass);

            var visited = new HashSet<Element?>();
            foreach (var element in elements.Where(element =>
                    !IsPrimitive(element.Prim.ToString()?.ToLower()) && !IsPrimitive(element.Type.ToString()?.ToLower())))
            {
                BuildSubDm(element, visited, classDefinitions);
            }
            classDefinitions.Add("\n"); //python convention to end with a newline
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
        /// <param name="redo"></param>
        private static void BuildSubDm(Element element, HashSet<Element?> visited, List<string> classDefinitions, bool redo = false)
        {
            if (element.Type == null) return;
            var type = GetPrintType(element, true);

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

            if (redo)
            {
                type = RptPlaceHolder + type;
            }

            
            var classDef = $"@dataclass\nclass {type}:\n";
            foreach (var child in element.Children)
            {

                child.Type ??= Element.Types.Null;
                string? childType;

                if (visited.TryGetValue(child, out var match) && match != null)
                {
                    //type_: str = field(metadata={"json_name": "type"})
                    match.List = child.List; //match list and type mem vars (not needed in normal TryGetValue override in Element)
                    match.Type = child.Type;
                    childType = GetPrintType(match, false);
                    for (int i = 0; i <= match.AtCount; i++)
                        childType = RptPlaceHolder + childType;
                }
                else
                    childType = GetPrintType(child, false);
                //child.LegalName('_', HasReserved(child.Name))
                if(HasReserved(child.Name))
                    classDef += $"    {child.LegalName('_', HasReserved(child.Name))} : {childType} = field(metadata={{\"json_name\": \"{child.Name}\"}})\n";
                else
                    classDef += $"    {child.Name} : {childType}\n";

                if (child.Children.Count > 0)
                {
                    BuildSubDm(child, visited, classDefinitions); // Recursive call for nested children

                }
            }
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
            if (list) text = "List[" + text + "]";

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
                        if (elem.Prim == Element.Types.Integer) name = "int";
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
                    type = "object";
                    break;
                case Element.Types.Integer:
                    type = "int";
                    break;
                case Element.Types.Double:
                case Element.Types.Float:
                case Element.Types.Long:
                case Element.Types.String:
                    type = type?.ToLower();
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
