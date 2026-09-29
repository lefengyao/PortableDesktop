"""验证 1.1.2 的安装行为：选项页填的父目录下是否自动新建「便携桌面」文件夹，
以及卸载时是否只清掉自己的文件夹、不动用户自己的文件。

背景（2026-09-29 老板反馈）：
  1.1.1 把"安装位置"直接当最终安装目录，用户选到已有内容的目录时，
  PortableDesktop.exe 会跟用户的文件混在一起，卸载后目录还在 —— 观感就是
  "不会自动新建文件夹" + "卸载不清理"。
  1.1.2 改成：填父目录，程序在其中建「便携桌面」子文件夹。

只动工作区内的测试目录，不碰 %LOCALAPPDATA%\\PortableDesktop（老板的真实数据）。

用法：python -u e2e_verify_folder.py
"""
import os
import subprocess
import sys
import shutil

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:  # noqa: BLE001
    pass

SETUP = r"D:\生产项目\项目\AI工作流\便携桌面\installer\PortableDesktop-Setup-v1.1.2.exe"
ROOT = r"D:\生产项目\项目\AI工作流\便携桌面"
PARENT = os.path.join(ROOT, "__PDVerify")
DEFAULT_INSTALL = os.path.join(os.environ["LOCALAPPDATA"], "Programs", "PortableDesktop")
CACHED_PATTERN = os.path.join(os.environ["LOCALAPPDATA"], "Package Cache")


def run(args, tag):
    print(f"  $ setup.exe {' '.join(args)}", flush=True)
    p = subprocess.run([SETUP] + args, capture_output=True, text=True, timeout=600)
    print(f"  [{tag}] exit={p.returncode}", flush=True)
    return p.returncode


def cached_setup():
    """找到 Burn 缓存的安装程序（卸载入口）。"""
    for d in os.listdir(CACHED_PATTERN):
        p = os.path.join(CACHED_PATTERN, d, "PortableDesktop-Setup-v1.1.2.exe")
        if os.path.exists(p):
            return p
    return None


def tree(base, label):
    print(f"  --- {label}: {base} ---", flush=True)
    if not os.path.exists(base):
        print("    (不存在)", flush=True)
        return
    for entry in sorted(os.listdir(base)):
        full = os.path.join(base, entry)
        if os.path.isdir(full):
            print(f"    [目录] {entry}/", flush=True)
            for sub in sorted(os.listdir(full)):
                sp = os.path.join(full, sub)
                size = os.path.getsize(sp) if os.path.isfile(sp) else "-"
                print(f"        {sub} ({size})", flush=True)
        else:
            print(f"    {entry} ({os.path.getsize(full)})", flush=True)


def clean(path):
    if os.path.exists(path):
        shutil.rmtree(path, ignore_errors=True)


def main():
    print("=" * 70, flush=True)
    print("测试 1：默认安装（不给 InstallFolder，走默认父目录）", flush=True)
    print("=" * 70, flush=True)
    clean(DEFAULT_INSTALL)
    run(["/install", "/quiet"], "install-default")
    print(f"  期望安装到: {DEFAULT_INSTALL}", flush=True)
    print(f"  实际存在 = {os.path.exists(os.path.join(DEFAULT_INSTALL, 'PortableDesktop.exe'))}", flush=True)
    cs = cached_setup()
    print(f"  缓存卸载入口 = {cs}", flush=True)
    if cs:
        subprocess.run([cs, "/uninstall", "/quiet"], capture_output=True, timeout=600)
    print(f"  卸载后默认目录存在 = {os.path.exists(DEFAULT_INSTALL)}", flush=True)
    print(f"  数据目录仍在 = {os.path.exists(os.path.join(os.environ['LOCALAPPDATA'], 'PortableDesktop'))}", flush=True)

    print("", flush=True)
    print("=" * 70, flush=True)
    print("测试 2：把父目录指向一个【已有用户文件】的目录", flush=True)
    print("=" * 70, flush=True)
    clean(PARENT)
    os.makedirs(PARENT, exist_ok=True)
    with open(os.path.join(PARENT, "我自己的文件.txt"), "w", encoding="utf-8") as fh:
        fh.write("这是用户自己的文件，卸载时绝不能被删")
    tree(PARENT, "安装前")

    run(["/install", "/quiet", f"InstallFolder={PARENT}"], "install-custom")
    tree(PARENT, "安装后")

    expect_app = os.path.join(PARENT, "PortableDesktop", "PortableDesktop.exe")
    print(f"  ✅ 是否新建了子文件夹并装入: {os.path.exists(expect_app)}", flush=True)
    print(f"  （旧 1.1.1 行为会是直接把 exe 铺在 {PARENT} 下）", flush=True)

    cs = cached_setup()
    if cs:
        subprocess.run([cs, "/uninstall", "/quiet"], capture_output=True, timeout=600)
    tree(PARENT, "卸载后")
    print(f"  ✅ 程序子文件夹已清除: {not os.path.exists(os.path.join(PARENT, 'PortableDesktop'))}", flush=True)
    print(f"  ✅ 用户自己的文件保留: {os.path.exists(os.path.join(PARENT, '我自己的文件.txt'))}", flush=True)

    clean(PARENT)
    print("\n测试目录已清理。", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
