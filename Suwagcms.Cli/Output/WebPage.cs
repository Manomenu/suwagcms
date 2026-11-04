using System.ComponentModel;
using System.Diagnostics;

namespace Suwagcms.Cli.Output;

internal static class WebPage
{
    public static async Task<int> ShowAsync(Uri page, TextWriter output, TextWriter error, Action<Uri>? openBrowser)
    {
        await output.WriteLineAsync(page.ToString());
        try
        {
            (openBrowser ?? OpenInDefaultBrowser)(page);
        }
        catch (Win32Exception)
        {
            // Why: a server without a screen (SSH, CI) has no opener, and the address printed above is the way in.
            await error.WriteLineAsync("No browser could be opened: open the address above.");
        }
        return ExitCodes.Done;
    }

    // Why: with the shell, .NET hands an address to the system's opener (xdg-open, open, start).
    private static void OpenInDefaultBrowser(Uri page) =>
        Process.Start(new ProcessStartInfo(page.ToString()) { UseShellExecute = true })?.Dispose();
}
