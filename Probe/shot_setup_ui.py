"""验证便携桌面的单文件安装程序界面：欢迎页 / 选项页（安装位置）实际显示成什么样。

直接从 OpsHelper/Probe/shot_zh_ui.py 移植（那套已经踩平 5 个坑，详见技能
wixstdba-ui-verification-windows）。这里的用途是回答老板的疑问：
  "为什么安装程序不会自动新建文件夹安装？"

只读验证：打开界面 -> 截图 -> 读控件文字 -> 点「选项」再看一遍 -> WM_CLOSE 关闭。
**绝不点「安装」**，不改动系统状态。

用法：python -u shot_setup_ui.py
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

EXE = r"D:\生产项目\项目\AI工作流\便携桌面\installer\PortableDesktop-Setup-v1.1.2.exe"
SHOT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "shots")

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
        return None, (w, h)

    hdc = user32.GetWindowDC(hwnd)
    memdc = gdi32.CreateCompatibleDC(hdc)
    bmp = gdi32.CreateCompatibleBitmap(hdc, w, h)
    if not (hdc and memdc and bmp):
        return None, (w, h)
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
        return None, (w, h)

    img = Image.frombuffer("RGBA", (w, h), buf.raw, "raw", "BGRA", 0, 1).convert("RGB")
    img.save(path)
    return img, (w, h)


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


def main():
    os.makedirs(SHOT_DIR, exist_ok=True)
    if not os.path.exists(EXE):
        print(f"找不到安装程序：{EXE}", flush=True)
        return 2

    print(f"启动：{os.path.basename(EXE)}", flush=True)
    proc = subprocess.Popen([EXE])
    print(f"父进程 pid = {proc.pid}", flush=True)

    hwnd = None
    for i in range(40):
        time.sleep(0.7)
        cand = [w for w in find_ui_windows(proc.pid)
                if (w[2][2] - w[2][0]) > 200 and (w[2][3] - w[2][1]) > 200]
        if cand:
            hwnd, title, rect = max(cand, key=lambda x: (x[2][2] - x[2][0]) * (x[2][3] - x[2][1]))
            print(f"第 {i + 1} 次探测到已就绪窗口：{title!r} {rect}", flush=True)
            break

    if not hwnd:
        print("28 秒内没等到有尺寸的窗口 —— 安装程序可能启动就失败了", flush=True)
        alive = proc.poll()
        print(f"父进程退出码 = {alive}", flush=True)
        try:
            proc.kill()
        except Exception:  # noqa: BLE001
            pass
        return 1

    time.sleep(2.0)

    p1 = os.path.join(SHOT_DIR, "setup_01_welcome.png")
    _, size1 = print_window(hwnd, p1)
    ctrls1 = read_controls(hwnd)
    texts1 = [n for _t, n in ctrls1 if n.strip()]
    print(f"\n欢迎页截图：{p1}  尺寸={size1}", flush=True)
    for t, n in ctrls1:
        if n.strip():
            print(f"  [{t}] {n!r}", flush=True)

    print("\n点击「选项」…", flush=True)
    clicked = invoke_button(hwnd, "选项")
    time.sleep(2.5)
    p2 = os.path.join(SHOT_DIR, "setup_02_options.png")
    _, size2 = print_window(hwnd, p2)
    ctrls2 = read_controls(hwnd)
    texts2 = [n for _t, n in ctrls2 if n.strip()]
    print(f"选项页截图：{p2}  尺寸={size2}", flush=True)
    for t, n in ctrls2:
        if n.strip():
            print(f"  [{t}] {n!r}", flush=True)

    j1, j2 = "\n".join(texts1), "\n".join(texts2)
    checks = [
        ("[欢迎页] 中文标题（欢迎使用）", "欢迎使用" in j1),
        ("[欢迎页] 按钮中文化", "安装(I)" in j1 or "安装" in j1),
        ("[选项页] 点「选项」有反应", clicked),
        ("[选项页] 安装位置标签", "安装位置" in j2),
        ("[选项页] 存在可编辑输入框", any(t.strip() == "EditControl" for t, _n in ctrls2)),
    ]
    print("\n=== 判定 ===", flush=True)
    for label, ok in checks:
        print(f"  {'[OK]  ' if ok else '[FAIL]'} {label}", flush=True)

    print("\n关闭安装程序（不执行安装）…", flush=True)
    user32.PostMessageW(hwnd, 0x0010, 0, 0)
    time.sleep(1.5)
    if proc.poll() is None:
        proc.kill()
    print("已关闭。", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
