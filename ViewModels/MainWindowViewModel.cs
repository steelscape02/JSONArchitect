using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using JsonArchitect.dm;
using ReactiveUI;

namespace JsonArchitect.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _jsonOutput = string.Empty;
    public string JsonOutput
    {
        get => _jsonOutput;
        set
        {
            IsJsonOutput = !string.IsNullOrEmpty(value);
            this.RaiseAndSetIfChanged(ref _jsonOutput, value);
        }
    }

    private bool _isJsonOutput;
    public bool IsJsonOutput
    {
        get => _isJsonOutput;
        set => this.RaiseAndSetIfChanged(ref _isJsonOutput, value);
    }
    
    private string _jsonInput = string.Empty;
    public string JsonInput
    {
        get => _jsonInput;
        set
        {
            IsJsonInput = !string.IsNullOrEmpty(value);
            IsJsonInputValid = IsValidJson(value);
            this.RaiseAndSetIfChanged(ref _jsonInput, value);
        }
    }
    
    private bool _isJsonInput;
    public bool IsJsonInput
    {
        get => _isJsonInput;
        set => this.RaiseAndSetIfChanged(ref _isJsonInput, value);
    }
    
    private bool _isJsonInputValid;
    public bool IsJsonInputValid
    {
        get => _isJsonInputValid;
        set => this.RaiseAndSetIfChanged(ref _isJsonInputValid, value);
    }
    
    private string _validateMsg = string.Empty;
    public string ValidateMsg
    {
        get => _validateMsg;
        set => this.RaiseAndSetIfChanged(ref _validateMsg, value);
    }
    
    private bool _isPaneOpen;
    public bool IsPaneOpen
    {
        get => _isPaneOpen;
        set => this.RaiseAndSetIfChanged(ref _isPaneOpen, value);
    }
    
    private string _selectedLanguage = string.Empty;
    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set => this.RaiseAndSetIfChanged(ref _selectedLanguage, value);
    }
    
    private int _selectedIndex;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedIndex, value);
    }

    private bool _flashAnimation;
    public bool FlashAnimation
    {
        get => _flashAnimation;
        set => this.RaiseAndSetIfChanged(ref _flashAnimation, value);
    }

    private bool _isValFlyoutOpen;
    public bool IsValFlyoutOpen
    {
        get => _isValFlyoutOpen;
        set => this.RaiseAndSetIfChanged(ref _isValFlyoutOpen, value);
    }

    private bool _allOptional;
    public bool AllOptional
    {
        get
        {
            var res = _manager.Get(TextResources.allOptional);
            _ = bool.TryParse(res?.ToString(), out _allOptional);
            return _allOptional;
        }
        set
        {
            _manager.Set(TextResources.allOptional,value);
            this.RaiseAndSetIfChanged(ref _allOptional, value);
        }
    }

    private bool _suggestCorrs;
    public bool SuggestCorrs
    {
        get
        {
            var res = _manager.Get(TextResources.suggestCorrs);
            _ = bool.TryParse(res?.ToString(), out _suggestCorrs);
            return _suggestCorrs;
        }
        set
        {
            _manager.Set(TextResources.suggestCorrs, value);
            this.RaiseAndSetIfChanged(ref _suggestCorrs, value);
        }
    }

    private bool _detailedErrors;
    public bool DetailedErrors
    {
        get
        {
            var res = _manager.Get(TextResources.detailedErrors);
            _ = bool.TryParse(res?.ToString(), out _detailedErrors);
            return _detailedErrors;
        }
        set
        {
            _manager.Set(TextResources.detailedErrors, value);
            this.RaiseAndSetIfChanged(ref _detailedErrors, value);
        }
    }

    //suggest corrections
    private bool _isSugCorrsFlyoutOpen;
    public bool IsSugCorrsFlyoutOpen
    {
        get => _isSugCorrsFlyoutOpen;
        set => this.RaiseAndSetIfChanged(ref _isSugCorrsFlyoutOpen, value);
    }
    
    private string _corrMsg = string.Empty;
    public string CorrMsg
    {
        get => _corrMsg;
        set => this.RaiseAndSetIfChanged(ref _corrMsg, value);
    }
    
    private string _corrEntry = string.Empty;
    public string CorrEntry
    {
        get => _corrEntry;
        set
        {
            IsCorrEntry = !string.IsNullOrEmpty(value) && !IsReserved(value) && !IsIllegal(value);
            this.RaiseAndSetIfChanged(ref _corrEntry, value);
        }
    }

    private bool _isCorrEntry;
    public bool IsCorrEntry
    {
        get => _isCorrEntry;
        set => this.RaiseAndSetIfChanged(ref _isCorrEntry, value);
    }

    private static string _popupContent = string.Empty;

    private static bool _cancelAllSuggs;
    
    public ObservableCollection<string> Languages { get; set; } = ["C#", "C++", "Python"];
    
    private static FilePickerFileType JsonFileType { get; } = new("All Images") {
        Patterns = ["*.json"],
        AppleUniformTypeIdentifiers = ["public.json"],
        MimeTypes = ["application/json"]
    };
    
    private readonly Window _mainWindow;
    private readonly StorageManager _manager;
    
    public static void SaveSettings()
    {
        var settings = new StorageManager();
        //fill the setting with data.
        var json = JsonSerializer.Serialize(settings.GetAll());
        const string filePath = "settings.json";
        File.WriteAllText(filePath, json);
    }

    public static void LoadSettings()
    {
        var settings = new StorageManager();
        try
        {
            var contents = File.ReadAllText("settings.json");
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(contents);
            if (json != null) settings.SetAll(json);
        }
        catch (FileNotFoundException)
        { }
    }

    private static bool IsValidJson(string str)
    {
        try
        {
            JsonDocument.Parse(str);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task WaitForSuggClose()
    {
        if (IsSugCorrsFlyoutOpen)
        {
            await this.WhenAnyValue(x => x.IsSugCorrsFlyoutOpen)
                .Where(isOpen => !isOpen)
                .Take(1); // Take only the first false value
        }
    }

    private bool IsReserved(string text)
    {
        var reserved = SelectedLanguage switch
        {
            "C#" => CSharpDm.ReservedWords.Contains(text),
            "C++" => PythonDm.ReservedWords.Contains(text),
            "Python" => CppDm.ReservedWords.Contains(text),
            _ => false
        };
        return reserved;
    }

    private bool IsIllegal(string text)
    {
        var illegal = Element.Illegal.Any(text.Contains);
        return illegal;
    }
    
    private async Task SuggestCorrs_Click(HashSet<Element> elements)
    {
        foreach (var i in elements)
        {
            var reserved = IsReserved(i.Name);
            var illegalChars = IsIllegal(i.Name);
            //show popup
            if (reserved || illegalChars)
            {
                if (reserved)
                {
                    CorrMsg = $"Reserved word found: {i.Name}";
                }
                else if (illegalChars)
                {
                    CorrMsg = $"Illegal char found: {i.Name}";
                }
                
                _popupContent = i.Name;
                IsSugCorrsFlyoutOpen = true;
                await WaitForSuggClose();
                if(_popupContent != "")
                    i.Name = _popupContent;
                if(_cancelAllSuggs) return;
                CorrEntry = string.Empty;
            }

            //recursive search
            if (i.Children.Count > 0)
            {
                await SuggestCorrs_Click(i.Children);
            }
        }
    }
    
    private async Task OpenFilePicker()
    {
        //This can also be applied for SaveFilePicker
        var files = await _mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            Title = "JSON File",
            FileTypeFilter = [JsonFileType, FilePickerFileTypes.All]
        });
        
        if (files.Count >= 1)
        {
            // Open reading stream from the first file.
            await using var stream = await files[0].OpenReadAsync();
            using var streamReader = new StreamReader(stream);
            // Reads all the content of file as a text.
            var fileContent = await streamReader.ReadToEndAsync();
            JsonInput = fileContent;
        }
    }
    
    private async Task SaveFilePicker()
    {
        var ext = SelectedLanguage switch
        {
            "C#" => ".cs",
            "C++" => ".cpp",
            "Python" => ".py",
            _ => ".txt"
        };
        // Get top level from the current control. Alternatively, you can use Window reference instead.
        var file = await _mainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Output",
            SuggestedFileName = $"output{ext}",
            FileTypeChoices =
            [
                new FilePickerFileType($"{SelectedLanguage} File") { Patterns = ["*{ext}"] },
                new FilePickerFileType("All Files") { Patterns = ["*"] },
            ]
        });

        if (file is not null)
        {
            // Open writing stream from the file.
            await using var stream = await file.OpenWriteAsync();
            await using var streamWriter = new StreamWriter(stream);
            // Write some content to the file.
            await streamWriter.WriteLineAsync(JsonOutput);
        }
    }

    private async Task CopyOutput()
    {
        var clipboard = _mainWindow.Clipboard;
        var dataObject = new DataObject();
        dataObject.Set(DataFormats.Text, JsonOutput);
        if(clipboard is not null)
            await clipboard.SetDataObjectAsync(dataObject);
    }
    
    private void ClearInput()
    {
        JsonInput = string.Empty;
    }
    
    private void ValidateInput(TextBox textBox)
    {
        IsValFlyoutOpen = !IsValFlyoutOpen;
        try
        {
            using (JsonDocument.Parse(JsonInput)) {}
            ValidateMsg = "No error";
        }
        catch (JsonException j)
        {
            var res = _manager.Get(TextResources.detailedErrors);
            _ = bool.TryParse(res?.ToString(), out var detailedError);
            if (detailedError)
            {
                ValidateMsg = string.IsNullOrWhiteSpace(JsonInput) ? "Empty JSON" : j.ToString();
            }
            else
            {
                ValidateMsg = string.IsNullOrWhiteSpace(JsonInput) ? "Empty JSON" : "JSON Error";
            }
        }
        FlyoutBase.ShowAttachedFlyout(textBox);
    }

    private void CancelMsg()
    {
        IsValFlyoutOpen = false;
    }

    private void ConfirmSugg()
    {
        _popupContent = CorrEntry;
        IsSugCorrsFlyoutOpen = false;
    }

    private void CancelSugg() //cancel one
    {
        IsSugCorrsFlyoutOpen = false;
    } 

    private void CancelAllSugg() //cancel all
    {
        _cancelAllSuggs = true;
    }
    
    // ReSharper disable once AsyncVoidMethod
    private async void FlashControl()
    {
        FlashAnimation = !FlashAnimation;
        await Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1.5));

            // Switch back to the UI thread to update the property.
            Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
            {
                FlashAnimation = !FlashAnimation; // Toggle the state
            });
        });
    }
    
    private async Task SubmitInput()
    {
        var selector = new LanguageSelector(_manager);
        if (!IsValidJson(JsonInput)) return;
        var reader = new JsonReader(JsonInput);
        var contents = reader.ReadJson();
        var res = _manager.Get(TextResources.suggestCorrs);
        _ = bool.TryParse(res?.ToString(), out var suggCorr);
        if (suggCorr)
        {
            await SuggestCorrs_Click(contents);
        }
        string dm;
        switch (SelectedLanguage)
        {
            case "C#":
                dm = selector.Select(LanguageSelector.Languages.CSharp, contents);
                JsonOutput = dm;
                break;
            case "C++":
                dm = selector.Select(LanguageSelector.Languages.Cpp, contents);
                JsonOutput = dm;
                break;
            case "Python":
                dm = selector.Select(LanguageSelector.Languages.Python, contents);
                JsonOutput = dm;
                break;
            default:
                FlashControl();
                break;
        }
    }
    
    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
    public ReactiveCommand<Unit, Unit> UploadCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<TextBox, Unit> ValidateCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelValCommand { get; }
    public ReactiveCommand<Unit, Unit> SubmitCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    
    public ReactiveCommand<Unit, Unit> ConfirmSuggCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelSuggCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelAllSuggCommand { get; }
    
    public MainWindowViewModel(Window window)
    {
        _mainWindow = window;
        _manager = new StorageManager();
        
        //pane toggle command
        ToggleCommand= ReactiveCommand.Create(() => {
                IsPaneOpen = !IsPaneOpen;
            });

        UploadCommand = ReactiveCommand.CreateFromTask(OpenFilePicker);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveFilePicker);
        CopyCommand = ReactiveCommand.CreateFromTask(CopyOutput);
        
        ClearCommand = ReactiveCommand.Create(ClearInput);
        
        ValidateCommand = ReactiveCommand.Create<TextBox>(ValidateInput);
        CancelValCommand = ReactiveCommand.Create(CancelMsg); //close validate msg box
        
        SubmitCommand = ReactiveCommand.CreateFromTask(SubmitInput);
        ConfirmSuggCommand = ReactiveCommand.Create(ConfirmSugg);
        CancelSuggCommand = ReactiveCommand.Create(CancelSugg);
        CancelAllSuggCommand = ReactiveCommand.Create(CancelAllSugg);
    }
}