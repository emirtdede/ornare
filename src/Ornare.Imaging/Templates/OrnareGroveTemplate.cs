using System;
using System.Text;

namespace Ornare.Imaging.Templates;

/// <summary>
/// The release-approved original Ornare folder template: "ornare-grove" Version 1 (Section 8).
/// Features calm, slightly wider visual body, gentle geometric character, and clear front/back relationship.
/// </summary>
public static class OrnareGroveTemplate
{
    public const string TemplateId = "ornare-grove";
    public const int TemplateVersion = 1;

    public const string SvgContent =
@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"" width=""256"" height=""256"">
  <!-- Rear body & Organic wide tab -->
  <rect data-role=""folder-tab"" data-ornare-role=""deep"" x=""18"" y=""34"" width=""102"" height=""44"" rx=""14"" ry=""14"" fill=""#004578"" />
  <rect data-role=""folder-back"" data-ornare-role=""deep"" x=""18"" y=""50"" width=""220"" height=""122"" rx=""18"" ry=""18"" fill=""#004578"" />

  <!-- Front body with wider organic stance -->
  <rect data-role=""folder-front"" data-ornare-role=""base"" x=""16"" y=""84"" width=""224"" height=""142"" rx=""18"" ry=""18"" fill=""#0078D4"" />

  <!-- Semantic organic tones -->
  <rect data-role=""edge"" data-ornare-role=""light"" x=""16"" y=""84"" width=""224"" height=""40"" rx=""18"" ry=""18"" fill=""#2B88D8"" />
  <rect data-role=""highlight"" data-ornare-role=""highlight"" x=""20"" y=""86"" width=""216"" height=""4"" rx=""2"" ry=""2"" fill=""#50E6FF"" />
  <rect data-role=""shadow"" data-ornare-role=""shadow"" x=""16"" y=""214"" width=""224"" height=""12"" rx=""8"" ry=""8"" fill=""#005A9E"" />
</svg>";

    private static readonly Lazy<byte[]> LazyBytes = new(() => Encoding.UTF8.GetBytes(SvgContent));

    public static byte[] GetTemplateBytes() => LazyBytes.Value;
}
