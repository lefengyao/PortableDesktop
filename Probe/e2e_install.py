"""端到端实测便携桌面的单文件安装程序：安装 -> 检查落盘 -> （另跑卸载脚本）。

为什么必须实测：静默分析只能证明"配置看起来对"，但老板反馈的是**实际行为**。
本脚本用 UIA 走正常安装流程（不是 /quiet），带 /log 记录全过程。

安全边界：
  - 安装目标是 %LocalAppData%\\Programs\\PortableDesktop（用户目录，全新）
  - 不碰 %LocalAppData%\\PortableDesktop（老板的真实数据：items.json / settings.json）
  - 每一步都截图 + 记录控件文字，可直接给人看

用法：python -u e2e_install.py
"""
import ctypes
import ctypes.wintypes as wt
import os
import subprocess
import sys
import threading
import time

from PIL import Image
import uiautomation as auto

EXE = r"D:\生产项目\项目\AI工作流\便携桌面\installer\PortableDesktop-Setup-v1.1.1.exe"
SHOT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "shots")
LOG = os.path.join(os.environ["TEMP"], "pd_install.log")

user32 = ctypes.windll.user32
gdi32 = ctypes.windll.gdi32


class RECT(ctypes.Structure):
    _fields_ = [("left", wt.LONG), ("top", wt.LONG), ("right", wt.LONG), ("bottom", wt.LONG)]


user32.GetWindowDC.argtypes = [wt.HWND]
user32.GetWindowDC.restype = ctypes.c_void_p
user32.ReleaseDC.argtypes = [wt.HWND, ctypes.c_void_p]
user32.ReleaseDC.restype = ctypes.c_int
user32.GetWindowRect.argtypes = [wt.HWND, ctypes.POINTER(RECT)]
user32.GetWindowRect.restype = wt.BOOL
user32.PrintWindow.argtypes = [wt.HWND, ctypes.c_void_p, wt.UINT]
user32.PrintWindow.restype = wt.BOOL

gdi32.CreateCompatibleDC.argtypes = [ctypes.c_void_p]
gdi32.CreateCompatibleDC.restype = ctypes.c_void_p
gdi32.CreateCompatibleBitmap.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_int]
gdi32.CreateCompatibleBitmap.restype = ctypes.c_void_p
gdi32.SelectObject.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
gdi32.SelectObject.restype = ctypes.c_void_p
gdi32.DeleteObject.argtypes = [ctypes.c_void_p]
gdi32.DeleteObject.restype = wt.BOOL
gdi32.DeleteDC.argtypes = [ctypes.c_void_p]
gdi32.DeleteDC.restype = wt.BOOL
gdi32.GetDIBits.argtypes = [
    ctypes.c_void_p, ctypes.c_void_p, wt.UINT, wt.UINT,
    ctypes.c_void_p, ctypes.c_void_p, wt.UINT,
]
gdi32.GetDIBits.restype = ctypes.c_int

try:
    ctypes.windll.shcore.SetProcessDpiAwareness(2)
except Exception:  # noqa: BLE001
    user32.SetProcessDPIAware()

PW_CLIENTONLY = 0x00000001
PW_RENDERFULLCONTENT = 0x00000002
DIB_RGB_COLORS = 0

WNDENUMPROC = ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM)


def child_pids(pid):
    try:
        ps = subprocess.run(
            ["powershell", "-NoProfile", "-Command",
             f"(Get-CimInstance Win32_Process -Filter \"ParentProcessId={pid}\").ProcessId"],
            capture_output=True, text=True, timeout=20,
        )
        return [int(x) for x in ps.stdout.split() if x.strip().isdigit()]
    except Exception:  # noqa: BLE001
        return []


def _enum_for_pid(pid):
    found = []

    def cb(hwnd, _lparam):
        wpid = wt.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(wpid))
        if wpid.value != pid or not user32.IsWindowVisible(hwnd):
            return True
        length = user32.GetWindowTextLengthW(hwnd)
        buf = ctypes.create_unicode_buffer(length + 2)
        user32.GetWindowTextW(hwnd, buf, length + 2)
        r = RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(r))
        found.append((hwnd, buf.value, (r.left, r.top, r.right, r.bottom)))
        return True

    user32.EnumWindows(WNDENUMPROC(cb), 0)
    return found


