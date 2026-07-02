namespace ReeTrack.Infrastructure.Configuration;

public static class EnvFileLoader
{
    public static void Load()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        for (var depth = 0; depth < 8 && directory is not null; depth++, directory = directory.Parent)
        {
            var envPath = Path.Combine(directory.FullName, ".env");
            if (!File.Exists(envPath)) continue;

            foreach (var rawLine in File.ReadAllLines(envPath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0) continue;

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim().Trim('"');

                if (Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, value);
            }

            break;
        }
    }
}
