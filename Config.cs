using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace LOLSummonerTiming
{
    // Config class that stores app settings and auto-saves to config.json on any change
    public sealed class Config : INotifyPropertyChanged, IJsonOnDeserialized
    {
        private static readonly object _sync = new();
        private static Config? _current;

        // Singleton-like accessor for ease of use across the app
        public static Config Current
        {
            get
            {
                if (_current is null)
                {
                    _current = Load();
                }
                return _current;
            }
        }

        // Path to the config file (next to the executable)
        [JsonIgnore]
        public static string FilePath { get; } = Path.Combine(AppContext.BaseDirectory, "config.json");

        // Prevent saving while the object is being populated from disk
        private bool _suspendSave = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        // Called by System.Text.Json after deserialization completes
        void IJsonOnDeserialized.OnDeserialized()
        {
            _suspendSave = false;
        }

        // Loads config from disk. If missing or invalid, creates defaults and saves.
        public static Config Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var cfg = JsonSerializer.Deserialize<Config>(json, SerializerOptions()) ?? new Config();
                    return cfg; // _suspendSave will be false thanks to OnDeserialized
                }
            }
            catch
            {
                // Ignore errors and fallback to defaults
            }

            var def = new Config();
            def._suspendSave = false; // enable saving after initialization
            def.Save(); // create initial file with defaults
            return def;
        }

        // Saves the current config state to disk
        public void Save()
        {
            if (_suspendSave) return;
            lock (_sync)
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(this, SerializerOptions());
                var temp = FilePath + ".tmp";
                File.WriteAllText(temp, json);
                File.Copy(temp, FilePath, true);
                File.Delete(temp);
            }
        }

        private static JsonSerializerOptions SerializerOptions() => new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        // Helper to set field, raise change notification, and auto-save if value changed
        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            Save();
            return true;
        }

        // ---------- Hotkeys ----------
        private Key _topKey = Key.NumPad7;
        public Key TopKey
        {
            get => _topKey;
            set => SetProperty(ref _topKey, value);
        }

        private Key _jungleKey = Key.NumPad4;
        public Key JungleKey
        {
            get => _jungleKey;
            set => SetProperty(ref _jungleKey, value);
        }

        private Key _midKey = Key.NumPad8;
        public Key MidKey
        {
            get => _midKey;
            set => SetProperty(ref _midKey, value);
        }

        private Key _adcKey = Key.NumPad9;
        public Key AdcKey
        {
            get => _adcKey;
            set => SetProperty(ref _adcKey, value);
        }

        private Key _supportKey = Key.NumPad6;
        public Key SupportKey
        {
            get => _supportKey;
            set => SetProperty(ref _supportKey, value);
        }

        private Key _sendKey = Key.NumPad5;
        public Key SendKey
        {
            get => _sendKey;
            set => SetProperty(ref _sendKey, value);
        }

        // ---------- Text / templates ----------
        private string _beforeText = "flash";
        public string BeforeText
        {
            get => _beforeText;
            set => SetProperty(ref _beforeText, value);
        }

        private string _textTemplate = "{role} {time}";
        public string TextTemplate
        {
            get => _textTemplate;
            set => SetProperty(ref _textTemplate, value);
        }

        // ---------- Role names (used in message composition) ----------
        private string _topRole = "top";
        public string TopRole
        {
            get => _topRole;
            set => SetProperty(ref _topRole, value);
        }

        private string _jungleRole = "jungle";
        public string JungleRole
        {
            get => _jungleRole;
            set => SetProperty(ref _jungleRole, value);
        }

        private string _midRole = "mid";
        public string MidRole
        {
            get => _midRole;
            set => SetProperty(ref _midRole, value);
        }

        private string _adcRole = "adc";
        public string AdcRole
        {
            get => _adcRole;
            set => SetProperty(ref _adcRole, value);
        }

        private string _supportRole = "support";
        public string SupportRole
        {
            get => _supportRole;
            set => SetProperty(ref _supportRole, value);
        }
    }
}
