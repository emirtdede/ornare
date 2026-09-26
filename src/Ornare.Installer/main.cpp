#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <windowsx.h>
#include <commctrl.h>
#include <dwmapi.h>
#include <uxtheme.h>
#include <shlobj.h>
#include <shellapi.h>
#include <wrl/client.h>
#include <wincrypt.h>
#include <string>
#include <vector>
#include "resource.h"

#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "dwmapi.lib")
#pragma comment(lib, "uxtheme.lib")
#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "advapi32.lib")

#define WM_INSTALL_PROGRESS (WM_USER + 101)
#define WM_INSTALL_COMPLETE (WM_USER + 102)

enum class InstallState
{
    Ready,
    Installing,
    Completed,
    Failed
};

struct AppColors
{
    COLORREF bg;
    COLORREF cardBg;
    COLORREF textPrimary;
    COLORREF textSecondary;
    COLORREF textAccent;
    COLORREF border;
    HBRUSH hbrBg;
    HBRUSH hbrCard;
};

static HINSTANCE g_hInstance = nullptr;
static HWND g_hWnd = nullptr;
static HWND g_hwndProgress = nullptr;
static HWND g_hwndBtnInstall = nullptr;
static HWND g_hwndBtnCancel = nullptr;
static HWND g_hwndBtnLaunch = nullptr;
static HWND g_hwndChkShortcut = nullptr;

static HICON g_hAppIcon = nullptr;
static HFONT g_hFontTitle = nullptr;
static HFONT g_hFontSubtitle = nullptr;
static HFONT g_hFontBody = nullptr;
static HFONT g_hFontStatus = nullptr;

static InstallState g_state = InstallState::Ready;
static std::wstring g_statusText;
static bool g_isTurkish = false;
static bool g_isDarkMode = true;
static AppColors g_colors;

static void DetectSystemSettings()
{
    LANGID lang = GetUserDefaultUILanguage();
    g_isTurkish = (PRIMARYLANGID(lang) == LANG_TURKISH);

    DWORD lightTheme = 0;
    DWORD size = sizeof(lightTheme);
    HKEY hKey = nullptr;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", 0, KEY_READ, &hKey) == ERROR_SUCCESS)
    {
        RegQueryValueExW(hKey, L"AppsUseLightTheme", nullptr, nullptr, reinterpret_cast<LPBYTE>(&lightTheme), &size);
        RegCloseKey(hKey);
    }
    g_isDarkMode = (lightTheme == 0);

    if (g_isDarkMode)
    {
        g_colors.bg = RGB(32, 32, 32);
        g_colors.cardBg = RGB(42, 42, 42);
        g_colors.textPrimary = RGB(255, 255, 255);
        g_colors.textSecondary = RGB(170, 170, 170);
        g_colors.textAccent = RGB(96, 205, 255);
        g_colors.border = RGB(60, 60, 60);
    }
    else
    {
        g_colors.bg = RGB(243, 243, 243);
        g_colors.cardBg = RGB(255, 255, 255);
        g_colors.textPrimary = RGB(24, 24, 24);
        g_colors.textSecondary = RGB(90, 90, 90);
        g_colors.textAccent = RGB(0, 103, 192);
        g_colors.border = RGB(225, 225, 225);
    }

    g_colors.hbrBg = CreateSolidBrush(g_colors.bg);
    g_colors.hbrCard = CreateSolidBrush(g_colors.cardBg);
}

