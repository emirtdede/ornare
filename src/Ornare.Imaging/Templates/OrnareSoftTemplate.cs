using System;
using System.Text;

namespace Ornare.Imaging.Templates;

/// <summary>
/// The release-approved original Ornare folder template: "ornare-soft" (F03/Section 8).
/// Version 2 is an original blank-canvas artwork with pill tab and calm softened contours.
/// Version 1 legacy geometry is preserved for backward compatibility with existing assignments.
/// </summary>
public static class OrnareSoftTemplate
{
    public const string TemplateId = "ornare-soft";
    public const int TemplateVersion = 2;

    public const string SvgContent =
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"" width=""256"" height=""256"">
  <!-- Rear body & Soft pill tab -->
  <rect data-role=""folder-tab"" data-ornare-role=""deep"" x=""22"" y=""32"" width=""86"" height=""44"" rx=""20"" ry=""20"" fill=""#004578"" />
  <rect data-role=""folder-back"" data-ornare-role=""deep"" x=""22"" y=""46"" width=""212"" height=""126"" rx=""26"" ry=""26"" fill=""#004578"" />

  <!-- Front body -->
  <rect data-role=""folder-front"" data-ornare-role=""base"" x=""16"" y=""82"" width=""224"" height=""144"" rx=""26"" ry=""26"" fill=""#0078D4"" />

  <!-- Semantic accent planes -->
  <rect data-role=""edge"" data-ornare-role=""light"" x=""16"" y=""82"" width=""224"" height=""48"" rx=""26"" ry=""26"" fill=""#2B88D8"" />
  <rect data-role=""highlight"" data-ornare-role=""highlight"" x=""24"" y=""84"" width=""208"" height=""6"" rx=""3"" ry=""3"" fill=""#50E6FF"" />
  <rect data-role=""shadow"" data-ornare-role=""shadow"" x=""20"" y=""212"" width=""216"" height=""14"" rx=""12"" ry=""12"" fill=""#005A9E"" />
</svg>";

    public const string LegacyV1SvgContent =
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"" width=""256"" height=""256"">
  <defs>
    <clipPath id=""ornareSoftFrontClip"">
      <rect x=""16"" y=""88"" width=""224"" height=""136"" rx=""24"" ry=""24"" />
    </clipPath>
  </defs>
  <rect data-ornare-role=""deep"" x=""24"" y=""40"" width=""88"" height=""64"" rx=""24"" ry=""24"" fill=""#004578"" />
  <rect data-ornare-role=""deep"" x=""24"" y=""48"" width=""208"" height=""120"" rx=""24"" ry=""24"" fill=""#004578"" />
  <g clip-path=""url(#ornareSoftFrontClip)"">
    <rect data-ornare-role=""base"" x=""16"" y=""88"" width=""224"" height=""136"" rx=""24"" ry=""24"" fill=""#0078D4"" />
    <rect data-ornare-role=""light"" x=""16"" y=""88"" width=""224"" height=""52"" fill=""#2B88D8"" />
    <rect data-ornare-role=""highlight"" x=""16"" y=""88"" width=""224"" height=""8"" fill=""#50E6FF"" />
    <rect data-ornare-role=""shadow"" x=""16"" y=""212"" width=""224"" height=""12"" fill=""#005A9E"" />
  </g>
</svg>";

    private static readonly Lazy<byte[]> LazyBytes = new(() => Encoding.UTF8.GetBytes(SvgContent));
    private static readonly Lazy<byte[]> LazyV1Bytes = new(() => Encoding.UTF8.GetBytes(LegacyV1SvgContent));

    public static byte[] GetTemplateBytes() => LazyBytes.Value;
    public static byte[] GetLegacyV1TemplateBytes() => LazyV1Bytes.Value;
}
