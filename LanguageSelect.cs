using JsonArchitect.dm;

namespace JsonArchitect;

internal class LanguageSelector(IStorageManager localStorage)
{
    public enum Languages
    {
        CSharp,
        Cpp,
        Python
    }
    public string Select(Languages lang, HashSet<Element> contents)
    {
        var rootName = localStorage.Get(TextResources.rootName) as string ?? TextResources.baseName;
        var res = localStorage.Get(TextResources.allOptional);
        _ = bool.TryParse(res?.ToString(), out var allOptional);
        
        return lang switch
        {
            Languages.CSharp => CSharpDm.BuildRoot(contents, rootName, allOptional),
            Languages.Cpp => CppDm.BuildRoot(contents, rootName, allOptional),
            Languages.Python => PythonDm.BuildRoot(contents, rootName, allOptional),
            _ => throw new ArgumentOutOfRangeException(nameof(lang), TextResources.LangNotSupported),
        };
    }
}