#include <windows.h>
#include <unknwn.h>
#include <shobjidl_core.h>
#include <shellapi.h>
#include <shlwapi.h>
#include <atomic>
#include <string>

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "uuid.lib")

// Top-level golden path command CLSID: {4B1F8E1C-6C4D-4E10-9F1C-9E29DA21A77E}
static const CLSID CLSID_OrnareCommand =
    { 0x4B1F8E1C, 0x6C4D, 0x4E10, { 0x9F, 0x1C, 0x9E, 0x29, 0xDA, 0x21, 0xA7, 0x7E } };

// Top-level app flyout command CLSID (F21): {A5E650D8-04B2-4A82-9B7C-295822368940}
static const CLSID CLSID_OrnareFlyoutCommand =
    { 0xA5E650D8, 0x04B2, 0x4A82, { 0x9B, 0x7C, 0x29, 0x58, 0x22, 0x36, 0x89, 0x40 } };

// IID_IExplorerCommand: {a08ce4d0-fa25-44ab-b57c-c7b1c323e0db}
static const IID IID_IExplorerCommand_Explicit =
    { 0xa08ce4d0, 0xfa25, 0x44ab, { 0xb5, 0x7c, 0xc7, 0xb1, 0xc3, 0x23, 0xe0, 0xdb } };

// IID_IEnumExplorerCommand: {a8882650-0b6f-4791-9f13-6cadc4d79449}
static const IID IID_IEnumExplorerCommand_Explicit =
    { 0xa8882650, 0x0b6f, 0x4791, { 0x9f, 0x13, 0x6c, 0xad, 0xc4, 0xd7, 0x94, 0x49 } };

static HMODULE g_hModule = nullptr;
static std::atomic<LONG> g_moduleRefCount{ 0 };