def find_ui_windows(pid):
    pids = {pid} | set(child_pids(pid))
    for c in list(pids):
        if c != pid:
            pids |= set(child_pids(c))
    wins = []
    for p in pids:
        wins.extend(_enum_for_pid(p))
    return wins


def find_big_window(pid):
    cand = [w for w in find_ui_windows(pid)
            if (w[2][2] - w[2][0]) > 200 and (w[2][3] - w[2][1]) > 200]
    if not cand:
        return None
    return max(cand, key=lambda x: (x[2][2] - x[2][0]) * (x[2][3] - x[2][1]))[0]


class BITMAPINFOHEADER(ctypes.Structure):
    _fields_ = [
        ("biSize", wt.DWORD), ("biWidth", wt.LONG), ("biHeight", wt.LONG),
        ("biPlanes", wt.WORD), ("biBitCount", wt.WORD), ("biCompression", wt.DWORD),
        ("biSizeImage", wt.DWORD), ("biXPelsPerMeter", wt.LONG), ("biYPelsPerMeter", wt.LONG),
        ("biClrUsed", wt.DWORD), ("biClrImportant", wt.DWORD),
    ]


class BITMAPINFO(ctypes.Structure):
    _fields_ = [("bmiHeader", BITMAPINFOHEADER), ("bmiColors", wt.DWORD * 3)]


def print_window(hwnd, path):
    r = RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(r))
    w, h = r.right - r.left, r.bottom - r.top
    if w <= 0 or h <= 0:
        return (w, h)
    hdc = user32.GetWindowDC(hwnd)
    memdc = gdi32.CreateCompatibleDC(hdc)
    bmp = gdi32.CreateCompatibleBitmap(hdc, w, h)
    if not (hdc and memdc and bmp):
        return (w, h)
    gdi32.SelectObject(memdc, bmp)
    if not user32.PrintWindow(hwnd, memdc, PW_CLIENTONLY | PW_RENDERFULLCONTENT):
        user32.PrintWindow(hwnd, memdc, 0)
    bmi = BITMAPINFO()
    bmi.bmiHeader.biSize = ctypes.sizeof(BITMAPINFOHEADER)
    bmi.bmiHeader.biWidth = w
    bmi.bmiHeader.biHeight = -h
    bmi.bmiHeader.biPlanes = 1
    bmi.bmiHeader.biBitCount = 32
    buf = ctypes.create_string_buffer(w * h * 4)
    got = gdi32.GetDIBits(memdc, bmp, 0, h, buf, ctypes.byref(bmi), DIB_RGB_COLORS)
    gdi32.DeleteObject(bmp)
    gdi32.DeleteDC(memdc)
    user32.ReleaseDC(hwnd, hdc)
    if not got:
        return (w, h)
    Image.frombuffer("RGBA", (w, h), buf.raw, "raw", "BGRA", 0, 1).convert("RGB").save(path)
    return (w, h)


def read_controls(hwnd, max_depth=2):
    out = []

    def work():
        with auto.UIAutomationInitializerInThread():
            win = auto.ControlFromHandle(hwnd)
            for c in win.GetChildren():
                try:
                    out.append((c.ControlTypeName, c.Name or ""))
                    if max_depth > 1:
                        for cc in c.GetChildren():
                            out.append(("  " + cc.ControlTypeName, cc.Name or ""))
                except Exception:  # noqa: BLE001
                    continue

    th = threading.Thread(target=work, daemon=True)
    th.start()
    th.join(timeout=45)
    return out


def invoke_button(hwnd, keyword):
    hit = {"ok": False}

    def work():
        with auto.UIAutomationInitializerInThread():
            win = auto.ControlFromHandle(hwnd)
            for c in win.GetChildren():
                try:
                    if c.ControlTypeName == "ButtonControl" and keyword in (c.Name or ""):
                        c.GetInvokePattern().Invoke()
                        hit["ok"] = True
                        return
                except Exception:  # noqa: BLE001
                    continue

    th = threading.Thread(target=work, daemon=True)
    th.start()
    th.join(timeout=45)
    return hit["ok"]


