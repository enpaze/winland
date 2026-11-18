namespace Winland.Domain;

public static partial class ConfigParser
{
    private sealed partial class Parser
    {
        private void HandleSource(string path, int lineNumber)
        {
            if (path.Length == 0)
            {
                Warn(lineNumber, "Malformed line.");
                return;
            }

            path = SourceKey(SourcePath.Expand(path, Variable));
            if (SourcePath.HasGlob(path))
            {
                var matches = sourceReader?.Match(path) ?? [];
                if (matches.Count == 0)
                {
                    _sources.Add(path);
                    Warn(lineNumber, $"Could not read source '{path}'.");
                    return;
                }

                foreach (var match in matches)
                {
                    if (!_openSources.Contains(SourceKey(match)))
                        IncludeSource(match, lineNumber);
                }

                return;
            }

            IncludeSource(path, lineNumber);
        }

        private void IncludeSource(string path, int lineNumber)
        {
            _sources.Add(path);
            if (sourceReader?.Read(path) is not string text)
            {
                Warn(lineNumber, $"Could not read source '{path}'.");
                return;
            }

            var key = SourceKey(path);
            if (!_openSources.Add(key))
            {
                Warn(lineNumber, $"Source '{path}' includes itself.");
                return;
            }

            var previous = _source;
            _source = key;
            try
            {
                ParseLines(text);
            }
            finally
            {
                _source = previous;
                _openSources.Remove(key);
            }
        }

        private string SourceKey(string path)
        {
            var folder = _source is null ? _rootFolder : Path.GetDirectoryName(_source);
            var joined = Path.IsPathFullyQualified(path) || string.IsNullOrEmpty(folder) ? path : Path.Join(folder, path);
            if (!Path.IsPathFullyQualified(joined))
                return joined.Replace('/', '\\');

            try
            {
                return Path.GetFullPath(joined);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return joined;
            }
        }

        private string? Variable(string name) => sourceReader?.Variable(name);
    }
}
