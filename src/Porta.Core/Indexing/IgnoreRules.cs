using System.Text;
using System.Text.RegularExpressions;

namespace Porta.Core.Indexing;

/// <summary>
/// Правила исключения файлов из индексации/синхронизации (подмножество gitignore).
/// Регистрозависимо (детерминизм между устройствами). См. docs/features/12-ignore-patterns.md.
/// </summary>
public sealed class IgnoreRules
{
    private readonly List<Regex> _segmentRules = [];
    private readonly List<Regex> _pathRules = [];

    public IgnoreRules(IEnumerable<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        foreach (string raw in patterns)
            Compile(raw);
    }

    /// <summary>Пустой набор — ничего не игнорируется.</summary>
    public static IgnoreRules Empty { get; } = new([]);

    /// <summary>Игнорируется ли путь (относительный, с разделителем '/').</summary>
    public bool IsIgnored(string relativePath)
    {
        string path = relativePath.Replace('\\', '/');

        foreach (Regex rule in _pathRules)
            if (rule.IsMatch(path))
                return true;

        if (_segmentRules.Count > 0)
            foreach (string segment in path.Split('/'))
                foreach (Regex rule in _segmentRules)
                    if (rule.IsMatch(segment))
                        return true;

        return false;
    }

    private void Compile(string raw)
    {
        string pattern = raw.Trim();
        if (pattern.Length == 0 || pattern.StartsWith('#'))
            return;

        bool hasSlash = pattern.Contains('/');
        pattern = pattern.TrimEnd('/');
        if (pattern.StartsWith('/'))
            pattern = pattern.TrimStart('/');
        if (pattern.Length == 0)
            return;

        var options = RegexOptions.CultureInvariant | RegexOptions.Singleline;
        if (hasSlash)
            _pathRules.Add(new Regex("^" + Translate(pattern, pathMode: true) + "(/.*)?$", options));
        else
            _segmentRules.Add(new Regex("^" + Translate(pattern, pathMode: false) + "$", options));
    }

    private static string Translate(string glob, bool pathMode)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < glob.Length; i++)
        {
            char c = glob[i];
            switch (c)
            {
                case '*':
                    if (pathMode && i + 1 < glob.Length && glob[i + 1] == '*')
                    {
                        sb.Append(".*");
                        i++;
                    }
                    else
                    {
                        sb.Append("[^/]*");
                    }
                    break;
                case '?':
                    sb.Append("[^/]");
                    break;
                default:
                    if ("\\^$.|+()[]{}".IndexOf(c) >= 0)
                        sb.Append('\\');
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
