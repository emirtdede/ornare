using System;
using System.Collections.Generic;

namespace Ornare.Imaging.Badges;

/// <summary>
/// Vector asset store for status preset badge icons (F04).
/// Contains clean, monochrome vector paths from Microsoft Fluent UI System Icons (MIT License, commit a563cf9).
/// </summary>
public static class StatusBadgeAssets
{
    private const string ClockPath = "M3.5 12C3.5 7.30558 7.30558 3.5 12 3.5C16.6944 3.5 20.5 7.30558 20.5 12C20.5 16.6944 16.6944 20.5 12 20.5C7.30558 20.5 3.5 16.6944 3.5 12ZM12 2C6.47715 2 2 6.47715 2 12C2 17.5228 6.47715 22 12 22C17.5228 22 22 17.5228 22 12C22 6.47715 17.5228 2 12 2ZM11.9931 6.64827C11.9435 6.28233 11.6295 6 11.25 6C10.836 6 10.5 6.336 10.5 6.75V12.75L10.5069 12.8517C10.5565 13.2177 10.8705 13.5 11.25 13.5H15.25L15.3517 13.4931C15.7177 13.4435 16 13.1295 16 12.75C16 12.336 15.664 12 15.25 12H12V6.75L11.9931 6.64827Z";

    private const string CheckmarkCirclePath = "M12 2C17.5228 2 22 6.47715 22 12C22 17.5228 17.5228 22 12 22C6.47715 22 2 17.5228 2 12C2 6.47715 6.47715 2 12 2ZM12 3.5C7.30558 3.5 3.5 7.30558 3.5 12C3.5 16.6944 7.30558 20.5 12 20.5C16.6944 20.5 20.5 16.6944 20.5 12C20.5 7.30558 16.6944 3.5 12 3.5ZM10.75 13.4393L15.2197 8.96967C15.5126 8.67678 15.9874 8.67678 16.2803 8.96967C16.5466 9.23594 16.5708 9.6526 16.3529 9.94621L16.2803 10.0303L11.2803 15.0303C11.0141 15.2966 10.5974 15.3208 10.3038 15.1029L10.2197 15.0303L7.71967 12.5303C7.42678 12.2374 7.42678 11.7626 7.71967 11.4697C7.98594 11.2034 8.4026 11.1792 8.69621 11.3971L8.78033 11.4697L10.75 13.4393L15.2197 8.96967L10.75 13.4393Z";

    private const string ArchivePath = "M10.25 11C9.83579 11 9.5 11.3358 9.5 11.75C9.5 12.1642 9.83579 12.5 10.25 12.5H13.75C14.1642 12.5 14.5 12.1642 14.5 11.75C14.5 11.3358 14.1642 11 13.75 11H10.25ZM3 5.25C3 4.00736 4.00736 3 5.25 3H18.75C19.9926 3 21 4.00736 21 5.25V6.75C21 7.5301 20.603 8.21748 20 8.62111V17.25C20 19.3211 18.3211 21 16.25 21H7.75C5.67893 21 4 19.3211 4 17.25V8.62111C3.39701 8.21748 3 7.5301 3 6.75V5.25ZM5.5 9V17.25C5.5 18.4926 6.50736 19.5 7.75 19.5H16.25C17.4926 19.5 18.5 18.4926 18.5 17.25V9H5.5ZM5.25 4.5C4.83579 4.5 4.5 4.83579 4.5 5.25V6.75C4.5 7.16421 4.83579 7.5 5.25 7.5H18.75C19.1642 7.5 19.5 7.16421 19.5 6.75V5.25C19.5 4.83579 19.1642 4.5 18.75 4.5H5.25Z";

    private const string ImportantPath = "M12.0004 2.00195C9.86006 2.00195 8.125 3.73701 8.125 5.87732C8.125 8.79606 9.33243 12.4289 9.93776 14.0759C10.2606 14.9542 11.097 15.4995 12.0025 15.4995C12.9057 15.4995 13.7409 14.957 14.0646 14.0809C14.6705 12.4412 15.8757 8.827 15.8757 5.87732C15.8757 3.73701 14.1407 2.00195 12.0004 2.00195ZM9.625 5.87732C9.625 4.56544 10.6885 3.50195 12.0004 3.50195C13.3122 3.50195 14.3757 4.56544 14.3757 5.87732C14.3757 8.53153 13.2639 11.9202 12.6576 13.561C12.5621 13.8194 12.3089 13.9995 12.0025 13.9995C11.6954 13.9995 11.4412 13.8184 11.3457 13.5585C10.7387 11.9069 9.625 8.50056 9.625 5.87732ZM12.0011 17.001C10.6198 17.001 9.5 18.1208 9.5 19.5021C9.5 20.8834 10.6198 22.0032 12.0011 22.0032C13.3825 22.0032 14.5022 20.8834 14.5022 19.5021C14.5022 18.1208 13.3825 17.001 12.0011 17.001ZM11 19.5021C11 18.9492 11.4482 18.501 12.0011 18.501C12.554 18.501 13.0022 18.9492 13.0022 19.5021C13.0022 20.055 12.554 20.5032 12.0011 20.5032C11.4482 20.5032 11 20.055 11 19.5021Z";

    // F05 Priority Badges: 100% Ornare-authored geometric monochrome vector paths (v1)
    private const string PriorityHighPath = "M4 16L8 16L12 10L16 16L20 16L12 4Z";
    private const string PriorityMediumPath = "M6 10H18A2 2 0 0 1 20 12A2 2 0 0 1 18 14H6A2 2 0 0 1 4 12A2 2 0 0 1 6 10Z";
    private const string PriorityLowPath = "M4 8L8 8L12 14L16 8L20 8L12 20Z";

    private static readonly Dictionary<string, string> PathsByAssetId = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ic_fluent_clock_24_regular", ClockPath },
        { "ic_fluent_checkmark_circle_24_regular", CheckmarkCirclePath },
        { "ic_fluent_archive_24_regular", ArchivePath },
        { "ic_fluent_important_24_regular", ImportantPath },
        { "ornare_priority_high_24", PriorityHighPath },
        { "ornare_priority_medium_24", PriorityMediumPath },
        { "ornare_priority_low_24", PriorityLowPath }
    };

    public static string GenerateBadgeSvg(string badgeAssetId, string fillHex = "#212121")
    {
        if (!PathsByAssetId.TryGetValue(badgeAssetId, out string? pathData))
        {
            throw new ArgumentException($"Unknown badge asset '{badgeAssetId}'.", nameof(badgeAssetId));
        }

        return $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 24 24"" width=""24"" height=""24"">
  <path d=""{pathData}"" fill=""{fillHex}"" />
</svg>";
    }

    public static bool TryGetPathData(string badgeAssetId, out string? pathData)
    {
        return PathsByAssetId.TryGetValue(badgeAssetId, out pathData);
    }
}
