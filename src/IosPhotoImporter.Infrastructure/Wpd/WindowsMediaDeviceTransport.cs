using MediaDevices;
using IosPhotoImporter.Core.Models;

namespace IosPhotoImporter.Infrastructure.Wpd;

public sealed class WindowsMediaDeviceTransport : IWpdTransport
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".JPG",
        ".JPEG",
        ".PNG",
        ".HEIC",
        ".HEIF"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".MOV",
        ".MP4",
        ".M4V"
    };

    private static readonly string[] LivePhotoStillExtensions =
    {
        ".HEIC",
        ".JPG",
        ".JPEG",
        ".PNG"
    };

    public Task<bool> IsDriverInstalledAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(false);
        }

        try
        {
            _ = MediaDevice.GetDevices();
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<IReadOnlyList<WpdDeviceSnapshot>> GetConnectedDevicesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<IReadOnlyList<WpdDeviceSnapshot>>(Array.Empty<WpdDeviceSnapshot>());
        }

        var snapshots = new List<WpdDeviceSnapshot>();

        foreach (var candidate in MediaDevice.GetDevices())
        {
            ct.ThrowIfCancellationRequested();

            var deviceId = string.IsNullOrWhiteSpace(candidate.DeviceId)
                ? candidate.PnPDeviceID
                : candidate.DeviceId;

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                continue;
            }

            var isReady = false;
            var isTrusted = false;

            try
            {
                ConnectReadOnly(candidate);
                _ = candidate.GetRootDirectory();
                isReady = true;
                isTrusted = true;
            }
            catch
            {
                isReady = false;
                isTrusted = false;
            }
            finally
            {
                SafeDisconnect(candidate);
            }

            snapshots.Add(new WpdDeviceSnapshot(
                deviceId,
                ResolveDisplayName(candidate),
                isTrusted,
                isReady));
        }

        return Task.FromResult<IReadOnlyList<WpdDeviceSnapshot>>(snapshots);
    }

    public async IAsyncEnumerable<WpdMediaObject> EnumerateMediaAsync(
        string deviceId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        var device = TryOpenDevice(deviceId);
        if (device is null)
        {
            yield break;
        }

        try
        {
            foreach (var mediaPath in EnumerateCandidateFilePaths(device, ct))
            {
                ct.ThrowIfCancellationRequested();

                MediaFileInfo fileInfo;
                try
                {
                    fileInfo = device.GetFileInfo(mediaPath);
                }
                catch
                {
                    continue;
                }

                var extension = Path.GetExtension(fileInfo.Name).ToUpperInvariant();
                if (!IsSupportedExtension(extension))
                {
                    continue;
                }

                var createdAt = ToDateTimeOffset(
                    fileInfo.DateAuthored
                    ?? fileInfo.CreationTime
                    ?? fileInfo.LastWriteTime
                    ?? DateTime.Now);

                var persistentId = string.IsNullOrWhiteSpace(fileInfo.PersistentUniqueId)
                    ? null
                    : fileInfo.PersistentUniqueId;

                var mediaKind = IsVideoExtension(extension)
                    ? MediaKind.Video
                    : MediaKind.Image;

                var isLivePhotoMotion = IsLivePhotoMotionComponent(device, fileInfo, extension);

                yield return new WpdMediaObject(
                    fileInfo.FullName,
                    persistentId,
                    fileInfo.Name,
                    extension,
                    (long)fileInfo.Length,
                    createdAt,
                    mediaKind,
                    isLivePhotoMotion);

                await Task.Yield();
            }
        }
        finally
        {
            SafeDisconnect(device);
        }
    }

    public Task<Stream> OpenMediaReadStreamAsync(string deviceId, string sourceObjectId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Media device transport is only available on Windows.");
        }

        var device = TryOpenDevice(deviceId)
            ?? throw new InvalidOperationException("Device is not connected or cannot be opened.");

        try
        {
            var fileInfo = device.GetFileInfo(sourceObjectId);
            var stream = fileInfo.OpenRead();
            return Task.FromResult<Stream>(new DeviceBoundReadStream(device, stream));
        }
        catch
        {
            SafeDisconnect(device);
            throw;
        }
    }

    private static MediaDevice? TryOpenDevice(string deviceId)
    {
        var device = MediaDevice.GetDevices()
            .FirstOrDefault(x => string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));

        if (device is null)
        {
            return null;
        }

        try
        {
            ConnectReadOnly(device);
            return device;
        }
        catch
        {
            SafeDisconnect(device);
            return null;
        }
    }

    private static void ConnectReadOnly(MediaDevice device)
    {
        if (!device.IsConnected)
        {
            device.Connect(MediaDeviceAccess.GenericRead, MediaDeviceShare.Read, enableCache: true);
        }
    }

    private static void SafeDisconnect(MediaDevice device)
    {
        try
        {
            if (device.IsConnected)
            {
                device.Disconnect();
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static IEnumerable<string> EnumerateCandidateFilePaths(MediaDevice device, CancellationToken ct)
    {
        var roots = device.GetContentLocations(ContentType.Image)
            .Concat(device.GetContentLocations(ContentType.Video))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roots.Length == 0)
        {
            roots = ["\\"];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            ct.ThrowIfCancellationRequested();

            string[] files;
            try
            {
                files = device.GetFiles(root, "*.*", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                if (!seen.Add(file))
                {
                    continue;
                }

                var extension = Path.GetExtension(file).ToUpperInvariant();
                if (IsSupportedExtension(extension))
                {
                    yield return file;
                }
            }
        }
    }

    private static bool IsLivePhotoMotionComponent(MediaDevice device, MediaFileInfo fileInfo, string extension)
    {
        if (!extension.Equals(".MOV", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var directory = fileInfo.Directory?.FullName;
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileInfo.Name);
        foreach (var stillExtension in LivePhotoStillExtensions)
        {
            var candidate = CombineDevicePath(directory, baseName + stillExtension);

            try
            {
                if (device.FileExists(candidate))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore lookup errors for this candidate and continue.
            }
        }

        return false;
    }

    private static string CombineDevicePath(string directory, string leaf)
    {
        return directory.EndsWith('\\')
            ? directory + leaf
            : directory + "\\" + leaf;
    }

    private static string ResolveDisplayName(MediaDevice device)
    {
        if (!string.IsNullOrWhiteSpace(device.FriendlyName))
        {
            return device.FriendlyName;
        }

        if (!string.IsNullOrWhiteSpace(device.Model))
        {
            return device.Model;
        }

        if (!string.IsNullOrWhiteSpace(device.Manufacturer))
        {
            return device.Manufacturer;
        }

        return "USB iOS Device";
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime value)
    {
        var kind = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Local)
            : value;

        return new DateTimeOffset(kind);
    }

    private static bool IsSupportedExtension(string extension)
    {
        return IsImageExtension(extension) || IsVideoExtension(extension);
    }

    private static bool IsImageExtension(string extension)
    {
        return ImageExtensions.Contains(extension);
    }

    private static bool IsVideoExtension(string extension)
    {
        return VideoExtensions.Contains(extension);
    }

    private sealed class DeviceBoundReadStream(MediaDevice device, Stream innerStream) : Stream
    {
        public override bool CanRead => innerStream.CanRead;

        public override bool CanSeek => innerStream.CanSeek;

        public override bool CanWrite => innerStream.CanWrite;

        public override long Length => innerStream.Length;

        public override long Position
        {
            get => innerStream.Position;
            set => innerStream.Position = value;
        }

        public override void Flush() => innerStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) => innerStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => innerStream.Seek(offset, origin);

        public override void SetLength(long value) => innerStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => innerStream.Write(buffer, offset, count);

        public override ValueTask DisposeAsync()
        {
            var disposeTask = innerStream.DisposeAsync();
            SafeDisconnect(device);
            device.Dispose();
            return disposeTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                innerStream.Dispose();
                SafeDisconnect(device);
                device.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
