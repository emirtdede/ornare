using System;
using System.IO;
using System.Text;
using System.Globalization;
using ImageMagick;

namespace Ornare.AssetGenerator;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("Ornare Brand & Windows 11 Visual Asset Generator");
        Console.WriteLine("=================================================");

        string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        string brandingDir = Path.Combine(projectRoot, "production_artifacts", "branding");
        string packageImagesDir = Path.Combine(projectRoot, "src", "Ornare.Package", "Images");
        string appAssetsDir = Path.Combine(projectRoot, "src", "Ornare.App", "Assets");
        string shellExtDir = Path.Combine(projectRoot, "src", "Ornare.ShellExtension");
        string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop");
        string desktopSvgPath = Path.Combine(desktopPath, "gemini-svg.svg");
        string artifactDir = @"C:\Users\emir\.gemini\antigravity-ide\brain\899c4603-7f7b-43c5-baf3-a13ee479adbd";

        Directory.CreateDirectory(brandingDir);
        Directory.CreateDirectory(packageImagesDir);
        Directory.CreateDirectory(appAssetsDir);
        Directory.CreateDirectory(shellExtDir);

        // 1. Prepare SVG Vector Templates

        // Pure Symbol (100x100 reference plane rotated 45 deg, transparent canvas)
        string GetSymbolSvg(string fillColor) => $"""
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100%" height="100%">
          <defs>
            <mask id="gap-cut">
              <rect width="100%" height="100%" fill="white" />
              <rect x="36" y="-10" width="28" height="28" fill="black" />
              <circle cx="50" cy="18" r="14" fill="black" />
            </mask>
          </defs>
          <g transform="rotate(45 50 50)">
            <path d="M 50 8 A 42 42 0 1 0 50 92 A 42 42 0 1 0 50 8 Z M 50 28 A 22 22 0 1 1 50 72 A 22 22 0 1 1 50 28 Z"
                  fill="{fillColor}"
                  mask="url(#gap-cut)" />
            <circle cx="50" cy="18" r="10" fill="{fillColor}" />
          </g>
        </svg>
        """;

        // Parametric Circular White Badge with customizable symbol scale
        // Default scale: 1.12 (fills 81% of circle, 11px white margin, bold and ultra-crisp at 16x16/20x20)
        string GetAppIconCircularSvg(double scale = 1.12)
        {
            double offset = (120.0 - 100.0 * scale) / 2.0;
            string offsetStr = offset.ToString("0.##", CultureInfo.InvariantCulture);
            string scaleStr = scale.ToString("0.##", CultureInfo.InvariantCulture);
            string maskId = $"gap-cut-circ-{scaleStr.Replace(".", "_")}";

            return $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120" width="100%" height="100%">
              <defs>
                <mask id="{maskId}">
                  <rect width="100%" height="100%" fill="white" />
                  <rect x="36" y="-10" width="28" height="28" fill="black" />
                  <circle cx="50" cy="18" r="14" fill="black" />
                </mask>
              </defs>
              <circle cx="60" cy="60" r="58" fill="#FFFFFF" stroke="#E4E4E7" stroke-width="1.5" />
              <g transform="translate({offsetStr}, {offsetStr}) scale({scaleStr}) rotate(45 50 50)">
                <path d="M 50 8 A 42 42 0 1 0 50 92 A 42 42 0 1 0 50 8 Z M 50 28 A 22 22 0 1 1 50 72 A 22 22 0 1 1 50 28 Z"
                      fill="#09090B"
                      mask="url(#{maskId})" />
                <circle cx="50" cy="18" r="10" fill="#09090B" />
              </g>
            </svg>
            """;
        }

        // Unplated Symbol on transparent canvas (for Windows 11 Taskbar alternate form)
        string GetUnplatedSvg(string color) => $"""
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100%" height="100%">
          <defs>
            <mask id="gap-cut-unp">
              <rect width="100%" height="100%" fill="white" />
              <rect x="36" y="-10" width="28" height="28" fill="black" />
              <circle cx="50" cy="18" r="14" fill="black" />
            </mask>
          </defs>
          <g transform="translate(8, 8) scale(0.84) rotate(45 50 50)">
            <path d="M 50 8 A 42 42 0 1 0 50 92 A 42 42 0 1 0 50 8 Z M 50 28 A 22 22 0 1 1 50 72 A 22 22 0 1 1 50 28 Z"
                  fill="{color}"
                  mask="url(#gap-cut-unp)" />
            <circle cx="50" cy="18" r="10" fill="{color}" />
          </g>
        </svg>
        """;

        // Helper to rasterize SVG to exact PNG dimensions with strict transparent background
        void RasterizeSvgToPng(string svgContent, int width, int height, string outputPath)
        {
            float aspect = (float)width / height;
            uint renderW, renderH;
            if (width >= height)
            {
                renderW = Math.Max(512, (uint)width * 2);
                renderH = (uint)Math.Round(renderW / aspect);
            }
            else
            {
                renderH = Math.Max(512, (uint)height * 2);
                renderW = (uint)Math.Round(renderH * aspect);
            }

            var readSettings = new MagickReadSettings
            {
                Format = MagickFormat.Svg,
                Width = renderW,
                Height = renderH,
                BackgroundColor = MagickColors.Transparent
            };

            using var image = new MagickImage(Encoding.UTF8.GetBytes(svgContent), readSettings);
            image.BackgroundColor = MagickColors.Transparent;
            image.FilterType = FilterType.Lanczos;
            image.Resize((uint)width, (uint)height);
            image.Format = MagickFormat.Png;
            image.Write(outputPath);
            Console.WriteLine($"  -> Generated {Path.GetFileName(outputPath)} ({image.Width}x{image.Height})");
        }

        // PREVIEW MODE: Generate side-by-side comparison image of different scale options
        if (args.Length > 0 && args[0].Equals("preview", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("\nGenerating Logo Size & Proportion Comparison Preview...");

            string previewSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1060 620" width="1060" height="620">
              <defs>
                <mask id="m-070">
                  <rect width="100%" height="100%" fill="white" />
                  <rect x="36" y="-10" width="28" height="28" fill="black" />
                  <circle cx="50" cy="18" r="14" fill="black" />
                </mask>
                <mask id="m-100">
                  <rect width="100%" height="100%" fill="white" />
                  <rect x="36" y="-10" width="28" height="28" fill="black" />
                  <circle cx="50" cy="18" r="14" fill="black" />
                </mask>
                <mask id="m-112">
                  <rect width="100%" height="100%" fill="white" />
                  <rect x="36" y="-10" width="28" height="28" fill="black" />
                  <circle cx="50" cy="18" r="14" fill="black" />
                </mask>
                <mask id="m-120">
                  <rect width="100%" height="100%" fill="white" />
                  <rect x="36" y="-10" width="28" height="28" fill="black" />
                  <circle cx="50" cy="18" r="14" fill="black" />
                </mask>

                <!-- Badge 0.70 (Old/Current Small) -->
                <g id="badge-070">
                  <circle cx="60" cy="60" r="58" fill="#FFFFFF" stroke="#E4E4E7" stroke-width="1.5" />
                  <g transform="translate(25, 25) scale(0.70) rotate(45 50 50)">
                    <path d="M 50 8 A 42 42 0 1 0 50 92 A 42 42 0 1 0 50 8 Z M 50 28 A 22 22 0 1 1 50 72 A 22 22 0 1 1 50 28 Z" fill="#09090B" mask="url(#m-070)" />
                    <circle cx="50" cy="18" r="10" fill="#09090B" />
                  </g>
                </g>

                <!-- Badge 1.00 (Option 1) -->
                <g id="badge-100">
                  <circle cx="60" cy="60" r="58" fill="#FFFFFF" stroke="#E4E4E7" stroke-width="1.5" />
                  <g transform="translate(10, 10) scale(1.00) rotate(45 50 50)">
                    <path d="M 50 8 A 42 42 0 1 0 50 92 A 42 42 0 1 0 50 8 Z M 50 28 A 22 22 0 1 1 50 72 A 22 22 0 1 1 50 28 Z" fill="#09090B" mask="url(#m-100)" />
                    <circle cx="50" cy="18" r="10" fill="#09090B" />
                  </g>
                </g>

                <!-- Badge 1.12 (Option 2 - Recommended) -->
                <g id="badge-112">
                  <circle cx="60" cy="60" r="58" fill="#FFFFFF" stroke="#E4E4E7" stroke-width="1.5" />
                  <g transform="translate(4, 4) scale(1.12) rotate(45 50 50)">
                    <path d="M 50 8 A 42 42 0 1 0 50 92 A 42 42 0 1 0 50 8 Z M 50 28 A 22 22 0 1 1 50 72 A 22 22 0 1 1 50 28 Z" fill="#09090B" mask="url(#m-112)" />
                    <circle cx="50" cy="18" r="10" fill="#09090B" />
                  </g>
                </g>

                <!-- Badge 1.20 (Option 3 - Max Impact) -->
                <g id="badge-120">
                  <circle cx="60" cy="60" r="58" fill="#FFFFFF" stroke="#E4E4E7" stroke-width="1.5" />
                  <g transform="translate(0, 0) scale(1.20) rotate(45 50 50)">
                    <path d="M 50 8 A 42 42 0 1 0 50 92 A 42 42 0 1 0 50 8 Z M 50 28 A 22 22 0 1 1 50 72 A 22 22 0 1 1 50 28 Z" fill="#09090B" mask="url(#m-120)" />
                    <circle cx="50" cy="18" r="10" fill="#09090B" />
                  </g>
                </g>
              </defs>

              <!-- Windows 11 Dark Mode Canvas (#1F1F1F) -->
              <rect width="1060" height="620" fill="#18181B" rx="16" />

              <!-- Header -->
              <text x="50" y="55" font-family="'Segoe UI Variable Text', -apple-system, sans-serif" font-size="24" font-weight="700" fill="#FFFFFF">Ornare Logo Boyut ve Doluluk Karşılaştırması</text>
              <text x="50" y="82" font-family="'Segoe UI Variable Text', -apple-system, sans-serif" font-size="14" font-weight="400" fill="#A1A1AA">Dairesel beyaz rozet içinde logonun farklı ölçek ve görünürlük oranları (Gerçek Windows 11 Koyu Teması)</text>

              <!-- COLUMN 1: MEVCUT (Scale 0.70) -->
              <g transform="translate(50, 115)">
                <rect width="220" height="460" rx="12" fill="#202023" stroke="#27272A" stroke-width="1" />
                <text x="110" y="32" font-family="'Segoe UI Variable Text', sans-serif" font-size="15" font-weight="600" fill="#EF4444" text-anchor="middle">Mevcut (Küçük)</text>
                <text x="110" y="52" font-family="'Segoe UI Variable Text', sans-serif" font-size="12" fill="#71717A" text-anchor="middle">%52 Doluluk (Fazla Beyazlık)</text>
                
                <!-- 100x100 Preview -->
                <rect x="60" y="75" width="100" height="100" rx="8" fill="#141416" />
                <g transform="translate(50, 65) scale(1)">
                  <use href="#badge-070" width="120" height="120" />
                </g>
                <text x="110" y="195" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#71717A" text-anchor="middle">Büyük İkon (100px)</text>

                <!-- 32x32 Taskbar Preview -->
                <rect x="30" y="225" width="160" height="50" rx="6" fill="#141416" />
                <g transform="translate(42, 234) scale(0.266)">
                  <use href="#badge-070" />
                </g>
                <text x="86" y="256" font-family="'Segoe UI Variable Text', sans-serif" font-size="12" font-weight="500" fill="#E4E4E7">Görev Çubuğu (32px)</text>

                <!-- Titlebar Simulation (22px logo) -->
                <rect x="20" y="305" width="180" height="120" rx="6" fill="#141416" stroke="#27272A" stroke-width="1" />
                <!-- Titlebar bar -->
                <rect x="20" y="305" width="180" height="34" rx="6" fill="#27272A" />
                <g transform="translate(28, 311) scale(0.183)">
                  <use href="#badge-070" />
                </g>
                <text x="56" y="327" font-family="'Segoe UI Variable Text', sans-serif" font-size="12" font-weight="600" fill="#FFFFFF">Ornare</text>
                <text x="100" y="327" font-family="'Segoe UI Variable Text', sans-serif" font-size="10" fill="#71717A">Folder Icons</text>
                <text x="110" y="365" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#EF4444" text-anchor="middle">Başlık Çubuğu (22px)</text>
                <text x="110" y="385" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#71717A" text-anchor="middle">Logo çok ince kalıyor,</text>
                <text x="110" y="403" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#71717A" text-anchor="middle">detaylar kayboluyor.</text>
              </g>

              <!-- COLUMN 2: SEÇENEK 1 (Scale 1.00) -->
              <g transform="translate(295, 115)">
                <rect width="220" height="460" rx="12" fill="#202023" stroke="#27272A" stroke-width="1" />
                <text x="110" y="32" font-family="'Segoe UI Variable Text', sans-serif" font-size="15" font-weight="600" fill="#38BDF8" text-anchor="middle">Seçenek 1 (Geniş)</text>
                <text x="110" y="52" font-family="'Segoe UI Variable Text', sans-serif" font-size="12" fill="#71717A" text-anchor="middle">%72 Doluluk (16px Beyaz Çerçeve)</text>
                
                <!-- 100x100 Preview -->
                <rect x="60" y="75" width="100" height="100" rx="8" fill="#141416" />
                <g transform="translate(50, 65) scale(1)">
                  <use href="#badge-100" width="120" height="120" />
                </g>
                <text x="110" y="195" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#71717A" text-anchor="middle">Büyük İkon (100px)</text>

                <!-- 32x32 Taskbar Preview -->
                <rect x="30" y="225" width="160" height="50" rx="6" fill="#141416" />
                <g transform="translate(42, 234) scale(0.266)">
                  <use href="#badge-100" />
                </g>
                <text x="86" y="256" font-family="'Segoe UI Variable Text', sans-serif" font-size="12" font-weight="500" fill="#E4E4E7">Görev Çubuğu (32px)</text>

                <!-- Titlebar Simulation (22px logo) -->
                <rect x="20" y="305" width="180" height="120" rx="6" fill="#141416" stroke="#27272A" stroke-width="1" />
                <rect x="20" y="305" width="180" height="34" rx="6" fill="#27272A" />
                <g transform="translate(28, 311) scale(0.183)">
                  <use href="#badge-100" />
                </g>
                <text x="56" y="327" font-family="'Segoe UI Variable Text', sans-serif" font-size="12" font-weight="600" fill="#FFFFFF">Ornare</text>
                <text x="100" y="327" font-family="'Segoe UI Variable Text', sans-serif" font-size="10" fill="#71717A">Folder Icons</text>
                <text x="110" y="365" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#38BDF8" text-anchor="middle">Başlık Çubuğu (22px)</text>
                <text x="110" y="385" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#A1A1AA" text-anchor="middle">Dengeli ve temiz,</text>
                <text x="110" y="403" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#A1A1AA" text-anchor="middle">orta derecede büyütülmüş.</text>
              </g>

              <!-- COLUMN 3: SEÇENEK 2 (Scale 1.12 - ÖNERİLEN) -->
              <g transform="translate(540, 115)">
                <rect width="225" height="460" rx="12" fill="#202025" stroke="#10B981" stroke-width="2" />
                <!-- Badge tag -->
                <rect x="62" y="-12" width="100" height="24" rx="12" fill="#10B981" />
                <text x="112" y="4" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" font-weight="700" fill="#FFFFFF" text-anchor="middle">ÖNERİLEN</text>

                <text x="112" y="34" font-family="'Segoe UI Variable Text', sans-serif" font-size="15" font-weight="700" fill="#10B981" text-anchor="middle">Seçenek 2 (Net &amp; Belirgin)</text>
                <text x="112" y="54" font-family="'Segoe UI Variable Text', sans-serif" font-size="12" fill="#71717A" text-anchor="middle">%81 Doluluk (11px Beyaz Çerçeve)</text>
                
                <!-- 100x100 Preview -->
                <rect x="62" y="75" width="100" height="100" rx="8" fill="#141416" />
                <g transform="translate(52, 65) scale(1)">
                  <use href="#badge-112" width="120" height="120" />
                </g>
                <text x="112" y="195" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#10B981" font-weight="600" text-anchor="middle">Büyük İkon (100px)</text>

                <!-- 32x32 Taskbar Preview -->
                <rect x="32" y="225" width="160" height="50" rx="6" fill="#141416" />
                <g transform="translate(44, 234) scale(0.266)">
                  <use href="#badge-112" />
                </g>
                <text x="88" y="256" font-family="'Segoe UI Variable Text', sans-serif" font-size="12" font-weight="500" fill="#E4E4E7">Görev Çubuğu (32px)</text>

                <!-- Titlebar Simulation (22px logo) -->
                <rect x="22" y="305" width="180" height="120" rx="6" fill="#141416" stroke="#27272A" stroke-width="1" />
                <rect x="22" y="305" width="180" height="34" rx="6" fill="#27272A" />
                <g transform="translate(30, 311) scale(0.183)">
                  <use href="#badge-112" />
                </g>
                <text x="58" y="327" font-family="'Segoe UI Variable Text', sans-serif" font-size="12" font-weight="600" fill="#FFFFFF">Ornare</text>
                <text x="102" y="327" font-family="'Segoe UI Variable Text', sans-serif" font-size="10" fill="#71717A">Folder Icons</text>
                <text x="112" y="365" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#10B981" font-weight="600" text-anchor="middle">Başlık Çubuğu (22px)</text>
                <text x="112" y="385" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#FFFFFF" text-anchor="middle">Kilit taşı ve dairesel boşluk</text>
                <text x="112" y="403" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#FFFFFF" font-weight="600" text-anchor="middle">küçükken bile net seçiliyor.</text>
              </g>

              <!-- COLUMN 4: SEÇENEK 3 (Scale 1.20) -->
              <g transform="translate(790, 115)">
                <rect width="220" height="460" rx="12" fill="#202023" stroke="#27272A" stroke-width="1" />
                <text x="110" y="32" font-family="'Segoe UI Variable Text', sans-serif" font-size="15" font-weight="600" fill="#F59E0B" text-anchor="middle">Seçenek 3 (Maksimum)</text>
                <text x="110" y="52" font-family="'Segoe UI Variable Text', sans-serif" font-size="12" fill="#71717A" text-anchor="middle">%87 Doluluk (7.5px Beyaz Çerçeve)</text>
                
                <!-- 100x100 Preview -->
                <rect x="60" y="75" width="100" height="100" rx="8" fill="#141416" />
                <g transform="translate(50, 65) scale(1)">
                  <use href="#badge-120" width="120" height="120" />
                </g>
                <text x="110" y="195" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#71717A" text-anchor="middle">Büyük İkon (100px)</text>

                <!-- 32x32 Taskbar Preview -->
                <rect x="30" y="225" width="160" height="50" rx="6" fill="#141416" />
                <g transform="translate(42, 234) scale(0.266)">
                  <use href="#badge-120" />
                </g>
                <text x="86" y="256" font-family="'Segoe UI Variable Text', sans-serif" font-size="12" font-weight="500" fill="#E4E4E7">Görev Çubuğu (32px)</text>

                <!-- Titlebar Simulation (22px logo) -->
                <rect x="20" y="305" width="180" height="120" rx="6" fill="#141416" stroke="#27272A" stroke-width="1" />
                <rect x="20" y="305" width="180" height="34" rx="6" fill="#27272A" />
                <g transform="translate(28, 311) scale(0.183)">
                  <use href="#badge-120" />
                </g>
                <text x="56" y="327" font-family="'Segoe UI Variable Text', sans-serif" font-size="12" font-weight="600" fill="#FFFFFF">Ornare</text>
                <text x="100" y="327" font-family="'Segoe UI Variable Text', sans-serif" font-size="10" fill="#71717A">Folder Icons</text>
                <text x="110" y="365" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#F59E0B" text-anchor="middle">Başlık Çubuğu (22px)</text>
                <text x="110" y="385" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#A1A1AA" text-anchor="middle">Maksimum büyüklük,</text>
                <text x="110" y="403" font-family="'Segoe UI Variable Text', sans-serif" font-size="11" fill="#A1A1AA" text-anchor="middle">beyaz kenar çok ince.</text>
              </g>
            </svg>
            """;

            string outArtifact = Path.Combine(artifactDir, "ornare-logo-size-options.png");
            string outBranding = Path.Combine(brandingDir, "ornare-logo-size-options.png");

            var readSettings = new MagickReadSettings
            {
                Format = MagickFormat.Svg,
                Width = 2120,
                Height = 1240,
                BackgroundColor = MagickColors.Transparent
            };
            using var img = new MagickImage(Encoding.UTF8.GetBytes(previewSvg), readSettings);
            img.FilterType = FilterType.Lanczos;
            img.Resize(1060, 620);
            img.Format = MagickFormat.Png;
            img.Write(outArtifact);
            img.Write(outBranding);

            Console.WriteLine($"[✓] Preview generated successfully:\n  - {outArtifact}\n  - {outBranding}");
            return;
        }

        // FULL GENERATION (will run after user approves)
        double chosenScale = 1.12; // default enlarged scale
        string circularBadgeSvg = GetAppIconCircularSvg(chosenScale);
        string unplatedDarkSvg = GetUnplatedSvg("#FFFFFF");
        string unplatedLightSvg = GetUnplatedSvg("#09090B");

        // Master Presentation SVG with Ornare wordmark & Circular Icon
        string GetMasterPresentationSvg() => $"""
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 800 600" width="100%" height="100%">
          <defs>
            <g id="ornare-symbol">
              <mask id="gap-cut-pres">
                <rect width="100%" height="100%" fill="white" />
                <rect x="36" y="-10" width="28" height="28" fill="black" />
                <circle cx="50" cy="18" r="14" fill="black" />
              </mask>
              <path d="M 50 8 A 42 42 0 1 0 50 92 A 42 42 0 1 0 50 8 Z M 50 28 A 22 22 0 1 1 50 72 A 22 22 0 1 1 50 28 Z" fill="currentColor" mask="url(#gap-cut-pres)" />
              <circle cx="50" cy="18" r="10" fill="currentColor" />
            </g>
          </defs>
          <rect width="800" height="600" fill="#F8F9FA" />
          <g transform="translate(130, 150)">
            <g transform="translate(0, -10) scale(1.15) rotate(45 50 50)" color="#09090B">
              <use href="#ornare-symbol" />
            </g>
            <text x="145" y="68" font-family="system-ui, -apple-system, sans-serif" font-size="64" font-weight="700" fill="#09090B" letter-spacing="-0.03em">Ornare</text>
            <text x="150" y="94" font-family="system-ui, -apple-system, sans-serif" font-size="12" font-weight="600" fill="#71717A" letter-spacing="0.35em">FOLDER ICONS</text>
          </g>
          <line x1="130" y1="310" x2="670" y2="310" stroke="#E5E7EB" stroke-width="1" />
          <g transform="translate(190, 360)">
            <circle cx="60" cy="60" r="58" fill="#FFFFFF" stroke="#E4E4E7" stroke-width="1.5" />
            <g transform="translate(4, 4) scale(1.12) rotate(45 50 50)" color="#09090B">
              <use href="#ornare-symbol" />
            </g>
            <text x="60" y="150" font-family="system-ui, -apple-system, sans-serif" font-size="12" font-weight="500" fill="#71717A" text-anchor="middle">Dairesel Rozet İkon</text>
          </g>
          <g transform="translate(390, 360)">
            <rect width="220" height="120" rx="20" fill="#FFFFFF" stroke="#E5E7EB" stroke-width="2" />
            <g transform="translate(60, 10) scale(1)">
              <g stroke="#E5E7EB" stroke-width="1" fill="none">
                <line x1="50" y1="-10" x2="50" y2="110" />
                <line x1="-10" y1="50" x2="110" y2="50" />
                <circle cx="50" cy="50" r="42" stroke-dasharray="2 4" />
              </g>
              <g stroke="#A1A1AA" stroke-width="1" fill="#F4F4F5">
                <path d="M 36 -5 L 36 18 A 14 14 0 0 0 64 18 L 64 -5 Z" stroke-dasharray="2 2" />
              </g>
              <g color="none" stroke="#09090B" stroke-width="1.5">
                <use href="#ornare-symbol" />
              </g>
              <circle cx="50" cy="18" r="2.5" fill="#EF4444" />
              <circle cx="50" cy="50" r="3" fill="#09090B" />
              <line x1="50" y1="18" x2="50" y2="50" stroke="#EF4444" stroke-width="1" stroke-dasharray="1 2" />
            </g>
            <text x="110" y="150" font-family="system-ui, -apple-system, sans-serif" font-size="12" font-weight="500" fill="#71717A" text-anchor="middle">Formül: R=42 &amp; Eşmerkezli Kesit</text>
          </g>
        </svg>
        """;

        // Save Master SVGs
        File.WriteAllText(Path.Combine(brandingDir, "ornare-presentation.svg"), GetMasterPresentationSvg(), Encoding.UTF8);
        File.WriteAllText(Path.Combine(brandingDir, "ornare-symbol.svg"), GetSymbolSvg("currentColor"), Encoding.UTF8);
        File.WriteAllText(Path.Combine(brandingDir, "ornare-icon-circular.svg"), circularBadgeSvg, Encoding.UTF8);
        File.WriteAllText(Path.Combine(brandingDir, "ornare-symbol-unplated.svg"), GetUnplatedSvg("#FFFFFF"), Encoding.UTF8);
        File.WriteAllText(Path.Combine(brandingDir, "ornare-symbol-lightunplated.svg"), GetUnplatedSvg("#09090B"), Encoding.UTF8);

        if (File.Exists(desktopSvgPath))
        {
            File.WriteAllText(desktopSvgPath, GetMasterPresentationSvg(), Encoding.UTF8);
            Console.WriteLine($"[✓] Updated Desktop file: {desktopSvgPath} with Ornare branding.");
        }

        Console.WriteLine("\n[1/4] Generating Square44x44Logo variants (Taskbar, App List, Alt-Tab)...");
        int[] sq44Scales = [100, 125, 150, 200, 400];
        foreach (int scale in sq44Scales)
        {
            int size = (int)Math.Round(44.0 * scale / 100.0);
            string filename = scale == 100 ? "Square44x44Logo.png" : $"Square44x44Logo.scale-{scale}.png";
            RasterizeSvgToPng(circularBadgeSvg, size, size, Path.Combine(packageImagesDir, filename));
            if (scale == 100)
            {
                RasterizeSvgToPng(circularBadgeSvg, size, size, Path.Combine(packageImagesDir, "Square44x44Logo.scale-100.png"));
            }
        }

        int[] targetSizes = [16, 20, 24, 30, 32, 36, 40, 48, 64, 256];
        foreach (int target in targetSizes)
        {
            RasterizeSvgToPng(circularBadgeSvg, target, target, Path.Combine(packageImagesDir, $"Square44x44Logo.targetsize-{target}.png"));
            RasterizeSvgToPng(circularBadgeSvg, target, target, Path.Combine(packageImagesDir, $"Square44x44Logo.targetsize-{target}_altform-unplated.png"));
            RasterizeSvgToPng(circularBadgeSvg, target, target, Path.Combine(packageImagesDir, $"Square44x44Logo.targetsize-{target}_altform-lightunplated.png"));
        }

        Console.WriteLine("\n[2/4] Generating Square150x150Logo variants (Start Menu Tiles)...");
        int[] sq150Scales = [100, 125, 150, 200, 400];
        foreach (int scale in sq150Scales)
        {
            int size = (int)Math.Round(150.0 * scale / 100.0);
            string filename = scale == 100 ? "Square150x150Logo.png" : $"Square150x150Logo.scale-{scale}.png";
            RasterizeSvgToPng(circularBadgeSvg, size, size, Path.Combine(packageImagesDir, filename));
            if (scale == 100)
            {
                RasterizeSvgToPng(circularBadgeSvg, size, size, Path.Combine(packageImagesDir, "Square150x150Logo.scale-100.png"));
            }
        }

        Console.WriteLine("\n[3/4] Generating StoreLogo & SplashScreen variants...");
        int[] storeScales = [100, 125, 150, 200, 400];
        foreach (int scale in storeScales)
        {
            int size = (int)Math.Round(50.0 * scale / 100.0);
            string filename = scale == 100 ? "StoreLogo.png" : $"StoreLogo.scale-{scale}.png";
            RasterizeSvgToPng(circularBadgeSvg, size, size, Path.Combine(packageImagesDir, filename));
            if (scale == 100)
            {
                RasterizeSvgToPng(circularBadgeSvg, size, size, Path.Combine(packageImagesDir, "StoreLogo.scale-100.png"));
            }
        }

        // SplashScreen SVG
        string splashSvg = $"""
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 620 300" width="100%" height="100%">
          <defs>
            <mask id="gap-cut-splash">
              <rect width="100%" height="100%" fill="white" />
              <rect x="36" y="-10" width="28" height="28" fill="black" />
              <circle cx="50" cy="18" r="14" fill="black" />
            </mask>
          </defs>
          <g transform="translate(310, 150)">
            <circle cx="0" cy="0" r="58" fill="#FFFFFF" stroke="#E4E4E7" stroke-width="1.5" />
            <g transform="translate(-56, -56) scale(1.12) rotate(45 50 50)">
              <path d="M 50 8 A 42 42 0 1 0 50 92 A 42 42 0 1 0 50 8 Z M 50 28 A 22 22 0 1 1 50 72 A 22 22 0 1 1 50 28 Z"
                    fill="#09090B"
                    mask="url(#gap-cut-splash)" />
              <circle cx="50" cy="18" r="10" fill="#09090B" />
            </g>
          </g>
        </svg>
        """;

        int[] splashScales = [100, 125, 150, 200, 400];
        foreach (int scale in splashScales)
        {
            int w = (int)Math.Round(620.0 * scale / 100.0);
            int h = (int)Math.Round(300.0 * scale / 100.0);
            string filename = scale == 100 ? "SplashScreen.png" : $"SplashScreen.scale-{scale}.png";
            RasterizeSvgToPng(splashSvg, w, h, Path.Combine(packageImagesDir, filename));
            if (scale == 100)
            {
                RasterizeSvgToPng(splashSvg, w, h, Path.Combine(packageImagesDir, "SplashScreen.scale-100.png"));
            }
        }

        // Also generate in-app UI logos
        RasterizeSvgToPng(circularBadgeSvg, 64, 64, Path.Combine(appAssetsDir, "OrnareLogo.png"));
        RasterizeSvgToPng(unplatedDarkSvg, 64, 64, Path.Combine(appAssetsDir, "OrnareLogoUnplated.png"));

        Console.WriteLine("\n[4/4] Building multi-resolution app.ico (16 to 256px frames)...");
        var icoMasterSettings = new MagickReadSettings
        {
            Format = MagickFormat.Svg,
            Width = 256,
            Height = 256,
            BackgroundColor = MagickColors.Transparent
        };
        using var masterIcoImg = new MagickImage(Encoding.UTF8.GetBytes(circularBadgeSvg), icoMasterSettings);
        masterIcoImg.BackgroundColor = MagickColors.Transparent;
        masterIcoImg.Format = MagickFormat.Png;

        int[] icoSizes = [16, 20, 24, 30, 32, 40, 48, 64, 96, 128, 256];
        using var icoFrames = new MagickImageCollection();
        foreach (int size in icoSizes)
        {
            var frame = (MagickImage)masterIcoImg.Clone();
            frame.BackgroundColor = MagickColors.Transparent;
            frame.FilterType = FilterType.Lanczos;
            frame.Resize((uint)size, (uint)size);
            frame.Format = MagickFormat.Ico;
            icoFrames.Add(frame);
        }

        string appIcoPath = Path.Combine(appAssetsDir, "app.ico");
        string packageIcoPath = Path.Combine(packageImagesDir, "app.ico");
        string brandingIcoPath = Path.Combine(brandingDir, "app.ico");
        string shellExtIcoPath = Path.Combine(shellExtDir, "app.ico");

        icoFrames.Write(appIcoPath, MagickFormat.Ico);
        icoFrames.Write(packageIcoPath, MagickFormat.Ico);
        icoFrames.Write(brandingIcoPath, MagickFormat.Ico);
        icoFrames.Write(shellExtIcoPath, MagickFormat.Ico);
        Console.WriteLine($"[✓] Multi-resolution ICO written to:\n  - {appIcoPath}\n  - {packageIcoPath}\n  - {brandingIcoPath}\n  - {shellExtIcoPath}");

        Console.WriteLine("\n[✓] All assets generated successfully with circular white badge!");
    }
}