def snapshot(tag):
    """记录安装状态：目录 + 注册表（不含数据目录）。"""
    lines = [f"--- {tag} ---"]
    inst = os.path.join(os.environ["LOCALAPPDATA"], "Programs", "PortableDesktop")
    if os.path.isdir(inst):
        for root, _dirs, files in os.walk(inst):
            for fn in files:
                p = os.path.join(root, fn)
                lines.append(f"DIR {p} ({os.path.getsize(p)} bytes)")
            if not files:
                lines.append(f"DIR {root} (空目录)")
    else:
        lines.append(f"未创建：{inst}")
    desktop = os.path.join(os.environ["USERPROFILE"], "Desktop", "便携桌面.lnk")
    lines.append(("桌面快捷方式存在" if os.path.exists(desktop) else "桌面快捷方式不存在"))
    menu = os.path.join(os.environ["APPDATA"], "Microsoft", "Windows", "Start Menu", "Programs", "便携桌面")
    lines.append((f"开始菜单目录存在：{menu}" if os.path.exists(menu) else f"开始菜单目录不存在：{menu}"))
    cache = os.path.join(os.environ["LOCALAPPDATA"], "Package Cache")
    if os.path.isdir(cache):
        for d in os.listdir(cache):
            if "PortableDesktop" in d or "BEFCD536" in d.upper():
                lines.append(f"PackageCache {d}")
    return lines


def main():
    os.makedirs(SHOT_DIR, exist_ok=True)
    for old in ("pd_install.log",):
        p = os.path.join(os.environ["TEMP"], old)
        if os.path.exists(p):
            os.remove(p)

    out = []
    out.append("=========== 安装前状态 ===========")
    out.extend(snapshot("before"))

    proc = subprocess.Popen([EXE, "/log", LOG])
    print(f"启动 pid={proc.pid} log={LOG}", flush=True)

    hwnd = None
    for i in range(40):
        time.sleep(0.7)
        hwnd = find_big_window(proc.pid)
        if hwnd:
            break
    if not hwnd:
        print("没等到窗口", flush=True)
        proc.kill()
        return 1

    time.sleep(2.0)
    print_window(hwnd, os.path.join(SHOT_DIR, "e2e_01_welcome.png"))

    print("点击「安装(I)」…", flush=True)
    ok = invoke_button(hwnd, "安装")
    print(f"  点击结果 = {ok}", flush=True)

    done = False
    for tick in range(1, 61):  # 最多 180 秒
        time.sleep(3)
        hwnd = find_big_window(proc.pid)
        if not hwnd:
            print(f"  t={tick*3}s 窗口消失了（进程退出码 {proc.poll()}）", flush=True)
            done = True
            break
        ctrls = read_controls(hwnd, max_depth=1)
        names = " ".join(n for _t, n in ctrls if n.strip())
        short = names[:160]
        print(f"  t={tick*3}s {short}", flush=True)
        # 注意：不能拿"取消"当结束标志 —— 欢迎页/进度页本来就有「取消(C)」按钮
        if any(k in names for k in ("完成", "失败", "错误", "关闭")):
            time.sleep(2)
            hwnd = find_big_window(proc.pid)
            if hwnd:
                print_window(hwnd, os.path.join(SHOT_DIR, "e2e_02_result.png"))
            ctrls = read_controls(hwnd) if hwnd else []
            out.append("=========== 结束页控件 ===========")
            for t, n in ctrls:
                if n.strip():
                    out.append(f"  [{t}] {n!r}")
            done = True
            break

    if not done:
        out.append("（180 秒内没进入结束页，仍在进行）")

    out.append("=========== 安装后状态 ===========")
    out.extend(snapshot("after"))

    # 读日志尾部
    out.append("=========== 安装日志尾部 ===========")
    if os.path.exists(LOG):
        with open(LOG, encoding="utf-8", errors="replace") as fh:
            tail = fh.readlines()[-60:]
        out.extend(l.rstrip() for l in tail)
    else:
        out.append("（没有日志文件）")

    out.append("=========== 进程状态 ===========")
    out.append(f"父进程退出码={proc.poll()}，窗口句柄={hwnd}")

    with open(os.path.join(os.environ["TEMP"], "pd_e2e_install.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(out))

    for line in out:
        print(line, flush=True)

    print("\n(安装程序窗口保持打开，由后续脚本/人工关闭)", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
