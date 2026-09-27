using System;
using System.IO;
using System.Runtime.InteropServices;
using Ornare.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct2D;
using Windows.Win32.Graphics.Direct2D.Common;
using Windows.Win32.Graphics.DirectWrite;
using Windows.Win32.Graphics.Imaging;
using Windows.Win32.System.Com;

namespace Ornare.Windows;

/// <summary>
/// Production DirectWrite / Direct2D color glyph renderer for Unicode emoji (Section 25).
/// Renders full multi-codepoint grapheme clusters (ZWJ, skin-tones, flags) to a transparent PBGRA surface.
/// </summary>
public static class DirectWriteEmojiRenderer
{
    private static readonly Guid ClsidWicImagingFactory = new("317d06e8-5f24-433d-bdf7-79ce68d8abc2");

    private static readonly object RenderLock = new();

    public static bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763, 0);

    /// <summary>
    /// Renders the specified Unicode emoji grapheme cluster to raw 32-bit PBGRA pixels.
    /// </summary>
    public static unsafe OperationResult<byte[]> RenderEmojiBgra(string graphemeSequence, int targetSize)
    {
        if (string.IsNullOrWhiteSpace(graphemeSequence))
        {
            return OperationResult.Failure<byte[]>(ErrorCode.InvalidTarget, "Grapheme sequence cannot be empty.");
        }

        if (targetSize < 16 || targetSize > 1024)
        {
            return OperationResult.Failure<byte[]>(ErrorCode.InvalidTarget, $"Target size {targetSize} is out of bounds (16..1024).");
        }

        lock (RenderLock)
        {
            try
            {
                // 1. Direct2D Factory
                var d2dGuid = typeof(ID2D1Factory).GUID;
                HRESULT hr = PInvoke.D2D1CreateFactory(
                    D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED,
                    &d2dGuid,
                    null,
                    out object d2dFactoryObj);

                if (hr.Failed)
                {
                    return OperationResult.Failure<byte[]>(ErrorCode.ImageDecodeFailed, $"D2D1CreateFactory failed: 0x{hr.Value:X8}");
                }
                var d2dFactory = (ID2D1Factory)d2dFactoryObj;

                // 2. DirectWrite Factory
                var dwriteGuid = typeof(IDWriteFactory).GUID;
                hr = PInvoke.DWriteCreateFactory(
                    DWRITE_FACTORY_TYPE.DWRITE_FACTORY_TYPE_SHARED,
                    &dwriteGuid,
                    out object dwriteFactoryObj);

                if (hr.Failed)
                {
                    return OperationResult.Failure<byte[]>(ErrorCode.ImageDecodeFailed, $"DWriteCreateFactory failed: 0x{hr.Value:X8}");
                }
                var dwriteFactory = (IDWriteFactory)dwriteFactoryObj;

                // 3. WIC Imaging Factory via standard native COM activation
                var wicType = Type.GetTypeFromCLSID(ClsidWicImagingFactory);
                if (wicType == null)
                {
                    return OperationResult.Failure<byte[]>(ErrorCode.ImageDecodeFailed, "Failed to resolve WICImagingFactory CLSID.");
                }
                var wicFactory = (IWICImagingFactory)Activator.CreateInstance(wicType)!;

                // 4. Create WIC Bitmap (targetSize x targetSize, PBGRA)
                var pixelFormat = PInvoke.GUID_WICPixelFormat32bppPBGRA;
                wicFactory.CreateBitmap(
                    (uint)targetSize,
                    (uint)targetSize,
                    &pixelFormat,
                    WICBitmapCreateCacheOption.WICBitmapCacheOnDemand,
                    out IWICBitmap wicBitmap);

                // 5. Create D2D Render Target from WIC Bitmap
                var rtProps = new D2D1_RENDER_TARGET_PROPERTIES
                {
                    type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_DEFAULT,
                    pixelFormat = new D2D1_PIXEL_FORMAT
                    {
                        format = global::Windows.Win32.Graphics.Dxgi.Common.DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                        alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED
                    },
                    dpiX = 96.0f,
                    dpiY = 96.0f,
                    usage = D2D1_RENDER_TARGET_USAGE.D2D1_RENDER_TARGET_USAGE_NONE,
                    minLevel = D2D1_FEATURE_LEVEL.D2D1_FEATURE_LEVEL_DEFAULT
                };

                d2dFactory.CreateWicBitmapRenderTarget(
                    wicBitmap,
                    &rtProps,
                    out ID2D1RenderTarget renderTarget);

                // 6. Create DirectWrite Text Format for Segoe UI Emoji
                // Font size scales to fit comfortably within the frame (approx. 78% of target canvas)
                float fontSize = targetSize * 0.78f;

                dwriteFactory.CreateTextFormat(
                    "Segoe UI Emoji",
                    null,
                    DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL,
                    DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL,
                    DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL,
                    fontSize,
                    "en-US",
                    out IDWriteTextFormat textFormat);

                textFormat.SetTextAlignment(DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_CENTER);
                textFormat.SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT.DWRITE_PARAGRAPH_ALIGNMENT_CENTER);

                // 7. Create Text Layout
                fixed (char* pText = graphemeSequence)
                {
                    dwriteFactory.CreateTextLayout(
                        pText,
                        (uint)graphemeSequence.Length,
                        textFormat,
                        targetSize,
                        targetSize,
                        out IDWriteTextLayout textLayout);

                    // 8. Render with Direct2D enabling color font glyphs
                    renderTarget.BeginDraw();

                    var clearColor = new D2D1_COLOR_F { r = 0, g = 0, b = 0, a = 0 };
                    renderTarget.Clear(&clearColor);

                    var brushColor = new D2D1_COLOR_F { r = 0, g = 0, b = 0, a = 1.0f };
                    renderTarget.CreateSolidColorBrush(&brushColor, null, out ID2D1SolidColorBrush brush);

                    var origin = new D2D_POINT_2F { x = 0, y = 0 };
                    renderTarget.DrawTextLayout(
                        origin,
                        textLayout,
                        brush,
                        D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT);

                    hr = renderTarget.EndDraw(null, null);
                    if (hr.Failed)
                    {
                        return OperationResult.Failure<byte[]>(ErrorCode.ImageDecodeFailed, $"renderTarget.EndDraw failed: 0x{hr.Value:X8}");
                    }
                }

                // 9. Read back PBGRA pixels from WIC Bitmap (LockRead = 0x00000001)
                var rect = new WICRect { X = 0, Y = 0, Width = targetSize, Height = targetSize };
                wicBitmap.Lock(&rect, 0x00000001, out IWICBitmapLock bitmapLock);

                uint bufferSize;
                byte* dataPtr;
                bitmapLock.GetDataPointer(out bufferSize, &dataPtr);
                byte[] bgraBytes = new byte[bufferSize];
                Marshal.Copy((nint)dataPtr, bgraBytes, 0, (int)bufferSize);

                return OperationResult.Success(bgraBytes);
            }
            catch (Exception ex)
            {
                return OperationResult.Failure<byte[]>(ErrorCode.ImageDecodeFailed, ex.Message);
            }
        }
    }
}

/// <summary>
/// DirectWrite-backed IEmojiRasterizer implementation for Ornare.Windows (Section 25).
/// </summary>
public sealed class DirectWriteEmojiRasterizer : IEmojiRasterizer
{
    public OperationResult<byte[]> RenderEmojiBgra(string graphemeSequence, int targetSize)
    {
        return DirectWriteEmojiRenderer.RenderEmojiBgra(graphemeSequence, targetSize);
    }
}