namespace
{
    std::wstring UrlEncode(const wchar_t* str) noexcept
    {
        if (str == nullptr)
        {
            return std::wstring();
        }

        int utf8Len = WideCharToMultiByte(CP_UTF8, 0, str, -1, nullptr, 0, nullptr, nullptr);
        if (utf8Len <= 1)
        {
            return std::wstring();
        }

        std::string utf8Str(utf8Len, '\0');
        WideCharToMultiByte(CP_UTF8, 0, str, -1, &utf8Str[0], utf8Len, nullptr, nullptr);
        utf8Str.pop_back(); // Remove null terminator

        std::wstring encoded;
        encoded.reserve(utf8Str.size() * 3);

        const char hexChars[] = "0123456789ABCDEF";

        for (unsigned char c : utf8Str)
        {
            // RFC 3986 unreserved characters: ALPHA / DIGIT / "-" / "." / "_" / "~"
            if ((c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '-' || c == '_' || c == '.' || c == '~')
            {
                encoded.push_back(static_cast<wchar_t>(c));
            }
            else
            {
                encoded.push_back(L'%');
                encoded.push_back(static_cast<wchar_t>(hexChars[(c >> 4) & 0x0F]));
                encoded.push_back(static_cast<wchar_t>(hexChars[c & 0x0F]));
            }
        }

        return encoded;
    }

    HRESULT GetAppIconPath(LPWSTR* ppszIcon) noexcept
    {
        if (ppszIcon == nullptr)
        {
            return E_POINTER;
        }
        *ppszIcon = nullptr;

        wchar_t modulePath[MAX_PATH];
        DWORD len = GetModuleFileNameW(g_hModule, modulePath, MAX_PATH);
        if (len == 0 || len >= MAX_PATH)
        {
            return E_FAIL;
        }

        // Return path to embedded icon resource 101 in this DLL: "<modulePath>,-101"
        wchar_t iconRef[MAX_PATH + 16];
        swprintf_s(iconRef, MAX_PATH + 16, L"%s,-101", modulePath);

        size_t strLen = wcslen(iconRef) + 1;
        LPWSTR buffer = static_cast<LPWSTR>(CoTaskMemAlloc(strLen * sizeof(wchar_t)));
        if (buffer == nullptr)
        {
            return E_OUTOFMEMORY;
        }

        wcscpy_s(buffer, strLen, iconRef);
        *ppszIcon = buffer;
        return S_OK;
    }

    HRESULT CheckTargetFolderState(IShellItemArray* psiItemArray, EXPCMDSTATE* pcmdState) noexcept
    {
        if (pcmdState == nullptr)
        {
            return E_POINTER;
        }

        *pcmdState = ECS_HIDDEN;

        if (psiItemArray == nullptr)
        {
            return S_OK;
        }

        DWORD count = 0;
        HRESULT hr = psiItemArray->GetCount(&count);
        if (FAILED(hr) || count != 1)
        {
            *pcmdState = ECS_HIDDEN;
            return S_OK;
        }

        IShellItem* item = nullptr;
        hr = psiItemArray->GetItemAt(0, &item);
        if (FAILED(hr) || item == nullptr)
        {
            *pcmdState = ECS_HIDDEN;
            return S_OK;
        }

        LPWSTR filePath = nullptr;
        hr = item->GetDisplayName(SIGDN_FILESYSPATH, &filePath);
        item->Release();

        if (FAILED(hr) || filePath == nullptr)
        {
            *pcmdState = ECS_HIDDEN;
            return S_OK;
        }

        // Quick non-blocking attribute check: must be a real folder, not a reparse point/symlink
        DWORD attributes = GetFileAttributesW(filePath);
        CoTaskMemFree(filePath);

        if (attributes == INVALID_FILE_ATTRIBUTES ||
            (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0 ||
            (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
        {
            *pcmdState = ECS_DISABLED;
            return S_OK;
        }

        *pcmdState = ECS_ENABLED;
        return S_OK;
    }

    bool GetSingleFolderPath(IShellItemArray* psiItemArray, std::wstring& outPath) noexcept
    {
        if (psiItemArray == nullptr)
        {
            return false;
        }

        DWORD count = 0;
        HRESULT hr = psiItemArray->GetCount(&count);
        if (FAILED(hr) || count != 1)
        {
            return false;
        }

        IShellItem* item = nullptr;
        hr = psiItemArray->GetItemAt(0, &item);
        if (FAILED(hr) || item == nullptr)
        {
            return false;
        }

        LPWSTR filePath = nullptr;
        hr = item->GetDisplayName(SIGDN_FILESYSPATH, &filePath);
        item->Release();

        if (FAILED(hr) || filePath == nullptr)
        {
            return false;
        }

        wchar_t canonicalPath[MAX_PATH];
        if (PathCanonicalizeW(canonicalPath, filePath))
        {
            outPath = canonicalPath;
        }
        else
        {
            outPath = filePath;
        }

        CoTaskMemFree(filePath);
        return !outPath.empty();
    }

    bool FindAppExe(wchar_t* appExePath, DWORD maxLen) noexcept
    {
        HMODULE hModule = nullptr;
        GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           reinterpret_cast<LPCWSTR>(&FindAppExe),
                           &hModule);

        if (hModule != nullptr && GetModuleFileNameW(hModule, appExePath, maxLen) > 0)
        {
            PathRemoveFileSpecW(appExePath);
            PathAppendW(appExePath, L"Ornare.App.exe");
            if (PathFileExistsW(appExePath))
            {
                return true;
            }

            // Development fallback: check Ornare.App build output
            wchar_t devPath[MAX_PATH] = {};
            GetModuleFileNameW(hModule, devPath, MAX_PATH);
            PathRemoveFileSpecW(devPath); // dll filename -> Release
            PathRemoveFileSpecW(devPath); // Release -> x64
            PathRemoveFileSpecW(devPath); // x64 -> Ornare.ShellExtension
            PathRemoveFileSpecW(devPath); // Ornare.ShellExtension -> src
            PathAppendW(devPath, L"Ornare.App\\bin\\x64\\Release\\net10.0-windows10.0.26100.0\\Ornare.App.exe");
            if (PathFileExistsW(devPath))
            {
                wcscpy_s(appExePath, maxLen, devPath);
                return true;
            }

            wchar_t devPath2[MAX_PATH] = {};
            GetModuleFileNameW(hModule, devPath2, MAX_PATH);
            PathRemoveFileSpecW(devPath2);
            PathRemoveFileSpecW(devPath2);
            PathRemoveFileSpecW(devPath2);
            PathRemoveFileSpecW(devPath2);
            PathAppendW(devPath2, L"Ornare.App\\bin\\Release\\net10.0-windows10.0.26100.0\\Ornare.App.exe");
            if (PathFileExistsW(devPath2))
            {
                wcscpy_s(appExePath, maxLen, devPath2);
                return true;
            }
        }
        return false;
    }

    enum class ShellStringId
    {
        SetFolderIcon,
        ApplyLastIcon,
        Undo,
        Reset,
        ResetTooltip
    };

    const wchar_t* GetLocalizedShellString(ShellStringId id) noexcept
    {
        LANGID langId = GetUserDefaultUILanguage();
        WORD primaryLang = PRIMARYLANGID(langId);

        switch (id)
        {
        case ShellStringId::SetFolderIcon:
            if (primaryLang == LANG_TURKISH) return L"Klas\u00F6r Simgesini Ayarla";
            if (primaryLang == LANG_GERMAN) return L"Ordnersymbol festlegen";
            if (primaryLang == LANG_FRENCH) return L"D\u00E9finir l'ic\u00F4ne du dossier";
            if (primaryLang == LANG_SPANISH) return L"Establecer icono de carpeta";
            if (primaryLang == LANG_PORTUGUESE) return L"Definir \u00EDcone da pasta";
            return L"Set Folder Icon";

        case ShellStringId::ApplyLastIcon:
            if (primaryLang == LANG_TURKISH) return L"Son simgeyi uygula";
            if (primaryLang == LANG_GERMAN) return L"Letztes Symbol anwenden";
            if (primaryLang == LANG_FRENCH) return L"Appliquer la derni\u00E8re ic\u00F4ne";
            if (primaryLang == LANG_SPANISH) return L"Aplicar \u00FAltimo icono";
            if (primaryLang == LANG_PORTUGUESE) return L"Aplicar \u00FAltimo \u00EDcone";
            return L"Apply last icon";

        case ShellStringId::Undo:
            if (primaryLang == LANG_TURKISH) return L"Geri Al";
            if (primaryLang == LANG_GERMAN) return L"R\u00FCckg\u00E4ngig";
            if (primaryLang == LANG_FRENCH) return L"Annuler";
            if (primaryLang == LANG_SPANISH) return L"Deshacer";
            if (primaryLang == LANG_PORTUGUESE) return L"Desfazer";
            return L"Undo";

        case ShellStringId::Reset:
            if (primaryLang == LANG_TURKISH) return L"S\u0131f\u0131rla";
            if (primaryLang == LANG_GERMAN) return L"Zur\u00FCcksetzen";
            if (primaryLang == LANG_FRENCH) return L"R\u00E9initialiser";
            if (primaryLang == LANG_SPANISH) return L"Restablecer";
            if (primaryLang == LANG_PORTUGUESE) return L"Redefinir";
            return L"Reset";

        case ShellStringId::ResetTooltip:
            if (primaryLang == LANG_TURKISH) return L"Klas\u00F6r simgesini Windows varsay\u0131lan\u0131na s\u0131f\u0131rla";
            if (primaryLang == LANG_GERMAN) return L"Ordnersymbol auf Windows-Standard zur\u00FCcksetzen";
            if (primaryLang == LANG_FRENCH) return L"R\u00E9initialiser l'ic\u00F4ne du dossier aux valeurs par d\u00E9faut de Windows";
            if (primaryLang == LANG_SPANISH) return L"Restablecer el icono de la carpeta al valor predeterminado de Windows";
            if (primaryLang == LANG_PORTUGUESE) return L"Redefinir o \u00EDcone da pasta para o padr\u00E3o do Windows";
            return L"Reset folder icon to Windows default";

        default:
            return L"";
        }
    }
}

// -----------------------------------------------------------------------------------------
// Launch plumbing
// -----------------------------------------------------------------------------------------
// Four commands differ only in three values: the localized title, the protocol verb and the CLI
// switch. Everything that used to be copied four times (ref counting, QueryInterface, the
// CoTaskMemAlloc title buffer, the two-step protocol-then-CLI launch) lives here exactly once.
// A duplicated IExplorerCommand implementation is the exact shape of bug that crashes Explorer,
// because Explorer loads this DLL in-process: fixing a defect in one copy and not the others is
// how the fourth command ends up dereferencing a null buffer.
namespace
{
    // ShellExecuteExW reports success through hInstApp for legacy reasons: a value of 32 or
    // less is an error code even when the call itself returned TRUE.
    bool TryProtocolActivation(const std::wstring& uri) noexcept
    {
        SHELLEXECUTEINFOW sei = {};
        sei.cbSize = sizeof(sei);
        sei.fMask = SEE_MASK_NOASYNC | SEE_MASK_FLAG_NO_UI;
        sei.lpVerb = L"open";
        sei.lpFile = uri.c_str();
        sei.nShow = SW_SHOWNORMAL;

        BOOL launched = ShellExecuteExW(&sei);
        return launched && reinterpret_cast<INT_PTR>(sei.hInstApp) > 32;
    }

    // Packaged activation is preferred; the unpackaged CLI exists for developer builds and for
    // machines where the package registration is broken.
    void LaunchCliFallback(const std::wstring& arguments) noexcept
    {
        wchar_t appExePath[MAX_PATH] = {};
        if (!FindAppExe(appExePath, MAX_PATH))
        {
            return;
        }

        SHELLEXECUTEINFOW sei = {};
        sei.cbSize = sizeof(sei);
        sei.fMask = SEE_MASK_NOASYNC | SEE_MASK_FLAG_NO_UI;
        sei.lpVerb = L"open";
        sei.lpFile = appExePath;
        sei.lpParameters = arguments.c_str();
        sei.nShow = SW_SHOWNORMAL;
        ShellExecuteExW(&sei);
    }

    // cursorX/Y are only meaningful for the quick panel; negative means "no cursor hint".
    void LaunchCommand(
        const wchar_t* verb,
        const wchar_t* cliSwitch,
        const wchar_t* folderPath,
        int cursorX,
        int cursorY) noexcept
    {
        if (folderPath == nullptr || *folderPath == L'\0')
        {
            return;
        }

        std::wstring uri = L"ornare://";
        uri += verb;
        uri += L"?v=1&path=";
        uri += UrlEncode(folderPath);
        if (cursorX >= 0)
        {
            uri += L"&x=" + std::to_wstring(cursorX);
            uri += L"&y=" + std::to_wstring(cursorY);
        }

        if (TryProtocolActivation(uri))
        {
            return;
        }

        std::wstring arguments = cliSwitch;
        arguments += L" \"";
        arguments += folderPath;
        arguments += L"\"";
        if (cursorX >= 0)
        {
            arguments += L" --x " + std::to_wstring(cursorX);
            arguments += L" --y " + std::to_wstring(cursorY);
        }

        LaunchCliFallback(arguments);
    }
}

// -----------------------------------------------------------------------------------------
// Shared IExplorerCommand implementation
// -----------------------------------------------------------------------------------------
class OrnareCommandBase : public IExplorerCommand
{
public:
    OrnareCommandBase() : m_refCount(1)
    {
        g_moduleRefCount.fetch_add(1, std::memory_order_relaxed);
    }

    // Virtual because Release() deletes through an OrnareCommandBase pointer.
    virtual ~OrnareCommandBase()
    {
        g_moduleRefCount.fetch_sub(1, std::memory_order_relaxed);
    }

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) noexcept override
    {
        if (ppv == nullptr)
        {
            return E_POINTER;
        }

        if (InlineIsEqualGUID(riid, IID_IUnknown) ||
            InlineIsEqualGUID(riid, __uuidof(IExplorerCommand)) ||
            InlineIsEqualGUID(riid, IID_IExplorerCommand_Explicit))
        {
            *ppv = static_cast<IExplorerCommand*>(this);
            AddRef();
            return S_OK;
        }

        *ppv = nullptr;
        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() noexcept override
    {
        return static_cast<ULONG>(InterlockedIncrement(&m_refCount));
    }

    IFACEMETHODIMP_(ULONG) Release() noexcept override
    {
        ULONG count = static_cast<ULONG>(InterlockedDecrement(&m_refCount));
        if (count == 0)
        {
            delete this;
        }
        return count;
    }

    // IExplorerCommand
    IFACEMETHODIMP GetTitle(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszName) noexcept override
    {
        // Explorer runs this on its own threads and treats an escaped exception as a shell
        // fault. Every method below is noexcept, so without a catch-all a std::bad_alloc from
        // the CoTaskMem copy path would call std::terminate inside explorer.exe.
        try
        {
            return CopyToCoTaskMem(TitleText(), ppszName);
        }
        catch (...)
        {
            return E_FAIL;
        }
    }

    IFACEMETHODIMP GetIcon(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszIcon) noexcept override
    {
        try
        {
            return GetAppIconPath(ppszIcon);
        }
        catch (...)
        {
            return E_FAIL;
        }
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszInfoTip) noexcept override
    {
        try
        {
            const wchar_t* tooltip = ToolTipText();
            if (tooltip == nullptr || *tooltip == L'\0')
            {
                if (ppszInfoTip == nullptr)
                {
                    return E_POINTER;
                }

                *ppszInfoTip = nullptr;
                return E_NOTIMPL;
            }

            return CopyToCoTaskMem(tooltip, ppszInfoTip);
        }
        catch (...)
        {
            return E_FAIL;
        }
    }

    IFACEMETHODIMP GetCanonicalName(GUID* pguidCommandName) noexcept override
    {
        if (pguidCommandName == nullptr)
        {
            return E_POINTER;
        }
        *pguidCommandName = CanonicalClsid();
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray* psiItemArray, BOOL /*fOkToBeSlow*/, EXPCMDSTATE* pcmdState) noexcept override
    {
        try
        {
            return CheckTargetFolderState(psiItemArray, pcmdState);
        }
        catch (...)
        {
            return E_FAIL;
        }
    }

    IFACEMETHODIMP Invoke(IShellItemArray* psiItemArray, IBindCtx* /*pbc*/) noexcept override
    {
        try
        {
            std::wstring folderPath;
            if (!GetSingleFolderPath(psiItemArray, folderPath))
            {
                return S_OK;
            }

            // Explorer calls this on its UI thread. The command must never throw and never
            // block, so the launch is fire-and-forget and every failure path returns S_OK.
            // A failed launch is indistinguishable from a cancelled one from Explorer's
            // point of view, so swallowing here is the correct behaviour.
            InvokeCore(folderPath.c_str());
        }
        catch (...)
        {
            // Deliberately silent: an exception escaping into explorer.exe is worse than a
            // command that appears to do nothing. The CLI fallback logs the real failure.
        }
        return S_OK;
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* pFlags) noexcept override
    {
        if (pFlags == nullptr)
        {
            return E_POINTER;
        }
        *pFlags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** ppEnum) noexcept override
    {
        if (ppEnum == nullptr)
        {
            return E_POINTER;
        }
        *ppEnum = nullptr;
        return E_NOTIMPL;
    }

protected:
    virtual const wchar_t* TitleText() const noexcept = 0;
    virtual const wchar_t* ToolTipText() const noexcept { return L""; }
    virtual GUID CanonicalClsid() const noexcept { return GUID_NULL; }
    virtual void InvokeCore(const wchar_t* folderPath) noexcept = 0;

    static HRESULT CopyToCoTaskMem(const wchar_t* text, LPWSTR* destination) noexcept
    {
        if (destination == nullptr)
        {
            return E_POINTER;
        }

        if (text == nullptr)
        {
            return E_INVALIDARG;
        }

        size_t length = wcslen(text) + 1;
        LPWSTR buffer = static_cast<LPWSTR>(CoTaskMemAlloc(length * sizeof(wchar_t)));
        if (buffer == nullptr)
        {
            return E_OUTOFMEMORY;
        }

        wcscpy_s(buffer, length, text);
        *destination = buffer;
        return S_OK;
    }

private:
    LONG m_refCount;
};

// -----------------------------------------------------------------------------------------
// Golden Path Command: "Set Folder Icon"
// -----------------------------------------------------------------------------------------
class OrnareExplorerCommand final : public OrnareCommandBase
{
protected:
    const wchar_t* TitleText() const noexcept override
    {
        return GetLocalizedShellString(ShellStringId::SetFolderIcon);
    }

    GUID CanonicalClsid() const noexcept override
    {
        return CLSID_OrnareCommand;
    }

    void InvokeCore(const wchar_t* folderPath) noexcept override
    {
        POINT cursorPos = {};
        GetCursorPos(&cursorPos);
        LaunchCommand(L"set-folder", L"--set-folder", folderPath, cursorPos.x, cursorPos.y);
    }
};

// -----------------------------------------------------------------------------------------
// Child Command: "Apply last icon" (F21)
// -----------------------------------------------------------------------------------------
class OrnareApplyLastCommand final : public OrnareCommandBase
{
protected:
    const wchar_t* TitleText() const noexcept override
    {
        return GetLocalizedShellString(ShellStringId::ApplyLastIcon);
    }

    void InvokeCore(const wchar_t* folderPath) noexcept override
    {
        LaunchCommand(L"apply-last", L"--apply-last", folderPath, -1, -1);
    }
};

// -----------------------------------------------------------------------------------------
// Subcommand 2: "Undo" / "Geri Al"
// -----------------------------------------------------------------------------------------
class OrnareUndoCommand final : public OrnareCommandBase
{
protected:
    const wchar_t* TitleText() const noexcept override
    {
        return GetLocalizedShellString(ShellStringId::Undo);
    }

    void InvokeCore(const wchar_t* folderPath) noexcept override
    {
        LaunchCommand(L"undo", L"--undo", folderPath, -1, -1);
    }
};

// -----------------------------------------------------------------------------------------
// Subcommand 3: "Reset" / "SÄ±fÄ±rla"
// -----------------------------------------------------------------------------------------
class OrnareResetCommand final : public OrnareCommandBase
{
protected:
    const wchar_t* TitleText() const noexcept override
    {
        return GetLocalizedShellString(ShellStringId::Reset);
    }

    const wchar_t* ToolTipText() const noexcept override
    {
        return GetLocalizedShellString(ShellStringId::ResetTooltip);
    }

    void InvokeCore(const wchar_t* folderPath) noexcept override
    {
        LaunchCommand(L"reset", L"--reset", folderPath, -1, -1);
    }
};


// -----------------------------------------------------------------------------------------
// Subcommand Enumerator: IEnumExplorerCommand
// -----------------------------------------------------------------------------------------
class OrnareSubCommandEnumerator final : public IEnumExplorerCommand
{
public:
    OrnareSubCommandEnumerator(IExplorerCommand** commands, ULONG count, ULONG index = 0)
        : m_refCount(1), m_count(count), m_currentIndex(index), m_commands(nullptr)
    {
        g_moduleRefCount.fetch_add(1, std::memory_order_relaxed);
        if (count > 0 && commands != nullptr)
        {
            m_commands = new (std::nothrow) IExplorerCommand*[count];
            if (m_commands != nullptr)
            {
                for (ULONG i = 0; i < count; i++)
                {
                    m_commands[i] = commands[i];
                    if (m_commands[i] != nullptr)
                    {
                        m_commands[i]->AddRef();
                    }
                }
            }
            else
            {
                m_count = 0;
            }
        }
    }

    ~OrnareSubCommandEnumerator()
    {
        if (m_commands != nullptr)
        {
            for (ULONG i = 0; i < m_count; i++)
            {
                if (m_commands[i] != nullptr)
                {
                    m_commands[i]->Release();
                }
            }
            delete[] m_commands;
        }
        g_moduleRefCount.fetch_sub(1, std::memory_order_relaxed);
    }

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) noexcept override
    {
        if (ppv == nullptr)
        {
            return E_POINTER;
        }

        if (InlineIsEqualGUID(riid, IID_IUnknown) ||
            InlineIsEqualGUID(riid, __uuidof(IEnumExplorerCommand)) ||
            InlineIsEqualGUID(riid, IID_IEnumExplorerCommand_Explicit))
        {
            *ppv = static_cast<IEnumExplorerCommand*>(this);
            AddRef();
            return S_OK;
        }

        *ppv = nullptr;
        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() noexcept override
    {
        return static_cast<ULONG>(InterlockedIncrement(&m_refCount));
    }

    IFACEMETHODIMP_(ULONG) Release() noexcept override
    {
        ULONG count = static_cast<ULONG>(InterlockedDecrement(&m_refCount));
        if (count == 0)
        {
            delete this;
        }
        return count;
    }

    // IEnumExplorerCommand
    IFACEMETHODIMP Next(ULONG celt, IExplorerCommand** pUICommand, ULONG* pceltFetched) noexcept override
    {
        if (pUICommand == nullptr)
        {
            return E_POINTER;
        }

        if (celt == 0)
        {
            if (pceltFetched != nullptr)
            {
                *pceltFetched = 0;
            }
            return S_OK;
        }

        if (pceltFetched == nullptr && celt != 1)
        {
            return E_INVALIDARG;
        }

        ULONG fetched = 0;
        while (m_currentIndex < m_count && fetched < celt)
        {
            pUICommand[fetched] = m_commands[m_currentIndex];
            if (pUICommand[fetched] != nullptr)
            {
                pUICommand[fetched]->AddRef();
            }
            m_currentIndex++;
            fetched++;
        }

        if (pceltFetched != nullptr)
        {
            *pceltFetched = fetched;
        }

        return (fetched == celt) ? S_OK : S_FALSE;
    }

    IFACEMETHODIMP Skip(ULONG celt) noexcept override
    {
        m_currentIndex += celt;
        if (m_currentIndex > m_count)
        {
            m_currentIndex = m_count;
            return S_FALSE;
        }
        return S_OK;
    }

    IFACEMETHODIMP Reset() noexcept override
    {
        m_currentIndex = 0;
        return S_OK;
    }

    IFACEMETHODIMP Clone(IEnumExplorerCommand** ppenum) noexcept override
    {
        if (ppenum == nullptr)
        {
            return E_POINTER;
        }
        *ppenum = new (std::nothrow) OrnareSubCommandEnumerator(m_commands, m_count, m_currentIndex);
        return (*ppenum != nullptr) ? S_OK : E_OUTOFMEMORY;
    }

private:
    LONG m_refCount;
    ULONG m_count;
    ULONG m_currentIndex;
    IExplorerCommand** m_commands;
};

// -----------------------------------------------------------------------------------------
// Root Flyout Command: "Ornare" (ECF_HASSUBCOMMANDS)
// -----------------------------------------------------------------------------------------
class OrnareFlyoutCommand final : public OrnareCommandBase
{
public:
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* pFlags) noexcept override
    {
        if (pFlags == nullptr)
        {
            return E_POINTER;
        }
        *pFlags = ECF_HASSUBCOMMANDS;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** ppEnum) noexcept override
    {
        if (ppEnum == nullptr)
        {
            return E_POINTER;
        }
        *ppEnum = nullptr;

        // Exactly three implemented child commands in fixed order:
        // 1. "Apply last icon" ("Son simgeyi uygula")
        // 2. "Undo" ("Geri Al")
        // 3. "Reset" ("SÄ±fÄ±rla")
        OrnareApplyLastCommand* applyLastCmd = new (std::nothrow) OrnareApplyLastCommand();
        if (applyLastCmd == nullptr)
        {
            return E_OUTOFMEMORY;
        }

        OrnareUndoCommand* undoCmd = new (std::nothrow) OrnareUndoCommand();
        if (undoCmd == nullptr)
        {
            applyLastCmd->Release();
            return E_OUTOFMEMORY;
        }

        OrnareResetCommand* resetCmd = new (std::nothrow) OrnareResetCommand();
        if (resetCmd == nullptr)
        {
            applyLastCmd->Release();
            undoCmd->Release();
            return E_OUTOFMEMORY;
        }

        IExplorerCommand* commands[3] = { applyLastCmd, undoCmd, resetCmd };
        OrnareSubCommandEnumerator* enumerator = new (std::nothrow) OrnareSubCommandEnumerator(commands, 3);
        applyLastCmd->Release();
        undoCmd->Release();
        resetCmd->Release();

        if (enumerator == nullptr)
        {
            return E_OUTOFMEMORY;
        }

        *ppEnum = enumerator;
        return S_OK;
    }

protected:
    // The brand name is never translated, so the flyout root does not read the string table.
    const wchar_t* TitleText() const noexcept override
    {
        return L"Ornare";
    }

    GUID CanonicalClsid() const noexcept override
    {
        return CLSID_OrnareFlyoutCommand;
    }

    void InvokeCore(const wchar_t* /*folderPath*/) noexcept override
    {
        // Root flyout menu itself is not directly invoked when subcommands are present.
    }
};

// -----------------------------------------------------------------------------------------
// Class Factories
// -----------------------------------------------------------------------------------------
class OrnareClassFactory final : public IClassFactory
{
public:
    OrnareClassFactory() : m_refCount(1)
    {
        g_moduleRefCount.fetch_add(1, std::memory_order_relaxed);
    }

    ~OrnareClassFactory()
    {
        g_moduleRefCount.fetch_sub(1, std::memory_order_relaxed);
    }

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) noexcept override
    {
        if (ppv == nullptr)
        {
            return E_POINTER;
        }

        if (InlineIsEqualGUID(riid, IID_IUnknown) || InlineIsEqualGUID(riid, __uuidof(IClassFactory)))
        {
            *ppv = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }

        *ppv = nullptr;
        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() noexcept override
    {
        return static_cast<ULONG>(InterlockedIncrement(&m_refCount));
    }

    IFACEMETHODIMP_(ULONG) Release() noexcept override
    {
        ULONG count = static_cast<ULONG>(InterlockedDecrement(&m_refCount));
        if (count == 0)
        {
            delete this;
        }
        return count;
    }

    // IClassFactory
    IFACEMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject) noexcept override
    {
        if (ppvObject == nullptr)
        {
            return E_POINTER;
        }
        *ppvObject = nullptr;

        if (pUnkOuter != nullptr)
        {
            return CLASS_E_NOAGGREGATION;
        }

        OrnareExplorerCommand* command = new (std::nothrow) OrnareExplorerCommand();
        if (command == nullptr)
        {
            return E_OUTOFMEMORY;
        }

        HRESULT hr = command->QueryInterface(riid, ppvObject);
        command->Release();
        return hr;
    }

    IFACEMETHODIMP LockServer(BOOL fLock) noexcept override
    {
        if (fLock)
        {
            g_moduleRefCount.fetch_add(1, std::memory_order_relaxed);
        }
        else
        {
            g_moduleRefCount.fetch_sub(1, std::memory_order_relaxed);
        }
        return S_OK;
    }

private:
    LONG m_refCount;
};

class OrnareFlyoutClassFactory final : public IClassFactory
{
public:
    OrnareFlyoutClassFactory() : m_refCount(1)
    {
        g_moduleRefCount.fetch_add(1, std::memory_order_relaxed);
    }

