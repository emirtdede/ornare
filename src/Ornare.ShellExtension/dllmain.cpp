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

// CLSID: {4B1F8E1C-6C4D-4E10-9F1C-9E29DA21A77E}
static const CLSID CLSID_OrnareCommand =
    { 0x4B1F8E1C, 0x6C4D, 0x4E10, { 0x9F, 0x1C, 0x9E, 0x29, 0xDA, 0x21, 0xA7, 0x7E } };

// IID_IExplorerCommand: {a08ce4d0-fa25-44ab-b57c-c7b1c323e0db}
static const IID IID_IExplorerCommand_Explicit =
    { 0xa08ce4d0, 0xfa25, 0x44ab, { 0xb5, 0x7c, 0xc7, 0xb1, 0xc3, 0x23, 0xe0, 0xdb } };

static std::atomic<LONG> g_moduleRefCount{ 0 };

class OrnareExplorerCommand final : public IExplorerCommand
{
public:
    OrnareExplorerCommand() : m_refCount(1)
    {
        g_moduleRefCount.fetch_add(1, std::memory_order_relaxed);
    }

    ~OrnareExplorerCommand()
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
        if (ppszName == nullptr)
        {
            return E_POINTER;
        }

        const wchar_t* title = L"Set Folder Icon";
        LANGID langId = GetUserDefaultUILanguage();
        if (PRIMARYLANGID(langId) == LANG_TURKISH)
        {
            title = L"Klas\u00F6r Simgesini Ayarla";
        }

        size_t len = wcslen(title) + 1;
        LPWSTR buffer = static_cast<LPWSTR>(CoTaskMemAlloc(len * sizeof(wchar_t)));
        if (buffer == nullptr)
        {
            return E_OUTOFMEMORY;
        }

        wcscpy_s(buffer, len, title);
        *ppszName = buffer;
        return S_OK;
    }

    IFACEMETHODIMP GetIcon(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszIcon) noexcept override
    {
        if (ppszIcon == nullptr)
        {
            return E_POINTER;
        }
        *ppszIcon = nullptr;
        // Use default shell command icon or return not implemented
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszInfoTip) noexcept override
    {
        if (ppszInfoTip == nullptr)
        {
            return E_POINTER;
        }
        *ppszInfoTip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(GUID* pguidCommandName) noexcept override
    {
        if (pguidCommandName == nullptr)
        {
            return E_POINTER;
        }
        *pguidCommandName = CLSID_OrnareCommand;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray* psiItemArray, BOOL /*fOkToBeSlow*/, EXPCMDSTATE* pcmdState) noexcept override
    {
        if (pcmdState == nullptr)
        {
            return E_POINTER;
        }

        // Fail-safe default
        *pcmdState = ECS_HIDDEN;

        if (psiItemArray == nullptr)
        {
            return S_OK;
        }

        DWORD count = 0;
        HRESULT hr = psiItemArray->GetCount(&count);
        if (FAILED(hr) || count != 1)
        {
            // V1 strictly restricts customization to exactly one folder
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
            // Virtual shell objects without filesystem path
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

    IFACEMETHODIMP Invoke(IShellItemArray* psiItemArray, IBindCtx* /*pbc*/) noexcept override
    {
        if (psiItemArray == nullptr)
        {
            return S_OK;
        }

        DWORD count = 0;
        HRESULT hr = psiItemArray->GetCount(&count);
        if (FAILED(hr) || count != 1)
        {
            return S_OK;
        }

        IShellItem* item = nullptr;
        hr = psiItemArray->GetItemAt(0, &item);
        if (FAILED(hr) || item == nullptr)
        {
            return S_OK;
        }

        LPWSTR filePath = nullptr;
        hr = item->GetDisplayName(SIGDN_FILESYSPATH, &filePath);
        item->Release();

        if (FAILED(hr) || filePath == nullptr)
        {
            return S_OK;
        }

        // Canonicalize/validate path
        wchar_t canonicalPath[MAX_PATH];
        if (PathCanonicalizeW(canonicalPath, filePath))
        {
            LaunchOrnareApp(canonicalPath);
        }
        else
        {
            LaunchOrnareApp(filePath);
        }

        CoTaskMemFree(filePath);
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

private:
    static void LaunchOrnareApp(const wchar_t* folderPath) noexcept
    {
        if (folderPath == nullptr || *folderPath == L'\0')
        {
            return;
        }

        wchar_t appExePath[MAX_PATH] = {};
        HMODULE hModule = nullptr;
        GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           reinterpret_cast<LPCWSTR>(&LaunchOrnareApp),
                           &hModule);

        bool foundExe = false;
        if (hModule != nullptr && GetModuleFileNameW(hModule, appExePath, MAX_PATH) > 0)
        {
            PathRemoveFileSpecW(appExePath);
            PathAppendW(appExePath, L"Ornare.App.exe");
            if (PathFileExistsW(appExePath))
            {
                foundExe = true;
            }
            else
            {
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
                    wcscpy_s(appExePath, MAX_PATH, devPath);
                    foundExe = true;
                }
                else
                {
                    wchar_t devPath2[MAX_PATH] = {};
                    GetModuleFileNameW(hModule, devPath2, MAX_PATH);
                    PathRemoveFileSpecW(devPath2);
                    PathRemoveFileSpecW(devPath2);
                    PathRemoveFileSpecW(devPath2);
                    PathRemoveFileSpecW(devPath2);
                    PathAppendW(devPath2, L"Ornare.App\\bin\\Release\\net10.0-windows10.0.26100.0\\Ornare.App.exe");
                    if (PathFileExistsW(devPath2))
                    {
                        wcscpy_s(appExePath, MAX_PATH, devPath2);
                        foundExe = true;
                    }
                }
            }
        }

        if (foundExe)
        {
            std::wstring args = L"--set-folder \"";
            args += folderPath;
            args += L"\"";

            SHELLEXECUTEINFOW exeSei = {};
            exeSei.cbSize = sizeof(exeSei);
            exeSei.fMask = SEE_MASK_NOASYNC | SEE_MASK_FLAG_NO_UI;
            exeSei.lpVerb = L"open";
            exeSei.lpFile = appExePath;
            exeSei.lpParameters = args.c_str();
            exeSei.nShow = SW_SHOWNORMAL;
            ShellExecuteExW(&exeSei);
            return;
        }

        // Protocol fallback: ornare://set-folder?v=1&path=...
        std::wstring uri = L"ornare://set-folder?v=1&path=";
        uri += folderPath;

        SHELLEXECUTEINFOW sei = {};
        sei.cbSize = sizeof(sei);
        sei.fMask = SEE_MASK_NOASYNC | SEE_MASK_FLAG_NO_UI;
        sei.lpVerb = L"open";
        sei.lpFile = uri.c_str();
        sei.nShow = SW_SHOWNORMAL;
        ShellExecuteExW(&sei);
    }

    LONG m_refCount;
};

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

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
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

    return CLASS_E_CLASSNOTAVAILABLE;
}
