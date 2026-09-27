using System;
using System.Text;

namespace Ornare.Imaging.Templates;

/// <summary>
/// The release-approved original Ornare folder template: "ornare-flat" (F03/Section 8).
/// Version 2 is an original blank-canvas artwork with crisp angular tab and minimalist semantic planes.
/// Version 1 legacy geometry is preserved for backward compatibility with existing assignments.
/// </summary>
public static class OrnareFlatTemplate
{
    public const string TemplateId = "ornare-flat";
    public const int TemplateVersion = 2;

    public const string SvgContent =
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"" width=""256"" height=""256"">
  <!-- Rear body & Crisp angular tab -->
  <rect data-role=""folder-tab"" data-ornare-role=""deep"" x=""24"" y=""38"" width=""84"" height=""38"" rx=""6"" ry=""6"" fill=""#004578"" />
  <rect data-role=""folder-back"" data-ornare-role=""deep"" x=""24"" y=""52"" width=""208"" height=""120"" rx=""8"" ry=""8"" fill=""#004578"" />

  <!-- Front body -->
  <rect data-role=""folder-front"" data-ornare-role=""base"" x=""16"" y=""86"" width=""224"" height=""138"" rx=""8"" ry=""8"" fill=""#0078D4"" />

  <!-- Semantic accent planes -->
  <rect data-role=""edge"" data-ornare-role=""light"" x=""16"" y=""86"" width=""224"" height=""28"" rx=""8"" ry=""8"" fill=""#2B88D8"" />
  <rect data-role=""highlight"" data-ornare-role=""highlight"" x=""18"" y=""87"" width=""220"" height=""2"" fill=""#50E6FF"" />
  <rect data-role=""shadow"" data-ornare-role=""shadow"" x=""16"" y=""218"" width=""224"" height=""6"" rx=""4"" ry=""4"" fill=""#005A9E"" />
</svg>";

    public const string LegacyV1SvgContent =
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"" width=""256"" height=""256"">
  <defs>
    <clipPath id=""ornareFlatFrontClip"">
      <rect x=""16"" y=""88"" width=""224"" height=""136"" rx=""12"" ry=""12"" />
    </clipPath>
  </defs>
  <rect data-ornare-role=""deep"" x=""24"" y=""40"" width=""88"" height=""64"" rx=""12"" ry=""12"" fill=""#004578"" />
  <rect data-ornare-role=""deep"" x=""24"" y=""48"" width=""208"" height=""120"" rx=""12"" ry=""12"" fill=""#004578"" />
  <g clip-path=""url(#ornareFlatFrontClip)"">
    <rect data-ornare-role=""base"" x=""16"" y=""88"" width=""224"" height=""136"" rx=""12"" ry=""12"" fill=""#0078D4"" />
    <rect data-ornare-role=""light"" x=""16"" y=""88"" width=""224"" height=""32"" fill=""#2B88D8"" />
    <rect data-ornare-role=""highlight"" x=""16"" y=""88"" width=""224"" height=""4"" fill=""#50E6FF"" />
    <rect data-ornare-role=""shadow"" x=""16"" y=""216"" width=""224"" height=""8"" fill=""#005A9E"" />
  </g>
</svg>";

    private static readonly Lazy<byte[]> LazyBytes = new(() => Encoding.UTF8.GetBytes(SvgContent));
    private static readonly Lazy<byte[]> LazyV1Bytes = new(() => Encoding.UTF8.GetBytes(LegacyV1SvgContent));

    public static byte[] GetTemplateBytes() => LazyBytes.Value;
    public static byte[] GetLegacyV1TemplateBytes() => LazyV1Bytes.Value;
}
