using System;
using System.Text;

namespace Ornare.Imaging.Templates;

/// <summary>
/// The release-approved original Ornare folder template: "ornare-soft" Version 1 (F03).
/// Features softer, rounded silhouette with 24px corner radius.
/// Geometry:
///   Rear body:   x=24, y=48, w=208, h=120, r=24 (data-ornare-role="deep")
///   Tab:         x=24, y=40, w=88,  h=64,  r=24 (data-ornare-role="deep")
///   Front panel: x=16, y=88, w=224, h=136, r=24 (data-ornare-role="base")
///   Semantic role strips clipped to front panel rounded rectangle:
///     light:     y=88..140 (h=52, data-ornare-role="light")
///     highlight: y=88..96  (h=8,  data-ornare-role="highlight")
///     shadow:    y=212..224 (h=12, data-ornare-role="shadow")
/// </summary>
public static class OrnareSoftTemplate
{
    public const string TemplateId = "ornare-soft";
    public const int TemplateVersion = 1;

    public const string SvgContent =
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"" width=""256"" height=""256"">
  <defs>
    <clipPath id=""ornareSoftFrontClip"">
      <rect x=""16"" y=""88"" width=""224"" height=""136"" rx=""24"" ry=""24"" />
    </clipPath>
  </defs>

  <!-- Rear body & Folder tab (deep role) -->
  <rect data-ornare-role=""deep"" x=""24"" y=""40"" width=""88"" height=""64"" rx=""24"" ry=""24"" fill=""#004578"" />
  <rect data-ornare-role=""deep"" x=""24"" y=""48"" width=""208"" height=""120"" rx=""24"" ry=""24"" fill=""#004578"" />

  <!-- Front panel group clipped to front rounded rectangle -->
  <g clip-path=""url(#ornareSoftFrontClip)"">
    <!-- Base front panel -->
    <rect data-ornare-role=""base"" x=""16"" y=""88"" width=""224"" height=""136"" rx=""24"" ry=""24"" fill=""#0078D4"" />

    <!-- Light strip: y=88..140 (h=52) -->
    <rect data-ornare-role=""light"" x=""16"" y=""88"" width=""224"" height=""52"" fill=""#2B88D8"" />

    <!-- Highlight strip: y=88..96 (h=8) -->
    <rect data-ornare-role=""highlight"" x=""16"" y=""88"" width=""224"" height=""8"" fill=""#50E6FF"" />

    <!-- Shadow strip: y=212..224 (h=12) -->
    <rect data-ornare-role=""shadow"" x=""16"" y=""212"" width=""224"" height=""12"" fill=""#005A9E"" />
  </g>
</svg>";

    private static readonly Lazy<byte[]> LazyBytes = new(() => Encoding.UTF8.GetBytes(SvgContent));

    public static byte[] GetTemplateBytes() => LazyBytes.Value;
}