static bool ExtractResource(int resId, const std::wstring& outputPath)
{
    HRSRC hRes = FindResourceW(g_hInstance, MAKEINTRESOURCEW(resId), RT_RCDATA);
    if (!hRes) return false;

    HGLOBAL hMem = LoadResource(g_hInstance, hRes);
    if (!hMem) return false;

    DWORD size = SizeofResource(g_hInstance, hRes);
    const void* pData = LockResource(hMem);
    if (!pData || size == 0) return false;

    HANDLE hFile = CreateFileW(outputPath.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (hFile == INVALID_HANDLE_VALUE) return false;

    DWORD written = 0;
    BOOL ok = WriteFile(hFile, pData, size, &written, nullptr);
    CloseHandle(hFile);

    return ok && (written == size);
}

static bool InstallCertificateFromResource(int resId)
{
    HRSRC hRes = FindResourceW(g_hInstance, MAKEINTRESOURCEW(resId), RT_RCDATA);
    if (!hRes) return false;

    HGLOBAL hMem = LoadResource(g_hInstance, hRes);
    if (!hMem) return false;

    DWORD size = SizeofResource(g_hInstance, hRes);
    const BYTE* pData = static_cast<const BYTE*>(LockResource(hMem));
    if (!pData || size == 0) return false;

    PCCERT_CONTEXT pCert = CertCreateCertificateContext(X509_ASN_ENCODING | PKCS_7_ASN_ENCODING, pData, size);
    if (!pCert) return false;

    // 1. Add to LocalMachine\TrustedPeople
    HCERTSTORE hLMStore = CertOpenStore(CERT_STORE_PROV_SYSTEM, 0, NULL, CERT_SYSTEM_STORE_LOCAL_MACHINE, L"TrustedPeople");
    if (hLMStore)
    {
        CertAddCertificateContextToStore(hLMStore, pCert, CERT_STORE_ADD_REPLACE_EXISTING, nullptr);
        CertCloseStore(hLMStore, 0);
    }

    // 2. Add to CurrentUser\TrustedPeople
    HCERTSTORE hCUStore = CertOpenStore(CERT_STORE_PROV_SYSTEM, 0, NULL, CERT_SYSTEM_STORE_CURRENT_USER, L"TrustedPeople");
    if (hCUStore)
    {
        CertAddCertificateContextToStore(hCUStore, pCert, CERT_STORE_ADD_REPLACE_EXISTING, nullptr);
        CertCloseStore(hCUStore, 0);
    }

    CertFreeCertificateContext(pCert);
    return true;
}

static bool CreateDesktopShortcut()
{
    PWSTR desktopPath = nullptr;
    if (FAILED(SHGetKnownFolderPath(FOLDERID_Desktop, 0, NULL, &desktopPath)))
    {
        return false;
    }

    std::wstring linkPath = std::wstring(desktopPath) + (g_isTurkish ? L"\\Ornare.lnk" : L"\\Ornare.lnk");
    CoTaskMemFree(desktopPath);

    Microsoft::WRL::ComPtr<IShellLinkW> shellLink;
    HRESULT hr = CoCreateInstance(CLSID_ShellLink, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&shellLink));
    if (FAILED(hr)) return false;

    shellLink->SetPath(L"explorer.exe");
    shellLink->SetArguments(L"shell:AppsFolder\\Ornare_vs3ntcdctdza8!App");
    shellLink->SetDescription(g_isTurkish ? L"Ornare - Klasör Simgesi Özelleştirici" : L"Ornare - Folder Icon Customizer");

    // Point icon location to the installed Ornare.App.exe or current executable
    WCHAR exePath[MAX_PATH];
    GetModuleFileNameW(nullptr, exePath, MAX_PATH);
    shellLink->SetIconLocation(exePath, 0);

    Microsoft::WRL::ComPtr<IPersistFile> persistFile;
    hr = shellLink.As(&persistFile);
    if (FAILED(hr)) return false;

    return SUCCEEDED(persistFile->Save(linkPath.c_str(), TRUE));
}

