using System.Globalization;
using System.Text.Json;
using Suwagcms.Sdk.BotRuntime.Availability;

namespace Suwagcms.Sdk.BotRuntime.Versions;

public sealed class FileVersionStore(string directory) : IVersionStore
{
    private const string StateFile = "state.json";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<VersionStoreState> ReadStateAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var pointer = await ReadPointerAsync(cancellationToken);
            return new VersionStoreState(pointer.Current, Held(), pointer.InUse, pointer.Availability);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<string?> ReadVersionAsync(int number, CancellationToken cancellationToken)
    {
        var path = VersionPath(number);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;
    }

    public async Task WriteVersionAsync(int number, string document, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(directory);
            var existed = File.Exists(VersionPath(number));
            await WriteAtomicallyAsync(VersionPath(number), document, cancellationToken);
            try
            {
                var pointer = await ReadPointerAsync(cancellationToken);
                await WritePointerAsync(pointer with { Current = number }, cancellationToken);
            }
            catch (IOException)
            {
                if (!existed)
                {
                    File.Delete(VersionPath(number));
                }

                throw;
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task WriteCurrentAsync(int number, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var pointer = await ReadPointerAsync(cancellationToken);
            await WritePointerAsync(pointer with { Current = number }, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task WriteInUseAsync(IReadOnlyList<int> numbers, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(directory);
            var pointer = await ReadPointerAsync(cancellationToken);
            await WritePointerAsync(pointer with { InUse = [.. numbers] }, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task WriteAvailabilityAsync(BotAvailability availability, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(availability);
        await Gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(directory);
            var pointer = await ReadPointerAsync(cancellationToken);
            if (pointer.Availability is null || availability.Number >= pointer.Availability.Number)
            {
                await WritePointerAsync(pointer with { Availability = availability }, cancellationToken);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task RemoveVersionAsync(int number, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            File.Delete(VersionPath(number));
        }
        finally
        {
            Gate.Release();
        }
    }

    private string VersionPath(int number) => Path.Combine(directory, $"v{number.ToString(CultureInfo.InvariantCulture)}.json");

    private int[] Held() =>
        Directory.Exists(directory)
            ?
            [
                .. Directory
                    .EnumerateFiles(directory, "v*.json")
                    .Select(path => Path.GetFileNameWithoutExtension(path)[1..])
                    .Where(name => int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    .Select(name => int.Parse(name, CultureInfo.InvariantCulture))
                    .Order(),
            ]
            : [];

    private async Task<Pointer> ReadPointerAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, StateFile);
        if (!File.Exists(path))
        {
            return new Pointer(null, null);
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Pointer>(stream, BotRuntimeJson.Options, cancellationToken)
            ?? throw new InvalidDataException($"{path} is empty.");
    }

    private Task WritePointerAsync(Pointer pointer, CancellationToken cancellationToken) =>
        WriteAtomicallyAsync(
            Path.Combine(directory, StateFile),
            JsonSerializer.Serialize(pointer, BotRuntimeJson.Options),
            cancellationToken
        );

    private static async Task WriteAtomicallyAsync(string path, string content, CancellationToken cancellationToken)
    {
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, content, cancellationToken);
        File.Move(temporary, path, overwrite: true);
    }

    private sealed record Pointer(int? Current, int[]? InUse, BotAvailability? Availability = null);
}