    ~OrnareFlyoutClassFactory()
    {
        g_moduleRefCount.fetch_sub(1, std::memory_order_relaxed);
    }

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) noexcept override
    {
        if (ppv == nullptr)
        {
            return E_POINTER;
        }

        if (InlineIsEqualGUID(riid, IID_IUnknown) || InlineIsEqualGUID(riid, __uuidof(IClassFactory)))
        {
            *ppv = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }

        *ppv = nullptr;
        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() noexcept override
    {
        return static_cast<ULONG>(InterlockedIncrement(&m_refCount));
    }

    IFACEMETHODIMP_(ULONG) Release() noexcept override
    {
        ULONG count = static_cast<ULONG>(InterlockedDecrement(&m_refCount));
        if (count == 0)
        {
            delete this;
        }
        return count;
    }

    // IClassFactory
    IFACEMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject) noexcept override
    {
        if (ppvObject == nullptr)
        {
            return E_POINTER;
        }
        *ppvObject = nullptr;

        if (pUnkOuter != nullptr)
        {
            return CLASS_E_NOAGGREGATION;
        }

        OrnareFlyoutCommand* command = new (std::nothrow) OrnareFlyoutCommand();
        if (command == nullptr)
        {
            return E_OUTOFMEMORY;
        }

        HRESULT hr = command->QueryInterface(riid, ppvObject);
        command->Release();
        return hr;
    }