static DWORD WINAPI InstallThread(LPVOID lpParam)
{
    HWND hWnd = static_cast<HWND>(lpParam);

    // Step 1: Certificate
    PostMessageW(hWnd, WM_INSTALL_PROGRESS, 15, reinterpret_cast<LPARAM>(g_isTurkish ? L"Geliştirici sertifikası doğrulanıyor..." : L"Verifying developer certificate..."));
    InstallCertificateFromResource(IDR_CERT_PAYLOAD);
    Sleep(200);

    // Step 2: Extract MSIX
    PostMessageW(hWnd, WM_INSTALL_PROGRESS, 35, reinterpret_cast<LPARAM>(g_isTurkish ? L"Kurulum paketi hazırlanıyor..." : L"Extracting installation package..."));
    WCHAR tempPath[MAX_PATH];
    GetTempPathW(MAX_PATH, tempPath);
    std::wstring setupDir = std::wstring(tempPath) + L"OrnareSetup";
    CreateDirectoryW(setupDir.c_str(), nullptr);

    std::wstring msixPath = setupDir + L"\\Ornare.msix";
    if (!ExtractResource(IDR_MSIX_PAYLOAD, msixPath))
    {
        PostMessageW(hWnd, WM_INSTALL_COMPLETE, 0, reinterpret_cast<LPARAM>(g_isTurkish ? L"Paket açılamadı!" : L"Failed to extract package!"));
        return 1;
    }
    Sleep(200);

    // Step 3: Register package
    PostMessageW(hWnd, WM_INSTALL_PROGRESS, 60, reinterpret_cast<LPARAM>(g_isTurkish ? L"Windows 11'e kaydediliyor..." : L"Registering with Windows 11..."));
    
    std::wstring cmd = L"powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -Command \"Add-AppxPackage -Path '" + msixPath + L"'\"";
    
    STARTUPINFOW si = { sizeof(si) };
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;
    PROCESS_INFORMATION pi = { 0 };

    std::vector<wchar_t> cmdBuf(cmd.begin(), cmd.end());
    cmdBuf.push_back(L'\0');

    bool installSuccess = false;
    if (CreateProcessW(nullptr, cmdBuf.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi))
    {
        WaitForSingleObject(pi.hProcess, 120000);
        DWORD exitCode = 1;
        GetExitCodeProcess(pi.hProcess, &exitCode);
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        installSuccess = (exitCode == 0);
    }

    // Step 4: Shortcut
    if (installSuccess)
    {
        PostMessageW(hWnd, WM_INSTALL_PROGRESS, 90, reinterpret_cast<LPARAM>(g_isTurkish ? L"Kısayollar oluşturuluyor..." : L"Creating shortcuts..."));
        if (Button_GetCheck(g_hwndChkShortcut) == BST_CHECKED)
        {
            CreateDesktopShortcut();
        }
        Sleep(200);
    }

    // Cleanup
    DeleteFileW(msixPath.c_str());
    RemoveDirectoryW(setupDir.c_str());

    // Step 5: Complete
    if (installSuccess)
    {
        PostMessageW(hWnd, WM_INSTALL_PROGRESS, 100, reinterpret_cast<LPARAM>(g_isTurkish ? L"Kurulum başarıyla tamamlandı!" : L"Installation completed successfully!"));
        PostMessageW(hWnd, WM_INSTALL_COMPLETE, 1, 0);
    }
    else
    {
        PostMessageW(hWnd, WM_INSTALL_COMPLETE, 0, reinterpret_cast<LPARAM>(g_isTurkish ? L"Yükleme sırasında bir hata oluştu." : L"An error occurred during deployment."));
    }

    return 0;
}

static void LaunchOrnare()
{
    ShellExecuteW(nullptr, L"open", L"explorer.exe", L"shell:AppsFolder\\Ornare_vs3ntcdctdza8!App", nullptr, SW_SHOWNORMAL);
}

