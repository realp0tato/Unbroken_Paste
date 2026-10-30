"""Text clipboard polling and recent-copy history, kept in memory only."""
import sys

MAX_ITEMS = 10
PREVIEW_CHARS = 28


def preview(text):
    line = " ".join(text.split())
    return line if len(line) <= PREVIEW_CHARS else line[:PREVIEW_CHARS] + "…"


class History:
    def __init__(self, backend):
        self.backend = backend
        self.items = []
        self.last_seen = backend.change_count()

    def poll(self):
        """Return True when a new text copy was recorded."""
        count = self.backend.change_count()
        if count == self.last_seen:
            return False
        self.last_seen = count
        text = self.backend.read_text()
        if not text or not text.strip():
            return False
        if text in self.items:
            self.items.remove(text)
        self.items.insert(0, text)
        del self.items[MAX_ITEMS:]
        return True

    def recopy(self, index):
        text = self.items[index]
        self.backend.write_text(text)
        self.last_seen = self.backend.change_count()
        self.items.insert(0, self.items.pop(index))

    def clear(self):
        self.items.clear()


class MacClipboard:
    SKIP = {"org.nspasteboard.ConcealedType", "org.nspasteboard.TransientType", "com.agilebits.onepassword"}

    def __init__(self):
        from AppKit import NSPasteboard, NSPasteboardTypeString
        self.pb, self.kind = NSPasteboard.generalPasteboard(), NSPasteboardTypeString

    def change_count(self):
        return self.pb.changeCount()

    def read_text(self):
        if self.SKIP & set(self.pb.types() or []):
            return None
        return self.pb.stringForType_(self.kind)

    def write_text(self, text):
        self.pb.clearContents()
        self.pb.setString_forType_(text, self.kind)


class WindowsClipboard:
    CF_UNICODETEXT = 13

    def __init__(self):
        import ctypes as C
        from ctypes import wintypes as W
        self.C = C
        u = self.u = C.WinDLL("user32", use_last_error=True)
        k = self.k = C.WinDLL("kernel32", use_last_error=True)
        u.OpenClipboard.argtypes, u.OpenClipboard.restype = [W.HWND], W.BOOL
        u.CloseClipboard.restype = W.BOOL
        u.EmptyClipboard.restype = W.BOOL
        u.GetClipboardData.argtypes, u.GetClipboardData.restype = [W.UINT], W.HANDLE
        u.SetClipboardData.argtypes, u.SetClipboardData.restype = [W.UINT, W.HANDLE], W.HANDLE
        u.IsClipboardFormatAvailable.argtypes, u.IsClipboardFormatAvailable.restype = [W.UINT], W.BOOL
        u.RegisterClipboardFormatW.argtypes, u.RegisterClipboardFormatW.restype = [W.LPCWSTR], W.UINT
        u.GetClipboardSequenceNumber.restype = W.DWORD
        k.GlobalAlloc.argtypes, k.GlobalAlloc.restype = [W.UINT, C.c_size_t], W.HGLOBAL
        k.GlobalLock.argtypes, k.GlobalLock.restype = [W.HGLOBAL], W.LPVOID
        k.GlobalUnlock.argtypes, k.GlobalUnlock.restype = [W.HGLOBAL], W.BOOL
        k.GlobalFree.argtypes, k.GlobalFree.restype = [W.HGLOBAL], W.HGLOBAL
        # Password managers mark secrets with this format; never record them.
        self.exclude = u.RegisterClipboardFormatW("ExcludeClipboardContentFromMonitorProcessing")

    def change_count(self):
        return self.u.GetClipboardSequenceNumber()

    def read_text(self):
        if not self.u.OpenClipboard(None):
            return None
        try:
            if self.u.IsClipboardFormatAvailable(self.exclude):
                return None
            handle = self.u.GetClipboardData(self.CF_UNICODETEXT)
            if not handle:
                return None
            pointer = self.k.GlobalLock(handle)
            if not pointer:
                return None
            try:
                return self.C.wstring_at(pointer)
            finally:
                self.k.GlobalUnlock(handle)
        finally:
            self.u.CloseClipboard()

    def write_text(self, text):
        data = (text + "\0").encode("utf-16-le")
        if not self.u.OpenClipboard(None):
            return
        try:
            self.u.EmptyClipboard()
            handle = self.k.GlobalAlloc(0x0002, len(data))
            if not handle:
                return
            pointer = self.k.GlobalLock(handle)
            self.C.memmove(pointer, data, len(data))
            self.k.GlobalUnlock(handle)
            if not self.u.SetClipboardData(self.CF_UNICODETEXT, handle):
                self.k.GlobalFree(handle)
        finally:
            self.u.CloseClipboard()


def create_history():
    return History(MacClipboard() if sys.platform == "darwin" else WindowsClipboard())