    IFACEMETHODIMP LockServer(BOOL fLock) noexcept override
    {
        if (fLock)
        {
            g_moduleRefCount.fetch_add(1, std::memory_order_relaxed);
        }
        else
        {
            g_moduleRefCount.fetch_sub(1, std::memory_order_relaxed);
        }
        return S_OK;
    }

private:
    LONG m_refCount;
};

// -----------------------------------------------------------------------------------------
// DLL Exports
// -----------------------------------------------------------------------------------------
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        g_hModule = hModule;
        DisableThreadLibraryCalls(hModule);
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

STDAPI DllCanUnloadNow(void)
{
    return (g_moduleRefCount.load(std::memory_order_relaxed) == 0) ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv)
{
    if (ppv == nullptr)
    {
        return E_POINTER;
    }
    *ppv = nullptr;

    // COM entry points are called on Explorer's threads; a failure must be an HRESULT, never
    // an exception. The factory allocation is the only throwing step in this function.
    try
    {
        if (InlineIsEqualGUID(rclsid, CLSID_OrnareCommand))
        {
            OrnareClassFactory* factory = new (std::nothrow) OrnareClassFactory();
            if (factory == nullptr)
            {
                return E_OUTOFMEMORY;
            }

            HRESULT hr = factory->QueryInterface(riid, ppv);
            factory->Release();
            return hr;
        }

        if (InlineIsEqualGUID(rclsid, CLSID_OrnareFlyoutCommand))
        {
            OrnareFlyoutClassFactory* factory = new (std::nothrow) OrnareFlyoutClassFactory();
            if (factory == nullptr)
            {
                return E_OUTOFMEMORY;
            }

            HRESULT hr = factory->QueryInterface(riid, ppv);
            factory->Release();
            return hr;
        }
    }
    catch (...)
    {
        *ppv = nullptr;
        return E_FAIL;
    }

    return CLASS_E_CLASSNOTAVAILABLE;
}
