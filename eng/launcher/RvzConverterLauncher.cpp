#include <windows.h>
#include <shellapi.h>
#include <string>
#include <vector>

static std::wstring QuoteArgument(const wchar_t* value)
{
    std::wstring result = L"\"";
    size_t slashes = 0;
    for (const wchar_t* p = value; ; ++p)
    {
        if (*p == L'\\') { ++slashes; continue; }
        if (*p == L'\"')
        {
            result.append(slashes * 2 + 1, L'\\');
            result.push_back(L'\"');
            slashes = 0;
            continue;
        }
        if (*p == L'\0')
        {
            result.append(slashes * 2, L'\\');
            result.push_back(L'\"');
            break;
        }
        result.append(slashes, L'\\');
        slashes = 0;
        result.push_back(*p);
    }
    return result;
}

static bool GetLauncherDirectory(std::wstring& directory)
{
    std::vector<wchar_t> path(512);
    for (;;)
    {
        DWORD length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
        if (length == 0) return false;
        if (length < path.size() - 1)
        {
            std::wstring full(path.data(), length);
            size_t separator = full.find_last_of(L"\\/");
            if (separator == std::wstring::npos) return false;
            directory.assign(full, 0, separator);
            return true;
        }
        if (path.size() >= 32768) return false;
        path.resize(path.size() * 2);
    }
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    std::wstring directory;
    if (!GetLauncherDirectory(directory))
    {
        MessageBoxW(nullptr, L"RVZ Converter could not determine its installation folder.", L"RVZ Converter", MB_OK | MB_ICONERROR);
        return ERROR_BAD_PATHNAME;
    }

    const std::wstring application = directory + L"\\app\\RvzConverter.exe";
    const DWORD attributes = GetFileAttributesW(application.c_str());
    if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
    {
        MessageBoxW(nullptr,
            L"RVZ Converter could not find app\\RvzConverter.exe. Extract the complete archive and keep the app folder beside RVZConverter.exe.",
            L"RVZ Converter files are missing", MB_OK | MB_ICONERROR);
        return ERROR_FILE_NOT_FOUND;
    }

    int argumentCount = 0;
    wchar_t** arguments = CommandLineToArgvW(GetCommandLineW(), &argumentCount);
    if (arguments == nullptr)
    {
        MessageBoxW(nullptr, L"RVZ Converter could not read its command-line arguments.", L"RVZ Converter", MB_OK | MB_ICONERROR);
        return ERROR_INVALID_PARAMETER;
    }

    std::wstring commandLine = QuoteArgument(application.c_str());
    for (int i = 1; i < argumentCount; ++i)
    {
        commandLine.push_back(L' ');
        commandLine += QuoteArgument(arguments[i]);
    }
    LocalFree(arguments);

    std::vector<wchar_t> writable(commandLine.begin(), commandLine.end());
    writable.push_back(L'\0');
    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    if (!CreateProcessW(application.c_str(), writable.data(), nullptr, nullptr, FALSE, 0, nullptr, nullptr, &startup, &process))
    {
        const DWORD error = GetLastError();
        std::wstring message = L"RVZ Converter could not start app\\RvzConverter.exe. Windows error code: " + std::to_wstring(error) + L".";
        MessageBoxW(nullptr, message.c_str(), L"RVZ Converter could not start", MB_OK | MB_ICONERROR);
        return static_cast<int>(error);
    }

    CloseHandle(process.hThread);
    WaitForSingleObject(process.hProcess, INFINITE);
    DWORD exitCode = ERROR_GEN_FAILURE;
    GetExitCodeProcess(process.hProcess, &exitCode);
    CloseHandle(process.hProcess);
    return static_cast<int>(exitCode);
}
