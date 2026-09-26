using System;
using System.Text;

namespace Ornare.Imaging.Templates;

/// <summary>
/// The release-approved original Ornare folder template: "ornare-classic" Version 1.
/// Authored originally for Ornare using exact 256x256 vector geometry. Contains zero proprietary assets.
/// Geometry:
///   Rear body:   x=24, y=48, w=208, h=120, r=16 (data-ornare-role="deep")
///   Tab:         x=24, y=40, w=88,  h=64,  r=16 (data-ornare-role="deep")
///   Front panel: x=16, y=88, w=224, h=136, r=16 (data-ornare-role="base")
///   Semantic role strips clipped to front panel rounded rectangle:
///     light:     y=88..130 (data-ornare-role="light")
///     highlight: y=88..94  (data-ornare-role="highlight")
///     shadow:    y=206..224 (data-ornare-role="shadow")
/// </summary>
public static class OrnareClassicTemplate
{
    public const string TemplateId = "ornare-classic";
    public const int TemplateVersion = 1;

    public const string SvgContent =
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"" width=""256"" height=""256"">
  <defs>
    <clipPath id=""ornareFrontClip"">
      <rect x=""16"" y=""88"" width=""224"" height=""136"" rx=""16"" ry=""16"" />
    </clipPath>
  </defs>

  <!-- Rear body & Folder tab (deep role) -->
  <rect data-ornare-role=""deep"" x=""24"" y=""40"" width=""88"" height=""64"" rx=""16"" ry=""16"" fill=""#004578"" />
  <rect data-ornare-role=""deep"" x=""24"" y=""48"" width=""208"" height=""120"" rx=""16"" ry=""16"" fill=""#004578"" />

  <!-- Front panel group clipped to front rounded rectangle -->
  <g clip-path=""url(#ornareFrontClip)"">
    <!-- Base front panel -->
    <rect data-ornare-role=""base"" x=""16"" y=""88"" width=""224"" height=""136"" rx=""16"" ry=""16"" fill=""#0078D4"" />

    <!-- Light strip: y=88..130 (h=42) -->
    <rect data-ornare-role=""light"" x=""16"" y=""88"" width=""224"" height=""42"" fill=""#2B88D8"" />

    <!-- Highlight strip: y=88..94 (h=6) -->
    <rect data-ornare-role=""highlight"" x=""16"" y=""88"" width=""224"" height=""6"" fill=""#50E6FF"" />

    <!-- Shadow strip: y=206..224 (h=18) -->
    <rect data-ornare-role=""shadow"" x=""16"" y=""206"" width=""224"" height=""18"" fill=""#005A9E"" />
  </g>
</svg>";

    private static readonly Lazy<byte[]> LazyBytes = new(() => Encoding.UTF8.GetBytes(SvgContent));

    public static byte[] GetTemplateBytes() => LazyBytes.Value;
}
