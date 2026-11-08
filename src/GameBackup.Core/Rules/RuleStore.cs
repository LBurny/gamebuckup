using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace GameBackup.Core.Rules
{
    [DataContract]
    public sealed class RuleSet
    {
        [DataMember(Name = "version", Order = 0)]
        public int Version { get; set; } = 1;

        [DataMember(Name = "rules", Order = 1)]
        public List<GameRule> Rules { get; set; } = new List<GameRule>();
    }

    [DataContract]
    public sealed class GameRule
    {
        [DataMember(Name = "key", Order = 0)]
        public string Key { get; set; }

        [DataMember(Name = "name", Order = 1)]
        public string Name { get; set; }

        [DataMember(Name = "appId", Order = 2)]
        public string AppId { get; set; }

        [DataMember(Name = "installDir", Order = 3)]
        public string InstallDir { get; set; }

        [DataMember(Name = "saves", Order = 4)]
        public string[] Saves { get; set; } = new string[0];

        [DataMember(Name = "source", Order = 5)]
        public string Source { get; set; }

        [DataMember(Name = "confirmedAt", Order = 6)]
        public string ConfirmedAt { get; set; }
    }

    /// <summary>
    /// Persists user-confirmed save locations. These rules always outrank
    /// manifest data and are the feedback loop that makes later games fast.
    /// </summary>
    public sealed class RuleStore
    {
        private readonly object _gate = new object();
        private readonly string _path;

        public RuleStore(string path)
        {
            _path = path;
        }

        public string Path => _path;

        public RuleSet Load()
        {
            lock (_gate)
            {
                return LoadInternal();
            }
        }

        public GameRule Find(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            return Load().Rules.FirstOrDefault(
                x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public void Upsert(GameRule rule)
        {
            if (rule == null || string.IsNullOrEmpty(rule.Key))
            {
                return;
            }

            lock (_gate)
            {
                var set = LoadInternal();
                set.Rules.RemoveAll(x => string.Equals(x.Key, rule.Key, StringComparison.OrdinalIgnoreCase));
                set.Rules.Add(rule);
                SaveInternal(set);
            }
        }

        private RuleSet LoadInternal()
        {
            if (!File.Exists(_path))
            {
                return new RuleSet();
            }

            try
            {
                var json = File.ReadAllText(_path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new RuleSet();
                }

                var serializer = new DataContractJsonSerializer(typeof(RuleSet));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var set = serializer.ReadObject(stream) as RuleSet;
                    if (set == null)
                    {
                        return new RuleSet();
                    }
                    set.Rules = set.Rules ?? new List<GameRule>();
                    return set;
                }
            }
            catch
            {
                return new RuleSet();
            }
        }

        private void SaveInternal(RuleSet set)
        {
            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var serializer = new DataContractJsonSerializer(typeof(RuleSet));
            string json;
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, set);
                json = Encoding.UTF8.GetString(stream.ToArray());
            }

            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, json, new UTF8Encoding(false));

            if (File.Exists(_path))
            {
                try
                {
                    File.Replace(temporary, _path, null);
                    return;
                }
                catch
                {
                    File.Delete(_path);
                }
            }

            File.Move(temporary, _path);
        }
    }
}
