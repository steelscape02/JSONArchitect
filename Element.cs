namespace JsonArchitect
{
    public class Element
    {
        /// <summary>
        /// Stores an individual JSON element for efficient management of the JSON tree
        /// </summary>
        /// <param name="type">The type of the JSON item</param>
        /// <param name="name">The name of the JSON item. Can be parsed to improve readability and comply with language restrictions</param>
        
        public Element(Types? type, string name = "")
        {
            Name = name;
            if (!string.IsNullOrEmpty(name) && char.IsUpper(name[0]))
            {
                Name = char.ToLower(name[0]) + name.Substring(1);
            }
            if (Illegal.Any(name.Contains))
            {
                Rename = true;
            }
            Type = type;
        }

        /// <summary>
        /// The Child elements of the parent <c>Element</c>
        /// </summary>
        public readonly HashSet<Element> Children = [];

        /// <summary>
        /// The name of the <c>Element</c>
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The JSON type of the <c>Element</c>
        /// </summary>
        public Types? Type { get; set; }
        //To
        /// <summary>
        /// Indicates the Prim type (if present) of the <c>Element</c>
        /// </summary>
        public Types? Prim { get; set; }

        /// <summary>
        /// The nullability of the <c>Element</c>. When <c>true</c>, this <c>Element</c> could be null, when <c>false</c>
        /// the <c>Element</c> never appeared as null
        /// </summary>
        public bool Nullable { get; set; }

        /// <summary>
        /// Indicates that the <c>Element</c> is not present as a child in all occurrences of the parent <c>Element</c>
        /// </summary>
        public bool Inconsistent { get; private set; }

        /// <summary>
        /// The total count of prefix @ signs for unusual repeat naming
        /// </summary>
        public int AtCount { get; set; }

        /// <summary>
        /// If an illegal character is found, the <c>Name</c> member variable will need to be edited when printed. If <c>true</c>,
        /// this <c>Element</c> must be renamed in the DOM, if <c>false</c> the name is valid without renaming
        /// </summary>
        public bool Rename { get; set; }

        /// <summary>
        /// If the <c>Element</c> is a list, this will be <c>true</c>
        /// </summary>
        public bool List { get; set; }

        /// <summary>
        /// A list of illegal chars for variable naming
        /// </summary>
        public static readonly char[] Illegal =
        [
            '!', '@', '#', '$', '%', '^', '&', '*', '(', ')',
        '-', '+', '=', '{', '}', '[', ']', '|', '\\', ':',
        ';', '"', '\'', '<', '>', ',', '.', '?', '/', '~',
        '`', ' ', '\t', '\n'
        ];

        /// <summary>
        /// Available JSON types
        /// </summary>
        public enum Types
        {
            Object,
            Array,
            Integer,
            Double,
            Float,
            Long,
            String,
            Boolean,
            Null,
        }

        /// <summary>
        /// Add child to the <c>Children</c> list
        /// </summary>
        /// <param name="newChild">The desired <c>Element</c> to add</param>
        /// <returns><c>true</c> if added, <c>false</c> if not</returns>
        public bool AddChild(Element newChild)
        {
            var added = Children.Add(newChild);
            return added;
        }

        /// <summary>
        /// Get the duplicate <c>Element</c> in the Children list <i>if present</i>
        /// </summary>
        /// <param name="element">The <c>Element</c> to search for a duplicate of</param>
        /// <returns>The matching <c>Element</c> if found, otherwise <c>null</c></returns>
        public Element? GetMatching(Element element)
        {
            var match = Children.FirstOrDefault(x => x.Name == element.Name);

            return match ?? null;
        }

        /// <summary>
        /// Find if this object's children <b>exactly</b> match the <c>match</c>'s children
        /// </summary>
        /// <param name="match">The match item to compare to</param>
        /// <returns><c>true</c> if the children of both objects match perfectly, otherwise <c>false</c></returns>
        public bool MatchingChildren(Element match)
        {
            foreach (var child in match.Children)
            {
                if (!Children.Contains(child))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Ensure that this <c>Element</c> can capture any inconsistently appearing variables. If a Child in <c>match</c> is
        /// not found in this object, it will be added to this object's <c>Children</c> list with an <c>Inconsistent</c> flag set to <c>true</c>.
        /// If an object in this <c>Element</c>'s <c>Children</c> list is not found in <c>match</c>, it will also be flagged as <c>Inconsistent</c>
        /// </summary>
        /// <param name="match">The <c>Element</c> to compare against</param>
        public void MatchChildren(Element match)
        {
            //check these elements children
            foreach (var child in Children.Where(child => !match.Children.Contains(child)))
            {
                child.Inconsistent = true;
            }

            //check the match elements children
            foreach (var matchChild in match.Children)
            {
                if (!Children.Contains(matchChild))
                {
                    
                    var temp = matchChild;
                    temp.Inconsistent = true;
                    Children.Add(temp);
                }
            }
        }


        /// <summary>
        /// Remove all Children for this element
        /// </summary>
        public void ClearChildren()
        {
            Children.Clear();
        }

        /// <summary>
        /// Removes all illegal characters from the <c>Name</c> member variable
        /// </summary>
        /// <returns>An unaltered <c>Name</c> if no illegal characters were found, otherwise a <c>Name</c> with all illegal characters removed</returns>
        public string LegalName(char replace, bool addAt = false)
        {
            var legalName = Name;
            if(Illegal.Any(Name.Contains))
            {
                for(int i = 0; i < legalName.Length; i++)
                {
                    if (Illegal.Contains(legalName[i]))
                    {
                        legalName = legalName.Remove(i, 1);
                    }
                }
            }
            if (addAt) legalName = replace + legalName;
            return legalName;
        }

        //.Contains() overrides
        public override bool Equals(object? obj)
        {
            if (obj is Element other)
            {
                return Name == other.Name;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}