namespace Ornare.Core;

using System;
using System.Globalization;

public readonly record struct NtfsFolderIdentity(uint VolumeSerialNumber, ulong FileId64)
{
    public byte[] GetFileIdBytes() => BitConverter.GetBytes(FileId64);

    public string VolumeSerialHex => VolumeSerialNumber.ToString("X8", CultureInfo.InvariantCulture);

    public string FileIdHex => FileId64.ToString("X16", CultureInfo.InvariantCulture);

    public static NtfsFolderIdentity FromBytes(uint volumeSerialNumber, ReadOnlySpan<byte> fileIdBytes)
    {
        if (fileIdBytes.Length != 8)
        {
            throw new ArgumentException("FileId bytes must be exactly 8 bytes.", nameof(fileIdBytes));
        }

        ulong fileId64 = BitConverter.ToUInt64(fileIdBytes);
        return new NtfsFolderIdentity(volumeSerialNumber, fileId64);
    }
}
