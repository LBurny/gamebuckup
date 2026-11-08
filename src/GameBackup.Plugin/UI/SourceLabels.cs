using System;
using GameBackup.Core.Models;

namespace GameBackup.Plugin.UI
{
    /// <summary>
    /// User-facing names for candidate sources, shared by the list, the detail
    /// pane and the locate dialog. Rule files store the stable keys so future
    /// releases can reword the labels freely.
    /// </summary>
    public static class SourceLabels
    {
        public static string Describe(CandidateSource source)
        {
            switch (source)
            {
                case CandidateSource.UserRule:
                    return "本地规则";
                case CandidateSource.Manifest:
                    return "游戏清单";
                case CandidateSource.Steam:
                    return "Steam";
                case CandidateSource.Ubisoft:
                    return "育碧 Connect";
                case CandidateSource.Gog:
                    return "GOG";
                case CandidateSource.Epic:
                    return "Epic";
                case CandidateSource.CommonRoot:
                    return "常见存档目录";
                case CandidateSource.InstallDir:
                    return "安装目录";
                case CandidateSource.SessionTrace:
                    return "会话追踪";
                case CandidateSource.Llm:
                    return "AI 定位";
                case CandidateSource.Manual:
                    return "手动指定";
                default:
                    return "已确认";
            }
        }

        public static string RuleSource(CandidateSource source)
        {
            switch (source)
            {
                case CandidateSource.Steam:
                    return "steam";
                case CandidateSource.Ubisoft:
                    return "ubisoft";
                case CandidateSource.Gog:
                    return "gog";
                case CandidateSource.Epic:
                    return "epic";
                case CandidateSource.CommonRoot:
                    return "common";
                case CandidateSource.InstallDir:
                    return "install";
                case CandidateSource.SessionTrace:
                    return "session";
                case CandidateSource.Llm:
                    return "llm+confirmed";
                case CandidateSource.Manual:
                    return "manual";
                default:
                    return "confirmed";
            }
        }

        public static string DescribeRuleSource(string source)
        {
            switch ((source ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "steam":
                    return "Steam";
                case "ubisoft":
                    return "育碧 Connect";
                case "gog":
                    return "GOG";
                case "epic":
                    return "Epic";
                case "common":
                    return "常见存档目录";
                case "install":
                    return "安装目录";
                case "session":
                    return "会话追踪";
                case "llm+confirmed":
                    return "AI 定位";
                case "llm":
                    return "AI 定位";
                case "manual":
                    return "手动指定";
                default:
                    return string.IsNullOrWhiteSpace(source) ? "已确认" : source;
            }
        }
    }
}