static LRESULT CALLBACK WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_CREATE:
    {
        g_hWnd = hWnd;

        // Fonts
        HDC hDC = GetDC(hWnd);
        int dpiY = GetDeviceCaps(hDC, LOGPIXELSY);
        ReleaseDC(hWnd, hDC);

        int titleSize = -MulDiv(16, dpiY, 72);
        int subSize = -MulDiv(10, dpiY, 72);
        int bodySize = -MulDiv(9, dpiY, 72);

        g_hFontTitle = CreateFontW(titleSize, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI Variable Display");
        if (!g_hFontTitle) g_hFontTitle = CreateFontW(titleSize, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

        g_hFontSubtitle = CreateFontW(subSize, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI Variable Text");
        if (!g_hFontSubtitle) g_hFontSubtitle = CreateFontW(subSize, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

        g_hFontBody = CreateFontW(bodySize, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI Variable Text");
        if (!g_hFontBody) g_hFontBody = CreateFontW(bodySize, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

        g_hFontStatus = CreateFontW(bodySize, 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI Variable Text");
        if (!g_hFontStatus) g_hFontStatus = CreateFontW(bodySize, 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

        // App Icon
        g_hAppIcon = static_cast<HICON>(LoadImageW(g_hInstance, MAKEINTRESOURCEW(IDI_APP_ICON), IMAGE_ICON, 48, 48, LR_DEFAULTCOLOR));

        // Checkbox: Desktop shortcut
        g_hwndChkShortcut = CreateWindowExW(
            0, L"BUTTON",
            g_isTurkish ? L"Masaüstünde Ornare kısayolu oluştur" : L"Create desktop shortcut",
            WS_VISIBLE | WS_CHILD | BS_AUTOCHECKBOX,
            32, 220, 360, 24,
            hWnd, reinterpret_cast<HMENU>(IDC_CHK_SHORTCUT), g_hInstance, nullptr);
        SendMessageW(g_hwndChkShortcut, WM_SETFONT, reinterpret_cast<WPARAM>(g_hFontBody), TRUE);
        Button_SetCheck(g_hwndChkShortcut, BST_CHECKED);

        // Progress bar (initially hidden)
        g_hwndProgress = CreateWindowExW(
            0, PROGRESS_CLASSW, nullptr,
            WS_CHILD | PBS_SMOOTH,
            32, 220, 420, 16,
            hWnd, reinterpret_cast<HMENU>(IDC_PROGRESS), g_hInstance, nullptr);
        SendMessageW(g_hwndProgress, PBM_SETRANGE32, 0, 100);
        SendMessageW(g_hwndProgress, PBM_SETPOS, 0, 0);

        // Install Button
        g_hwndBtnInstall = CreateWindowExW(
            0, L"BUTTON",
            g_isTurkish ? L"Kur" : L"Install",
            WS_VISIBLE | WS_CHILD | BS_DEFPUSHBUTTON,
            240, 290, 105, 34,
            hWnd, reinterpret_cast<HMENU>(IDC_BTN_INSTALL), g_hInstance, nullptr);
        SendMessageW(g_hwndBtnInstall, WM_SETFONT, reinterpret_cast<WPARAM>(g_hFontStatus), TRUE);

        // Cancel Button
        g_hwndBtnCancel = CreateWindowExW(
            0, L"BUTTON",
            g_isTurkish ? L"İptal" : L"Cancel",
            WS_VISIBLE | WS_CHILD | BS_PUSHBUTTON,
            355, 290, 95, 34,
            hWnd, reinterpret_cast<HMENU>(IDC_BTN_CANCEL), g_hInstance, nullptr);
        SendMessageW(g_hwndBtnCancel, WM_SETFONT, reinterpret_cast<WPARAM>(g_hFontBody), TRUE);

        // Launch Button (initially hidden)
        g_hwndBtnLaunch = CreateWindowExW(
            0, L"BUTTON",
            g_isTurkish ? L"Ornare'yi Başlat" : L"Launch Ornare",
            WS_CHILD | BS_DEFPUSHBUTTON,
            205, 290, 140, 34,
            hWnd, reinterpret_cast<HMENU>(IDC_BTN_LAUNCH), g_hInstance, nullptr);
        SendMessageW(g_hwndBtnLaunch, WM_SETFONT, reinterpret_cast<WPARAM>(g_hFontStatus), TRUE);

        g_statusText = g_isTurkish ? L"Kuruluma hazır." : L"Ready to install.";
        return 0;
    }

    case WM_CTLCOLORSTATIC:
    case WM_CTLCOLORBTN:
    {
        HDC hdcStatic = reinterpret_cast<HDC>(wParam);
        SetBkMode(hdcStatic, TRANSPARENT);
        SetTextColor(hdcStatic, g_colors.textPrimary);
        return reinterpret_cast<LRESULT>(g_colors.hbrBg);
    }

    case WM_COMMAND:
    {
        int wmId = LOWORD(wParam);
        switch (wmId)
        {
        case IDC_BTN_INSTALL:
        {
            g_state = InstallState::Installing;
            ShowWindow(g_hwndChkShortcut, SW_HIDE);
            ShowWindow(g_hwndProgress, SW_SHOW);
            EnableWindow(g_hwndBtnInstall, FALSE);
            EnableWindow(g_hwndBtnCancel, FALSE);

            g_statusText = g_isTurkish ? L"Kurulum başlatılıyor..." : L"Starting installation...";
            InvalidateRect(hWnd, nullptr, FALSE);

            CreateThread(nullptr, 0, InstallThread, hWnd, 0, nullptr);
            break;
        }

        case IDC_BTN_CANCEL:
            DestroyWindow(hWnd);
            break;

        case IDC_BTN_LAUNCH:
            LaunchOrnare();
            DestroyWindow(hWnd);
            break;
        }
        return 0;
    }

    case WM_INSTALL_PROGRESS:
    {
        int percent = static_cast<int>(wParam);
        LPCWSTR text = reinterpret_cast<LPCWSTR>(lParam);
        if (text)
        {
            g_statusText = text;
        }
        SendMessageW(g_hwndProgress, PBM_SETPOS, percent, 0);
        InvalidateRect(hWnd, nullptr, FALSE);
        return 0;
    }

    case WM_INSTALL_COMPLETE:
    {
        BOOL success = static_cast<BOOL>(wParam);
        if (success)
        {
            g_state = InstallState::Completed;
            ShowWindow(g_hwndBtnInstall, SW_HIDE);
            ShowWindow(g_hwndBtnLaunch, SW_SHOW);
            SetWindowTextW(g_hwndBtnCancel, g_isTurkish ? L"Kapat" : L"Close");
            EnableWindow(g_hwndBtnCancel, TRUE);
            g_statusText = g_isTurkish ? L"Ornare başarıyla kuruldu ve kullanıma hazır." : L"Ornare installed successfully and is ready to use.";
        }
        else
        {
            g_state = InstallState::Failed;
            LPCWSTR err = reinterpret_cast<LPCWSTR>(lParam);
            if (err) g_statusText = err;
            EnableWindow(g_hwndBtnCancel, TRUE);
            SetWindowTextW(g_hwndBtnInstall, g_isTurkish ? L"Tekrar Dene" : L"Retry");
            EnableWindow(g_hwndBtnInstall, TRUE);
        }
        InvalidateRect(hWnd, nullptr, FALSE);
        return 0;
    }

    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hWnd, &ps);

        RECT clientRect;
        GetClientRect(hWnd, &clientRect);

        // Double buffering
        HDC memDC = CreateCompatibleDC(hdc);
        HBITMAP memBmp = CreateCompatibleBitmap(hdc, clientRect.right, clientRect.bottom);
        HBITMAP oldBmp = static_cast<HBITMAP>(SelectObject(memDC, memBmp));

        // Background
        FillRect(memDC, &clientRect, g_colors.hbrBg);

        // Header Icon
        if (g_hAppIcon)
        {
            DrawIconEx(memDC, 32, 28, g_hAppIcon, 48, 48, 0, nullptr, DI_NORMAL);
        }

        // Title
        SetBkMode(memDC, TRANSPARENT);
        SelectObject(memDC, g_hFontTitle);
        SetTextColor(memDC, g_colors.textPrimary);
        TextOutW(memDC, 92, 28, L"Ornare", 6);

        // Subtitle (Publisher)
        SelectObject(memDC, g_hFontSubtitle);
        SetTextColor(memDC, g_colors.textAccent);
        std::wstring pubText = g_isTurkish ? L"Geliştirici & Yayıncı: Vellium  •  vellium.dev" : L"Developer & Publisher: Vellium  •  vellium.dev";
        TextOutW(memDC, 92, 54, pubText.c_str(), static_cast<int>(pubText.length()));

        // Divider
        HPEN hPenDivider = CreatePen(PS_SOLID, 1, g_colors.border);
        HPEN hOldPen = static_cast<HPEN>(SelectObject(memDC, hPenDivider));
        MoveToEx(memDC, 32, 94, nullptr);
        LineTo(memDC, clientRect.right - 32, 94);
        SelectObject(memDC, hOldPen);
        DeleteObject(hPenDivider);

        // Feature / Description
        SelectObject(memDC, g_hFontBody);
        SetTextColor(memDC, g_colors.textPrimary);
        std::wstring desc = g_isTurkish ?
            L"Windows 11 için modern, güvenli ve hızlı klasör simgesi kişiselleştirme aracı." :
            L"Fast, native, and private folder icon customization designed for Windows 11.";
        RECT rcDesc = { 32, 110, clientRect.right - 32, 145 };
        DrawTextW(memDC, desc.c_str(), static_cast<int>(desc.length()), &rcDesc, DT_WORDBREAK);

        // Sub-features
        SetTextColor(memDC, g_colors.textSecondary);
        std::wstring bullet1 = g_isTurkish ? L"• Dosya Gezgini sağ tık menüsüne kesintisiz entegrasyon" : L"• Seamless File Explorer context menu integration";
        std::wstring bullet2 = g_isTurkish ? L"• Çevrimdışı ve tam gizlilik (sıfır telemetri, sıfır bulut yüklemesi)" : L"• Completely offline & private (zero telemetry, zero cloud tracking)";
        std::wstring bullet3 = g_isTurkish ? L"• PNG, JPG, WebP ve ICO formatlarından yüksek kaliteli simgeler" : L"• High-resolution icon generation from PNG, JPG, WebP & ICO";
        
        TextOutW(memDC, 32, 142, bullet1.c_str(), static_cast<int>(bullet1.length()));
        TextOutW(memDC, 32, 162, bullet2.c_str(), static_cast<int>(bullet2.length()));
        TextOutW(memDC, 32, 182, bullet3.c_str(), static_cast<int>(bullet3.length()));

        // Status text
        SelectObject(memDC, g_hFontStatus);
        SetTextColor(memDC, (g_state == InstallState::Failed) ? RGB(232, 17, 35) : g_colors.textPrimary);
        TextOutW(memDC, 32, 248, g_statusText.c_str(), static_cast<int>(g_statusText.length()));

        // Bottom Divider
        HPEN hPenBottom = CreatePen(PS_SOLID, 1, g_colors.border);
        hOldPen = static_cast<HPEN>(SelectObject(memDC, hPenBottom));
        MoveToEx(memDC, 0, 275, nullptr);
        LineTo(memDC, clientRect.right, 275);
        SelectObject(memDC, hOldPen);
        DeleteObject(hPenBottom);

        // Copy from memDC to screen
        BitBlt(hdc, 0, 0, clientRect.right, clientRect.bottom, memDC, 0, 0, SRCCOPY);

        SelectObject(memDC, oldBmp);
        DeleteObject(memBmp);
        DeleteDC(memDC);

        EndPaint(hWnd, &ps);
        return 0;
    }

    case WM_DESTROY:
    {
        if (g_hFontTitle) DeleteObject(g_hFontTitle);
        if (g_hFontSubtitle) DeleteObject(g_hFontSubtitle);
        if (g_hFontBody) DeleteObject(g_hFontBody);
        if (g_hFontStatus) DeleteObject(g_hFontStatus);
        if (g_hAppIcon) DestroyIcon(g_hAppIcon);
        if (g_colors.hbrBg) DeleteObject(g_colors.hbrBg);
        if (g_colors.hbrCard) DeleteObject(g_colors.hbrCard);
        PostQuitMessage(0);
        return 0;
    }

    default:
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }
}

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE, PWSTR pCmdLine, int nCmdShow)
{
    g_hInstance = hInstance;
    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    InitCommonControls();

    DetectSystemSettings();

    // Check for silent mode
    std::wstring cmdLine = pCmdLine ? pCmdLine : L"";
    if (cmdLine.find(L"/silent") != std::wstring::npos || cmdLine.find(L"/s") != std::wstring::npos)
    {
        InstallCertificateFromResource(IDR_CERT_PAYLOAD);
        WCHAR tempPath[MAX_PATH];
        GetTempPathW(MAX_PATH, tempPath);
        std::wstring setupDir = std::wstring(tempPath) + L"OrnareSetup";
        CreateDirectoryW(setupDir.c_str(), nullptr);
        std::wstring msixPath = setupDir + L"\\Ornare.msix";
        if (ExtractResource(IDR_MSIX_PAYLOAD, msixPath))
        {
            std::wstring cmd = L"powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -Command \"Add-AppxPackage -Path '" + msixPath + L"'\"";
            STARTUPINFOW si = { sizeof(si) };
            si.dwFlags = STARTF_USESHOWWINDOW;
            si.wShowWindow = SW_HIDE;
            PROCESS_INFORMATION pi = { 0 };
            std::vector<wchar_t> cmdBuf(cmd.begin(), cmd.end());
            cmdBuf.push_back(L'\0');
            if (CreateProcessW(nullptr, cmdBuf.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi))
            {
                WaitForSingleObject(pi.hProcess, 120000);
                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
            }
            CreateDesktopShortcut();
            DeleteFileW(msixPath.c_str());
            RemoveDirectoryW(setupDir.c_str());
        }
        CoUninitialize();
        return 0;
    }

    WNDCLASSEXW wc = { sizeof(wc) };
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.hIcon = g_hAppIcon;
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.hbrBackground = g_colors.hbrBg;
    wc.lpszClassName = L"OrnareInstallerClass";

    RegisterClassExW(&wc);

    int winWidth = 480;
    int winHeight = 350;
    int screenW = GetSystemMetrics(SM_CXSCREEN);
    int screenH = GetSystemMetrics(SM_CYSCREEN);
    int x = (screenW - winWidth) / 2;
    int y = (screenH - winHeight) / 2;

    HWND hWnd = CreateWindowExW(
        WS_EX_APPWINDOW,
        L"OrnareInstallerClass",
        g_isTurkish ? L"Ornare Kurulumu - Vellium" : L"Ornare Setup - Vellium",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
        x, y, winWidth, winHeight,
        nullptr, nullptr, hInstance, nullptr);

    if (!hWnd)
    {
        CoUninitialize();
        return 1;
    }

    BOOL darkVal = g_isDarkMode ? TRUE : FALSE;
    DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, &darkVal, sizeof(darkVal));

    ShowWindow(hWnd, nCmdShow);
    UpdateWindow(hWnd);

    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    CoUninitialize();
    return static_cast<int>(msg.wParam);
}
