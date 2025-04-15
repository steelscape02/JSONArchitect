namespace JsonArchitect.dm
{
    public abstract class CSharpDm 
    {
        /// <summary>
        /// The default visibility of the C# elements in the data model
        /// </summary>
        private const string Vis = "public";

        /// <summary>
        /// Placeholder for repeated names
        /// </summary>
        private const string RptPlaceHolder = "_";

        /// <summary>
        /// Tracks if a variable name has been changed due to containing illegal characters.
        /// Used to improve the corresponding JSON package
        /// </summary>
        private static bool _overWrite;

        /// <summary>
        /// Array of reserved words in C#
        /// </summary>
        public static readonly string[] ReservedWords =
        [
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
        "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
        "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
        "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        ];

        /// <summary>
        /// Builds the Root class, with calls to <c>BuildSubDm</c> to create the child classes.
        /// </summary>
        /// <param name="elements">A <c>HashSet</c> of <c>Element</c> objects representing the <c>Root</c> class of
        /// the JSON response</param>
        /// <param name="baseName"></param>
        /// <param name="allOptional"></param>
        /// <returns>A string representation of a C# data model</returns>
        public static string BuildRoot(HashSet<Element> elements, string baseName, bool allOptional = false)
        {
            
            // Initialize the class definitions
            var classDefinitions = new List<string>();

            // Collect all classes, starting with the root
            var rootClass = $"{Vis} class {baseName}\n{{\n";

            foreach (var element in elements)
            {
                //if a rename is needed, this will contain the accurate name to correspond to a corrected name in the DM
                string? rename;
                if (element.Rename || HasReserved(element.Name))
                {
                    rename = $"\n   [JsonPropertyName(\"{element.Name}\")]\n   ";
                    _overWrite = true;
                }
                else
                    rename = null;

                var headerType = GetPrintType(element, false);

                var nullable = element.Nullable || element.Inconsistent ? headerType + "?" : headerType;
                if(element.Children.Count == 0 && allOptional) nullable += "?";
                rootClass += $"   {rename}{Vis} {nullable} {element.LegalName('@',HasReserved(element.Name))} {{get; set;}}\n";
            }

            rootClass += "}\n";
            classDefinitions.Add(rootClass);

            // Recursively build subclasses
            var visited = new HashSet<Element?>();
            foreach (var element in elements.Where(element => 
                !IsPrimitive(element.Prim.ToString()?.ToLower()) && !IsPrimitive(element.Type.ToString()?.ToLower())))
            {
                BuildSubDm(element, visited, classDefinitions,allOptional);
            }


            if (_overWrite) //add Json.Serialization package to use JsonPropertyName
            {
                classDefinitions.Insert(0, "using System.Text.Json.Serialization;\n");
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
        private static void BuildSubDm(Element element, HashSet<Element?> visited, List<string> classDefinitions, bool allOptional, bool redo = false)
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
                            for(int i=0;i<=match.AtCount; i++)
                                type = RptPlaceHolder + type;

                            visited.Remove(match);
                            match.AtCount += 1;
                            visited.Add(match);
                        }

                    }
                }
                
            }

            if(redo) 
            {
                type = RptPlaceHolder + type; 
            }

            var classDef = $"{Vis} class {type}\n{{\n";
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

                var nullable = childType;
                if (child.Nullable || child.Inconsistent || allOptional)
                {
                    nullable = childType + "?";
                }
                
                string? rename;
                if (child.Rename || HasReserved(child.Name))
                {
                    rename = $"\n    [JsonPropertyName(\"{child.Name}\")]\n    ";
                    _overWrite = true;
                }
                else
                    rename = null;

                classDef += $"    {rename}{Vis} {nullable} {child.LegalName('@', HasReserved(child.Name))} {{get; set;}}\n";

                if (child.Children.Count > 0)
                {
                    BuildSubDm(child, visited, classDefinitions, allOptional); // Recursive call for nested children
                    
                }
            }

            classDef += "}\n";
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
            if (list) text = "List<" + text + ">";

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
                    if (name != null) type = MakeFriendly(name, isList,isPrim);
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
                case null:
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