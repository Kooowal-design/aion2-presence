using System.Reflection;

namespace AionPresence;

public static class AppBrand
{
    public static Icon LoadIcon(int size = 32)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AionPresence.Icon")
            ?? throw new IOException("Missing application icon.");
        using var icon = new Icon(stream, size, size);
        return (Icon)icon.Clone();
    }
}
