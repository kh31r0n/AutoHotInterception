using System;
using System.IO;
using System.Text;
using AHIConfigurator.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AHIConfigurator.Services
{
    public class ConfigStore
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() },
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        public ConfigStore(string path)
        {
            Path = path;
        }

        public string Path { get; }

        /// <summary>
        /// Set by <see cref="Load"/> when the file could not be read and was backed up; null otherwise.
        /// </summary>
        public string LoadWarning { get; private set; }

        /// <summary>
        /// Loads the config. A missing file gives a fresh config; a corrupt file is renamed to *.bad-&lt;timestamp&gt;
        /// (so nothing is lost) and a fresh config is returned with <see cref="LoadWarning"/> set.
        /// </summary>
        public AppConfig Load()
        {
            LoadWarning = null;
            if (!File.Exists(Path)) return AppConfig.CreateDefault();
            try
            {
                var config = JsonConvert.DeserializeObject<AppConfig>(File.ReadAllText(Path, Encoding.UTF8), Settings)
                             ?? throw new JsonException("File is empty");
                config.Normalize();
                return config;
            }
            catch (Exception e) when (e is JsonException || e is IOException)
            {
                var backup = Path + ".bad-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Move(Path, backup);
                LoadWarning = $"Your settings file could not be read ({e.Message}).\nIt was saved as {backup} and default settings were loaded.";
                return AppConfig.CreateDefault();
            }
        }

        /// <summary>
        /// Writes to a temp file first so a crash mid-write never leaves a truncated config.
        /// </summary>
        public void Save(AppConfig config)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            var temp = Path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(config, Settings), new UTF8Encoding(false));
            if (File.Exists(Path))
                File.Replace(temp, Path, null);
            else
                File.Move(temp, Path);
        }
    }
}
