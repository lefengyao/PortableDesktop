"""在沙箱里运行 dotnet 的修正环境启动器。

沙箱剥离了 `ProgramFiles(x86)`、`APPDATA` 等变量，而 NuGet 在 Windows 上取机器级设置目录用的是
`EnvironmentVariableReader.GetEnvironmentVariable("PROGRAMFILES(X86)")`（见 NuGetEnvironment.cs），
拿不到就 Path.Combine(null, "NuGet") 崩溃。bash 无法 export 带括号的变量名，只有 Python 能设。

用法： python .dotnet.py <restore|publish|build|msbuild...> [参数...]
"""
import os
import subprocess
import sys

ENV_PATCH = {
    # 关键：带括号的变量名 bash 设不了，必须在这里设
    "ProgramFiles(x86)": r"C:\Program Files (x86)",
    "ProgramFiles": r"C:\Program Files",
    "ProgramW6432": r"C:\Program Files",
    "CommonProgramFiles(x86)": r"C:\Program Files (x86)\Common Files",
    # NuGet 用户级设置目录 %APPDATA%\NuGet
    "APPDATA": r"C:\Users\15610\AppData\Roaming",
    "LOCALAPPDATA": r"C:\Users\15610\AppData\Local",
    "ProgramData": r"C:\ProgramData",
    "ALLUSERSPROFILE": r"C:\ProgramData",
    "USERPROFILE": r"C:\Users\15610",
    "HOMEDRIVE": "C:",
    "HOMEPATH": r"\Users\15610",
    "SystemDrive": "C:",
    "windir": r"C:\Windows",
}


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2

    env = dict(os.environ)
    for key, value in ENV_PATCH.items():
        env[key] = value

    cmd = ["dotnet", *sys.argv[1:]]
    print("$", cmd[0], " ".join(cmd[1:]))
    proc = subprocess.run(cmd, env=env, text=True, encoding="utf-8", errors="replace")
    return proc.returncode


if __name__ == "__main__":
    sys.exit(main())
