namespace ReProgman.Model;

/// <summary>
/// A minimal INI file reader/writer used for portable settings storage.
/// Section and key lookups are case-insensitive, like classic Windows INI files.
/// </summary>
public sealed class IniDocument
{
    // Why not a plain Dictionary&lt;string, Dictionary&lt;...&gt;&gt;: the document order of
    // sections and keys must be preserved so that saved files stay diff-friendly.
    private readonly List<Section> _sections = [];

    private sealed class Section(string name)
    {
        public string Name { get; } = name;
        public List<string> KeyOrder { get; } = [];
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> Sections => _sections.Select(s => s.Name).ToArray();

    public static IniDocument Parse(string text)
    {
        var document = new IniDocument();
        Section? current = null;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = document.GetOrAddSection(line[1..^1]);
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator > 0 && current is not null)
            {
                current.Values[line[..separator]] = line[(separator + 1)..];
                if (!current.KeyOrder.Contains(line[..separator], StringComparer.OrdinalIgnoreCase))
                {
                    current.KeyOrder.Add(line[..separator]);
                }
            }
        }

        return document;
    }

    public string? Get(string section, string key)
    {
        var found = FindSection(section);
        return found is not null && found.Values.TryGetValue(key, out var value) ? value : null;
    }

    public void Set(string section, string key, string value)
    {
        var target = GetOrAddSection(section);
        if (!target.Values.ContainsKey(key))
        {
            target.KeyOrder.Add(key);
        }

        target.Values[key] = value;
    }

    public IReadOnlyList<string> Keys(string section) =>
        FindSection(section)?.KeyOrder.ToArray() ?? [];

    public string ToText()
    {
        var builder = new System.Text.StringBuilder();
        foreach (var section in _sections)
        {
            builder.Append('[').Append(section.Name).Append(']').AppendLine();
            foreach (var key in section.KeyOrder)
            {
                builder.Append(key).Append('=').Append(section.Values[key]).AppendLine();
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private Section? FindSection(string name) =>
        _sections.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    private Section GetOrAddSection(string name)
    {
        var section = FindSection(name);
        if (section is null)
        {
            section = new Section(name);
            _sections.Add(section);
        }

        return section;
    }
}
