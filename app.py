import json
import os
import subprocess
import mimetypes
from datetime import datetime
from pathlib import Path
import tkinter as tk
from tkinter import colorchooser, filedialog, messagebox, simpledialog, ttk

from PIL import Image, ImageDraw, ImageSequence, ImageTk

try:
    import cv2
except Exception:
    cv2 = None

APP_NAME = "Launcher File Explorer"
CONFIG_FILE = Path("launcher_config.json")

DEFAULT_CONFIG = {
    "theme": "arc-neon",
    "themes": {
        "arc-neon": {
            "bg": "#090b17",
            "panel": "#11162a",
            "card": "#171f39",
            "text": "#f2f4ff",
            "accent": "#ff8a3d",
            "secondary": "#6ce8ff",
            "background_image": "",
            "stickers": [],
        },
        "hollow-city": {
            "bg": "#0b0d13",
            "panel": "#171a24",
            "card": "#202533",
            "text": "#f3f5ff",
            "accent": "#ff5d63",
            "secondary": "#7ceeff",
            "background_image": "",
            "stickers": [],
        },
    },
    "preset_themes": {
        "Starter Neon": {
            "theme_name": "arc-neon",
            "theme": {
                "bg": "#090b17",
                "panel": "#11162a",
                "card": "#171f39",
                "text": "#f2f4ff",
                "accent": "#ff8a3d",
                "secondary": "#6ce8ff",
                "background_image": "",
                "stickers": [],
            },
            "sections": [],
            "file_images": {},
        }
    },
    "sections": [],
    "file_images": {},
    "explorer": {
        "view": "Details",
        "layout": "Tree + Files",
        "size": 28,
        "current_dir": "",
    },
}


class LauncherExplorerApp:
    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title(APP_NAME)
        self.root.geometry("1520x940")
        self.root.minsize(1250, 760)
        self.root.option_add("*Font", "Segoe UI 10")

        self.config_data = self.load_config()
        self.theme = self.current_theme()

        self.section_images = []
        self.file_icon_cache = {}
        self.file_icon_refs = []
        self.preview_image_ref = None
        self.preview_text_widget_font = ("Segoe UI", 10)
        self.theme_background_src = None
        self.theme_background_tk = None

        self.setup_style()
        self.build_ui()
        self.apply_theme()
        self.populate_roots()
        self.sync_explorer_controls_from_config()
        self.set_current_dir(self.config_data.get("explorer", {}).get("current_dir", ""), render=False)
        self.populate_file_view()
        self.render_sections()

        self.root.protocol("WM_DELETE_WINDOW", self.on_app_close)

    # ----------------- config -----------------
    def load_config(self):
        if not CONFIG_FILE.exists():
            return json.loads(json.dumps(DEFAULT_CONFIG))

        with CONFIG_FILE.open("r", encoding="utf-8") as f:
            loaded = json.load(f)

        merged = json.loads(json.dumps(DEFAULT_CONFIG))
        merged.update(loaded)
        merged["themes"] = {**DEFAULT_CONFIG["themes"], **loaded.get("themes", {})}
        merged["preset_themes"] = {**DEFAULT_CONFIG["preset_themes"], **loaded.get("preset_themes", {})}
        merged["explorer"] = {**DEFAULT_CONFIG["explorer"], **loaded.get("explorer", {})}

        for section in merged.get("sections", []):
            section.setdefault("name", "Unnamed")
            section.setdefault("type", "shortcut")
            section.setdefault("path", "")
            section.setdefault("image", "")
            section.setdefault("shape", "rounded")
            section.setdefault("image_mode", "contain")
            section.setdefault("scale_x", 100)
            section.setdefault("scale_y", 100)
        return merged

    def save_config(self):
        self.write_explorer_settings()
        with CONFIG_FILE.open("w", encoding="utf-8") as f:
            json.dump(self.config_data, f, indent=2)

    def on_app_close(self):
        try:
            self.save_config()
        except Exception as exc:
            messagebox.showwarning(APP_NAME, f"Could not save settings before exit:\n{exc}")
        self.root.destroy()

    # ----------------- ui scaffold -----------------
    def current_theme(self):
        name = self.config_data.get("theme", "arc-neon")
        return self.config_data["themes"].get(name, DEFAULT_CONFIG["themes"]["arc-neon"])

    def setup_style(self):
        self.style = ttk.Style()
        if "clam" in self.style.theme_names():
            self.style.theme_use("clam")

    def build_ui(self):
        top = tk.Frame(self.root, height=84)
        top.pack(fill="x", padx=12, pady=(10, 6))

        tk.Label(top, text=APP_NAME, font=("Segoe UI Semibold", 22)).pack(side="left", padx=(10, 20), pady=(8, 0))
        tk.Label(top, text="Anime launcher vibe • custom themes • rich cards", font=("Segoe UI", 10)).pack(side="left", pady=(12, 0))

        actions = tk.Frame(top)
        actions.pack(side="right", padx=8, pady=8)
        for txt, cb in [
            ("+ Add Section", self.add_section),
            ("Map File Image", self.map_file_image),
            ("Theme Studio", self.open_theme_studio),
            ("Refresh", self.refresh),
        ]:
            ttk.Button(actions, text=txt, command=cb).pack(side="left", padx=4)

        self.accent_bar = tk.Canvas(self.root, height=4, highlightthickness=0)
        self.accent_bar.pack(fill="x", padx=12, pady=(0, 8))

        self.main = tk.PanedWindow(self.root, orient="horizontal", sashwidth=8, bd=0)
        self.main.pack(fill="both", expand=True, padx=12, pady=(0, 10))

        self.left_shell = tk.Frame(self.main, width=500)
        self.right_shell = tk.Frame(self.main)
        self.main.add(self.left_shell)
        self.main.add(self.right_shell)

        self.build_left_side()
        self.build_right_side()

    def build_left_side(self):
        controls = tk.Frame(self.left_shell)
        controls.pack(fill="x", padx=6, pady=(4, 6))

        self.left_layout_var = tk.StringVar(value="Tree + Files")
        self.left_view_var = tk.StringVar(value="Details")
        self.left_size_var = tk.IntVar(value=28)
        self.current_dir_var = tk.StringVar(value="")

        tk.Label(controls, text="Layout:").pack(side="left")
        self.layout_combo = ttk.Combobox(
            controls,
            textvariable=self.left_layout_var,
            values=["Tree + Files", "Tree Only", "Files Only"],
            state="readonly",
            width=12,
        )
        self.layout_combo.pack(side="left", padx=(6, 10))
        self.layout_combo.bind("<<ComboboxSelected>>", lambda _e: self.apply_left_layout())

        tk.Label(controls, text="View:").pack(side="left")
        self.view_combo = ttk.Combobox(
            controls,
            textvariable=self.left_view_var,
            values=["Details", "List", "Icons"],
            state="readonly",
            width=9,
        )
        self.view_combo.pack(side="left", padx=(6, 10))
        self.view_combo.bind("<<ComboboxSelected>>", lambda _e: self.populate_file_view())

        tk.Label(controls, text="Size:").pack(side="left")
        self.left_size_label = tk.Label(controls, text=f"{self.left_size_var.get()}", width=3, anchor="e")
        self.left_size_label.pack(side="right", padx=(4, 2))
        self.left_size_slider = ttk.Scale(
            controls,
            from_=20,
            to=64,
            variable=self.left_size_var,
            command=lambda _v: self.on_left_size_change(),
            length=140,
        )
        self.left_size_slider.pack(side="left", padx=(6, 6))

        self.left_pane = tk.PanedWindow(self.left_shell, orient="vertical", sashwidth=6)
        self.left_pane.pack(fill="both", expand=True)

        self.tree_frame = tk.Frame(self.left_pane)
        self.files_frame = tk.Frame(self.left_pane)
        self.left_pane.add(self.tree_frame)
        self.left_pane.add(self.files_frame)

        self.tree = ttk.Treeview(self.tree_frame, columns=("fullpath",), show="tree")
        tree_scroll = ttk.Scrollbar(self.tree_frame, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=tree_scroll.set)
        self.tree.pack(side="left", fill="both", expand=True)
        tree_scroll.pack(side="right", fill="y")
        self.tree.bind("<<TreeviewOpen>>", self.on_tree_open)
        self.tree.bind("<<TreeviewSelect>>", self.on_tree_select)
        self.tree.bind("<Double-1>", self.on_tree_double_click)

        path_row = tk.Frame(self.files_frame)
        path_row.pack(fill="x", padx=4, pady=4)
        tk.Label(path_row, text="Path:").pack(side="left")
        ttk.Entry(path_row, textvariable=self.current_dir_var).pack(side="left", fill="x", expand=True, padx=(6, 6))
        ttk.Button(path_row, text="Go", command=lambda: self.set_current_dir(self.current_dir_var.get().strip())).pack(side="left")

        self.file_view = ttk.Treeview(self.files_frame, columns=("size", "type", "modified", "fullpath"), show="tree headings")
        self.file_view.heading("#0", text="Name")
        self.file_view.heading("size", text="Size")
        self.file_view.heading("type", text="Type")
        self.file_view.heading("modified", text="Modified")
        self.file_view.column("#0", width=240, anchor="w")
        self.file_view.column("size", width=90, anchor="e")
        self.file_view.column("type", width=90, anchor="w")
        self.file_view.column("modified", width=150, anchor="w")
        self.file_view.column("fullpath", width=0, stretch=False)

        file_scroll = ttk.Scrollbar(self.files_frame, orient="vertical", command=self.file_view.yview)
        self.file_view.configure(yscrollcommand=file_scroll.set)
        self.file_view.pack(side="left", fill="both", expand=True, padx=(4, 0), pady=(0, 4))
        file_scroll.pack(side="right", fill="y", pady=(0, 4))
        self.file_view.bind("<Double-1>", self.on_file_view_double_click)
        self.file_view.bind("<<TreeviewSelect>>", self.on_file_view_select)

        preview = tk.LabelFrame(self.files_frame, text="In-app preview")
        preview.pack(fill="x", padx=4, pady=(0, 4))
        self.preview_title = tk.Label(preview, text="Select a file/folder", anchor="w")
        self.preview_title.pack(fill="x", padx=6, pady=(4, 2))
        self.preview_text = tk.Text(preview, height=5, wrap="word")
        self.preview_text.pack(fill="x", padx=6, pady=(0, 6))

    def build_right_side(self):
        self.canvas = tk.Canvas(self.right_shell, highlightthickness=0)
        self.rscroll = ttk.Scrollbar(self.right_shell, orient="vertical", command=self.canvas.yview)
        self.canvas.configure(yscrollcommand=self.rscroll.set)

        self.content = tk.Frame(self.canvas)
        self.content.bind("<Configure>", lambda _e: self.canvas.configure(scrollregion=self.canvas.bbox("all")))
        self.content_window = self.canvas.create_window((0, 0), window=self.content, anchor="nw")
        self.canvas.bind("<Configure>", self._on_canvas_resize)

        self.canvas.pack(side="left", fill="both", expand=True)
        self.rscroll.pack(side="right", fill="y")

        self.zoom_var = tk.IntVar(value=100)
        self.sort_var = tk.StringVar(value="Alphabetical (A-Z)")

        toolbar = tk.Frame(self.content)
        toolbar.pack(fill="x", padx=24, pady=(18, 0))
        tk.Label(toolbar, text="Sort:", font=("Segoe UI Semibold", 10)).pack(side="left")
        self.sort_combo = ttk.Combobox(
            toolbar,
            textvariable=self.sort_var,
            values=["Alphabetical (A-Z)", "Alphabetical (Z-A)", "Size (Largest)", "Size (Smallest)", "Date (Newest)", "Date (Oldest)"],
            state="readonly",
            width=22,
        )
        self.sort_combo.pack(side="left", padx=(8, 16))
        self.sort_combo.bind("<<ComboboxSelected>>", lambda _e: self.render_sections())

        tk.Label(toolbar, text="Zoom:", font=("Segoe UI Semibold", 10)).pack(side="left")
        self.zoom_label = tk.Label(toolbar, text=f"{self.zoom_var.get()}%", width=5, anchor="e")
        self.zoom_label.pack(side="right", padx=(6, 0))
        self.zoom_slider = ttk.Scale(toolbar, from_=70, to=170, variable=self.zoom_var, length=180,
                 command=lambda _v: self.on_zoom_change())
        self.zoom_slider.pack(side="left", padx=(8, 6))

        self.cards_container = tk.Frame(self.content)
        self.cards_container.pack(fill="both", expand=True, padx=24, pady=24)


    def on_left_size_change(self):
        self.left_size_var.set(int(float(self.left_size_var.get())))
        self.left_size_label.configure(text=f"{int(self.left_size_var.get())}")
        self.populate_file_view()

    def on_zoom_change(self):
        self.zoom_var.set(int(float(self.zoom_var.get())))
        self.zoom_label.configure(text=f"{int(self.zoom_var.get())}%")
        self.render_sections()
    # ----------------- theme / coloring -----------------
    def apply_theme(self):
        self.theme = self.current_theme()
        t = self.theme

        self.root.configure(bg=t["bg"])
        self.main.configure(bg=t["bg"])
        self.accent_bar.configure(bg=t["secondary"])

        self.style.configure("TButton", padding=(12, 7), font=("Segoe UI Semibold", 10), foreground=t["text"], borderwidth=0)
        self.style.map("TButton", background=[("active", t["secondary"]), ("!active", t["card"])], foreground=[("active", "#0b0d13")])
        self.style.configure("TCombobox", fieldbackground=t["card"], background=t["card"], foreground=t["text"], arrowsize=13)
        self.style.configure("TEntry", fieldbackground=t["card"], foreground=t["text"])
        self.style.configure("Treeview", font=("Segoe UI", 10), rowheight=max(24, int(self.left_size_var.get())))
        self.style.configure("Treeview", background=t["panel"], fieldbackground=t["panel"], foreground=t["text"], borderwidth=0)
        self.style.configure("Treeview.Heading", background=t["card"], foreground=t["text"], font=("Segoe UI Semibold", 10), relief="flat")
        self.style.map("Treeview", background=[("selected", t["secondary"])], foreground=[("selected", "#0b0d13")])

        self._recolor_recursive(self.root, t)
        self._render_background()

    def _recolor_recursive(self, widget, theme):
        cls = widget.winfo_class()
        if cls in {"Frame", "PanedWindow", "Labelframe"}:
            widget.configure(bg=theme["bg"])
        elif cls == "Canvas":
            widget.configure(bg=theme["panel"])
        elif cls == "Label":
            widget.configure(bg=widget.master.cget("bg"), fg=theme["text"])
        for child in widget.winfo_children():
            self._recolor_recursive(child, theme)

    def _on_canvas_resize(self, event):
        self.canvas.itemconfig(self.content_window, width=event.width)
        self._render_background()

    def _render_background(self):
        path = self.theme.get("background_image", "")
        width = max(self.canvas.winfo_width(), 900)
        height = max(self.canvas.winfo_height(), 600)

        if not path or not os.path.exists(path):
            self.theme_background_src = None
            self.theme_background_tk = None
            self.content.configure(bg=self.theme["panel"])
            return

        if self.theme_background_src != (path, width, height):
            img = Image.open(path).convert("RGBA").resize((width, height), Image.Resampling.LANCZOS)
            shade = Image.new("RGBA", img.size, (0, 0, 0, 105))
            img.alpha_composite(shade)
            self.theme_background_tk = ImageTk.PhotoImage(img)
            self.theme_background_src = (path, width, height)

        if not hasattr(self, "background_label"):
            self.background_label = tk.Label(self.content, bd=0, highlightthickness=0)
            self.background_label.place(x=0, y=0)
        self.background_label.configure(image=self.theme_background_tk)
        self.background_label.image = self.theme_background_tk
        self.background_label.place(x=0, y=0, width=width, height=height)
        self.background_label.lower()
        self.cards_container.lift()

    # ----------------- left explorer feature set -----------------
    def sync_explorer_controls_from_config(self):
        ex = self.config_data.get("explorer", {})
        self.left_layout_var.set(ex.get("layout", "Tree + Files"))
        self.left_view_var.set(ex.get("view", "Details"))
        self.left_size_var.set(int(ex.get("size", 28)))
        self.apply_left_layout()

    def write_explorer_settings(self):
        self.config_data.setdefault("explorer", {})
        self.config_data["explorer"]["layout"] = self.left_layout_var.get()
        self.config_data["explorer"]["view"] = self.left_view_var.get()
        self.config_data["explorer"]["size"] = int(self.left_size_var.get())
        self.config_data["explorer"]["current_dir"] = self.current_dir_var.get().strip()

    def apply_left_layout(self):
        layout = self.left_layout_var.get()
        self.left_pane.forget(self.tree_frame)
        self.left_pane.forget(self.files_frame)
        if layout in {"Tree + Files", "Tree Only"}:
            self.left_pane.add(self.tree_frame)
        if layout in {"Tree + Files", "Files Only"}:
            self.left_pane.add(self.files_frame)
        self.write_explorer_settings()

    def populate_roots(self):
        self.tree.delete(*self.tree.get_children())
        if os.name == "nt":
            roots = [f"{d}:\\" for d in "ABCDEFGHIJKLMNOPQRSTUVWXYZ" if os.path.exists(f"{d}:\\")]
        else:
            roots = ["/"]
        for root in roots:
            node = self.tree.insert("", "end", text=root, values=(root,))
            self.tree.insert(node, "end", text="...")

    def on_tree_open(self, _event):
        node = self.tree.focus()
        path = self.tree.set(node, "fullpath")
        if not path:
            return
        children = self.tree.get_children(node)
        if children and self.tree.item(children[0], "text") != "...":
            return

        self.tree.delete(*children)
        try:
            for name in sorted(os.listdir(path), key=lambda x: x.lower()):
                full = os.path.join(path, name)
                child = self.tree.insert(node, "end", text=name, values=(full,))
                if os.path.isdir(full):
                    self.tree.insert(child, "end", text="...")
        except (PermissionError, FileNotFoundError):
            return

    def on_tree_select(self, _event):
        node = self.tree.focus()
        path = self.tree.set(node, "fullpath")
        if not path:
            return
        if os.path.isdir(path):
            self.set_current_dir(path)
        else:
            self.set_current_dir(os.path.dirname(path))

    def on_tree_double_click(self, _event):
        node = self.tree.focus()
        path = self.tree.set(node, "fullpath")
        if not path:
            return
        if os.path.isdir(path):
            self.set_current_dir(path)
        else:
            self.preview_file(path)
            self.open_external(path)

    def set_current_dir(self, path, render=True):
        if not path:
            return
        if os.path.isfile(path):
            path = os.path.dirname(path)
        if not os.path.isdir(path):
            return
        self.current_dir_var.set(path)
        self.write_explorer_settings()
        if render:
            self.populate_file_view()

    def map_image_for_path(self, path):
        images = self.config_data.get("file_images", {})
        if path in images and images[path]:
            return images[path]
        ext = Path(path).suffix.lower()
        if ext in images and images[ext]:
            return images[ext]
        return ""

    def icon_for_path(self, path):
        size = max(18, int(self.left_size_var.get()))
        key = (path, size)
        if key in self.file_icon_cache:
            return self.file_icon_cache[key]

        img_path = self.map_image_for_path(path)
        if img_path and os.path.exists(img_path):
            try:
                icon = Image.open(img_path).convert("RGBA").resize((size, size), Image.Resampling.LANCZOS)
            except Exception:
                icon = None
        else:
            icon = None

        if icon is None:
            icon = Image.new("RGBA", (size, size), (0, 0, 0, 0))
            d = ImageDraw.Draw(icon)
            if os.path.isdir(path):
                color = "#8ca9c8"
                d.rounded_rectangle((1, size * 0.22, size - 2, size - 2), radius=4, fill=color)
                d.rectangle((2, 2, size * 0.6, size * 0.35), fill=color)
            else:
                color = "#a9b6cc"
                d.rounded_rectangle((2, 2, size - 2, size - 2), radius=4, fill=color)

        tk_img = ImageTk.PhotoImage(icon)
        self.file_icon_cache[key] = tk_img
        self.file_icon_refs.append(tk_img)
        return tk_img

    def populate_file_view(self):
        for row in self.file_view.get_children():
            self.file_view.delete(row)

        current = self.current_dir_var.get().strip()
        if not current or not os.path.isdir(current):
            return

        mode = self.left_view_var.get()
        if mode == "Details":
            self.file_view.configure(show="tree headings")
            self.file_view.column("#0", width=250)
            self.file_view.column("size", width=90, stretch=True)
            self.file_view.column("type", width=90, stretch=True)
            self.file_view.column("modified", width=150, stretch=True)
        else:
            self.file_view.configure(show="tree")
            self.file_view.column("#0", width=360)
            self.file_view.column("size", width=0, stretch=False)
            self.file_view.column("type", width=0, stretch=False)
            self.file_view.column("modified", width=0, stretch=False)

        row_h = max(22, int(self.left_size_var.get()) + (12 if mode == "Icons" else 2))
        self.style.configure("Treeview", rowheight=row_h)

        try:
            names = sorted(os.listdir(current), key=lambda s: s.lower())
        except (PermissionError, FileNotFoundError):
            return

        for name in names:
            full = os.path.join(current, name)
            is_dir = os.path.isdir(full)
            size = "" if is_dir else self._format_size(os.path.getsize(full))
            typ = "Folder" if is_dir else (Path(full).suffix.lower().lstrip(".") or "file")
            try:
                modified = datetime.fromtimestamp(os.path.getmtime(full)).strftime("%Y-%m-%d %H:%M")
            except OSError:
                modified = ""
            icon = self.icon_for_path(full)
            text = name if mode != "Icons" else f"   {name}"
            self.file_view.insert("", "end", text=text, image=icon, values=(size, typ, modified, full))

        self.write_explorer_settings()

    def on_file_view_select(self, _event):
        sel = self.file_view.selection()
        if not sel:
            return
        path = self.file_view.set(sel[0], "fullpath")
        if path:
            self.preview_file(path)

    def on_file_view_double_click(self, _event):
        sel = self.file_view.selection()
        if not sel:
            return
        path = self.file_view.set(sel[0], "fullpath")
        if not path:
            return
        if os.path.isdir(path):
            self.set_current_dir(path)
        else:
            self.preview_file(path)
            self.open_external(path)

    def preview_file(self, path):
        self.preview_title.configure(text=path)
        self.preview_text.delete("1.0", "end")
        self.preview_image_ref = None

        if os.path.isdir(path):
            try:
                count = len(os.listdir(path))
            except Exception:
                count = 0
            self.preview_text.insert("end", f"Folder\nItems: {count}\n\nDouble-click folders to open them in-app.")
            return

        ext = Path(path).suffix.lower()
        mime, _ = mimetypes.guess_type(path)

        if ext in {".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp"}:
            try:
                img = Image.open(path).convert("RGBA")
                img.thumbnail((300, 220), Image.Resampling.LANCZOS)
                self.preview_image_ref = ImageTk.PhotoImage(img)
                self.preview_text.image_create("end", image=self.preview_image_ref)
                self.preview_text.insert("end", "\n\nImage preview in-app.")
                return
            except Exception:
                pass

        if ext in {".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv"} or (mime and mime.startswith("video/")):
            thumb = self.video_thumbnail(path)
            if thumb is not None:
                self.preview_image_ref = thumb
                self.preview_text.image_create("end", image=self.preview_image_ref)
                self.preview_text.insert("end", "\n\nVideo thumbnail preview in-app. Double-click to open with your system player.")
                return
            self.preview_text.insert("end", "Video detected. Thumbnail could not be generated on this environment.")
            return

        if ext in {".txt", ".md", ".json", ".py", ".ini", ".log", ".csv", ".js", ".html", ".css"}:
            try:
                with open(path, "r", encoding="utf-8", errors="ignore") as f:
                    self.preview_text.insert("end", f.read(6000))
                return
            except Exception:
                pass

        self.preview_text.insert("end", "Binary/unsupported preview. Double-click to open with system default app.")

    def video_thumbnail(self, path):
        if cv2 is None:
            return None
        try:
            cap = cv2.VideoCapture(path)
            if not cap.isOpened():
                return None
            ok, frame = cap.read()
            cap.release()
            if not ok or frame is None:
                return None
            frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            img = Image.fromarray(frame)
            img.thumbnail((300, 220), Image.Resampling.LANCZOS)
            return ImageTk.PhotoImage(img)
        except Exception:
            return None

    def _format_size(self, size):
        units = ["B", "KB", "MB", "GB", "TB"]
        value = float(size)
        idx = 0
        while value >= 1024 and idx < len(units) - 1:
            value /= 1024
            idx += 1
        return f"{value:.1f} {units[idx]}"

    # ----------------- sections (right side) -----------------
    def refresh(self):
        self.apply_theme()
        self.populate_roots()
        self.populate_file_view()
        self.render_sections()

    def add_section(self):
        self.section_editor_window()

    def edit_section(self, index):
        self.section_editor_window(index)

    def section_editor_window(self, index=None):
        editing = index is not None
        source = self.config_data["sections"][index] if editing else {
            "name": "",
            "type": "shortcut",
            "path": "",
            "image": "",
            "shape": "rounded",
            "image_mode": "contain",
            "scale_x": 100,
            "scale_y": 100,
        }

        win = tk.Toplevel(self.root)
        win.title("Edit Section" if editing else "Create Section")
        win.geometry("760x560")
        win.transient(self.root)

        shell = tk.Frame(win)
        shell.pack(fill="both", expand=True, padx=14, pady=14)
        left = tk.Frame(shell)
        left.pack(side="left", fill="both", expand=True)
        right = tk.LabelFrame(shell, text="Live Preview")
        right.pack(side="right", fill="both", padx=(14, 0))

        def make_row(parent, label_text, initial, browse=None):
            row = tk.Frame(parent)
            row.pack(fill="x", pady=5)
            tk.Label(row, text=label_text, width=14, anchor="w").pack(side="left")
            var = tk.StringVar(value=initial)
            ttk.Entry(row, textvariable=var).pack(side="left", fill="x", expand=True, padx=(0, 6))
            if browse:
                ttk.Button(row, text="Browse", command=lambda: browse(var)).pack(side="left")
            return var

        name_var = make_row(left, "Name", source.get("name", ""))

        trow = tk.Frame(left)
        trow.pack(fill="x", pady=5)
        tk.Label(trow, text="Section type", width=14, anchor="w").pack(side="left")
        type_var = tk.StringVar(value=source.get("type", "shortcut"))
        ttk.Combobox(trow, textvariable=type_var, values=["shortcut", "collection"], state="readonly").pack(side="left", fill="x", expand=True)

        path_var = make_row(left, "Target", source.get("path", ""), browse=self._pick_target)
        image_var = make_row(left, "Image", source.get("image", ""), browse=self._pick_image)

        srow = tk.Frame(left)
        srow.pack(fill="x", pady=5)
        tk.Label(srow, text="Shape", width=14, anchor="w").pack(side="left")
        shape_var = tk.StringVar(value=source.get("shape", "rounded"))
        ttk.Combobox(srow, textvariable=shape_var, values=["rounded", "rectangle", "circle", "hexagon"], state="readonly").pack(side="left", fill="x", expand=True)

        mrow = tk.Frame(left)
        mrow.pack(fill="x", pady=5)
        tk.Label(mrow, text="Image mode", width=14, anchor="w").pack(side="left")
        mode_var = tk.StringVar(value=source.get("image_mode", "contain"))
        ttk.Combobox(mrow, textvariable=mode_var, values=["contain", "cover", "original", "stretch"], state="readonly").pack(side="left", fill="x", expand=True)

        sx_var = tk.IntVar(value=int(source.get("scale_x", 100)))
        sy_var = tk.IntVar(value=int(source.get("scale_y", 100)))

        sx = tk.Frame(left)
        sx.pack(fill="x", pady=4)
        tk.Label(sx, text="Scale X", width=14, anchor="w").pack(side="left")
        tk.Scale(sx, from_=25, to=240, orient="horizontal", variable=sx_var, length=320).pack(side="left", fill="x", expand=True)

        sy = tk.Frame(left)
        sy.pack(fill="x", pady=4)
        tk.Label(sy, text="Scale Y", width=14, anchor="w").pack(side="left")
        tk.Scale(sy, from_=25, to=240, orient="horizontal", variable=sy_var, length=320).pack(side="left", fill="x", expand=True)

        tk.Label(left, text="collection type opens folder in this app; shortcut launches file/app.", font=("Segoe UI", 9)).pack(fill="x", pady=(8, 0))

        preview_canvas = tk.Canvas(right, width=340, height=180, highlightthickness=0)
        preview_canvas.pack(padx=10, pady=10)
        preview_ref = {"img": None}

        def rerender_preview(*_args):
            section_preview = {
                "image": image_var.get().strip(),
                "shape": shape_var.get().strip(),
                "image_mode": mode_var.get().strip(),
                "scale_x": sx_var.get(),
                "scale_y": sy_var.get(),
            }
            img = self.build_preview(section_preview, size=(320, 160))
            preview_canvas.delete("all")
            if img:
                preview_ref["img"] = img
                preview_canvas.create_image(10, 10, anchor="nw", image=img)
            else:
                preview_canvas.create_text(170, 90, text="Preview", fill=self.theme.get("text", "white"))

        for v in (image_var, shape_var, mode_var):
            v.trace_add("write", rerender_preview)
        sx_var.trace_add("write", rerender_preview)
        sy_var.trace_add("write", rerender_preview)
        rerender_preview()

        def save():
            payload = {
                "name": name_var.get().strip() or "Unnamed",
                "type": type_var.get().strip() or "shortcut",
                "path": path_var.get().strip(),
                "image": image_var.get().strip(),
                "shape": shape_var.get().strip(),
                "image_mode": mode_var.get().strip(),
                "scale_x": int(sx_var.get()),
                "scale_y": int(sy_var.get()),
            }
            if not payload["path"]:
                messagebox.showwarning(APP_NAME, "Please choose a target file/folder.")
                return
            if editing:
                self.config_data["sections"][index] = payload
            else:
                self.config_data["sections"].append(payload)
            self.save_config()
            self.render_sections()
            win.destroy()

        footer = tk.Frame(left)
        footer.pack(fill="x", pady=(12, 0))
        ttk.Button(footer, text="Save", command=save).pack(side="right")

    def _pick_target(self, var):
        f = filedialog.askopenfilename(title="Choose target file/executable")
        if f:
            var.set(f)
            return
        d = filedialog.askdirectory(title="Choose target folder")
        if d:
            var.set(d)

    def _pick_image(self, var):
        p = filedialog.askopenfilename(title="Choose image", filetypes=[("Images", "*.png *.jpg *.jpeg *.webp *.gif")])
        if p:
            var.set(p)

    def delete_section(self, index):
        del self.config_data["sections"][index]
        self.save_config()
        self.render_sections()

    def map_file_image(self):
        map_type = simpledialog.askstring(APP_NAME, "Map by 'ext' (e.g. .exe) or 'file'?")
        if not map_type:
            return

        map_type = map_type.strip().lower()
        if map_type == "ext":
            key = simpledialog.askstring(APP_NAME, "Extension (example: .exe)")
            if not key:
                return
            key = key.strip().lower()
        elif map_type == "file":
            key = filedialog.askopenfilename(title="Pick file to map image")
            if not key:
                return
        else:
            messagebox.showwarning(APP_NAME, "Please use 'ext' or 'file'.")
            return

        image = filedialog.askopenfilename(title="Pick image", filetypes=[("Images", "*.png *.jpg *.jpeg *.webp *.gif")])
        if not image:
            return

        self.config_data.setdefault("file_images", {})[key] = image
        self.file_icon_cache.clear()
        self.save_config()
        self.populate_file_view()
        messagebox.showinfo(APP_NAME, "Mapping saved.")

    def open_theme_studio(self):
        win = tk.Toplevel(self.root)
        win.title("Theme Studio")
        win.geometry("820x620")
        win.transient(self.root)

        themes = self.config_data["themes"]
        active = tk.StringVar(value=self.config_data.get("theme", "arc-neon"))

        top = tk.Frame(win)
        top.pack(fill="x", padx=12, pady=12)
        tk.Label(top, text="Theme", width=11, anchor="w").pack(side="left")
        picker = ttk.Combobox(top, textvariable=active, values=sorted(themes.keys()), state="readonly")
        picker.pack(side="left", fill="x", expand=True)

        status = tk.Label(top, text=f"Current: {self.config_data.get('theme', '')}")
        status.pack(side="right")

        def apply_selected_theme():
            self.config_data["theme"] = active.get()
            self.save_config()
            self.refresh()
            status.configure(text=f"Current: {active.get()}")

        ttk.Button(top, text="Apply", command=apply_selected_theme).pack(side="right", padx=(0, 6))

        # Preset themes
        preset_row = tk.Frame(win)
        preset_row.pack(fill="x", padx=12, pady=(0, 8))
        tk.Label(preset_row, text="Preset theme", width=11, anchor="w").pack(side="left")
        preset_var = tk.StringVar(value=next(iter(self.config_data.get("preset_themes", {"Starter Neon": {}}).keys())))
        preset_combo = ttk.Combobox(preset_row, textvariable=preset_var, values=sorted(self.config_data.get("preset_themes", {}).keys()), state="readonly")
        preset_combo.pack(side="left", fill="x", expand=True)

        def apply_preset():
            preset = self.config_data.get("preset_themes", {}).get(preset_var.get())
            if not preset:
                return
            tname = preset.get("theme_name", "arc-neon")
            self.config_data["themes"][tname] = {**DEFAULT_CONFIG["themes"]["arc-neon"], **preset.get("theme", {})}
            self.config_data["theme"] = tname
            self.config_data["sections"] = json.loads(json.dumps(preset.get("sections", [])))
            self.config_data["file_images"] = json.loads(json.dumps(preset.get("file_images", {})))
            self.file_icon_cache.clear()
            self.save_config()
            self.refresh()
            active.set(tname)
            status.configure(text=f"Current: {tname}")
            messagebox.showinfo(APP_NAME, f"Applied preset: {preset_var.get()}")

        ttk.Button(preset_row, text="Apply Preset", command=apply_preset).pack(side="left", padx=6)

        editor = tk.Frame(win)
        editor.pack(fill="both", expand=True, padx=12)
        fields = {}

        def color_row(label, key):
            r = tk.Frame(editor)
            r.pack(fill="x", pady=4)
            tk.Label(r, text=label, width=14, anchor="w").pack(side="left")
            var = tk.StringVar()
            ttk.Entry(r, textvariable=var).pack(side="left", fill="x", expand=True, padx=(0, 6))
            preview = tk.Label(r, text="    ", width=4)
            preview.pack(side="left", padx=(0, 6))

            def pick():
                chosen = colorchooser.askcolor(var.get() or "#ffffff", parent=win)
                if chosen and chosen[1]:
                    var.set(chosen[1])
                    preview.configure(bg=chosen[1])

            ttk.Button(r, text="🎨", width=3, command=pick).pack(side="left")
            fields[key] = (var, preview)

        for k in ["bg", "panel", "card", "text", "accent", "secondary"]:
            color_row(k.capitalize(), k)

        bg_row = tk.Frame(editor)
        bg_row.pack(fill="x", pady=(10, 4))
        tk.Label(bg_row, text="Background image", width=14, anchor="w").pack(side="left")
        bg_var = tk.StringVar()
        ttk.Entry(bg_row, textvariable=bg_var).pack(side="left", fill="x", expand=True, padx=(0, 6))
        ttk.Button(bg_row, text="Browse", command=lambda: self._pick_image(bg_var)).pack(side="left")

        def load_theme_values(*_args):
            data = themes.get(active.get(), DEFAULT_CONFIG["themes"]["arc-neon"])
            for k in ["bg", "panel", "card", "text", "accent", "secondary"]:
                fields[k][0].set(data.get(k, ""))
                fields[k][1].configure(bg=data.get(k, "#ffffff"))
            bg_var.set(data.get("background_image", ""))

        picker.bind("<<ComboboxSelected>>", load_theme_values)
        load_theme_values()

        bottom = tk.Frame(win)
        bottom.pack(fill="x", padx=12, pady=(0, 12))

        def save_theme():
            data = themes.setdefault(active.get(), json.loads(json.dumps(DEFAULT_CONFIG["themes"]["arc-neon"])))
            for k in ["bg", "panel", "card", "text", "accent", "secondary"]:
                data[k] = fields[k][0].get().strip() or DEFAULT_CONFIG["themes"]["arc-neon"][k]
            data["background_image"] = bg_var.get().strip()
            data.setdefault("stickers", [])
            self.config_data["theme"] = active.get()
            self.save_config()
            self.refresh()
            status.configure(text=f"Current: {active.get()}")
            messagebox.showinfo(APP_NAME, "Theme saved and applied.")

        ttk.Button(bottom, text="Save Theme", command=save_theme).pack(side="right")

    def open_external(self, path):
        if not path:
            return
        try:
            if os.name == "nt":
                os.startfile(path)
            else:
                subprocess.Popen(["xdg-open", path])
        except Exception as exc:
            messagebox.showerror(APP_NAME, f"Could not open:\n{exc}")

    def open_section(self, section):
        section_type = section.get("type", "shortcut")
        target = section.get("path", "")
        if section_type == "collection":
            if os.path.isdir(target):
                self.left_layout_var.set("Tree + Files")
                self.apply_left_layout()
                self.set_current_dir(target)
                self.main.sash_place(0, 520, 0)
            else:
                messagebox.showwarning(APP_NAME, "Collection sections must point to a folder.")
        else:
            self.open_external(target)

    def render_sections(self):
        for child in self.cards_container.winfo_children():
            child.destroy()
        self.section_images.clear()

        zoom = max(70, min(170, int(self.zoom_var.get()))) / 100
        card_w = int(340 * zoom)
        card_h = int(278 * zoom)
        img_w = int(304 * zoom)
        img_h = int(152 * zoom)

        sections = self.get_sorted_sections()
        cols = 4 if zoom <= 1 else 3
        for display_idx, (idx, section) in enumerate(sections):
            row, col = divmod(display_idx, cols)
            card = tk.Frame(self.cards_container, width=card_w, height=card_h, bd=0, relief="flat")
            card.grid(row=row, column=col, padx=12, pady=12, sticky="nsew")
            card.grid_propagate(False)

            img = self.build_preview(section, size=(img_w, img_h))
            if img:
                lbl = tk.Label(card, image=img)
                lbl.image = img
                self.section_images.append(img)
            else:
                lbl = tk.Label(card, text="No image", font=("Segoe UI", max(9, int(10 * zoom))))
            lbl.pack(pady=(12, 8))

            tk.Label(card, text=section.get("name", "Unnamed"), font=("Segoe UI Semibold", max(10, int(13 * zoom)))).pack()
            tk.Label(card, text=f"[{section.get('type', 'shortcut')}]", font=("Segoe UI", 9)).pack()
            tk.Label(card, text=section.get("path", ""), font=("Segoe UI", max(8, int(8 * zoom))), wraplength=img_w).pack(pady=(2, 8))

            row_btn = tk.Frame(card)
            row_btn.pack(pady=4)
            ttk.Button(row_btn, text="Open", command=lambda s=section: self.open_section(s)).pack(side="left", padx=4)
            ttk.Button(row_btn, text="Edit", command=lambda i=idx: self.edit_section(i)).pack(side="left", padx=4)

            card.bind("<Button-3>", lambda e, i=idx: self.section_context_menu(e, i))
            for child in card.winfo_children():
                child.bind("<Button-3>", lambda e, i=idx: self.section_context_menu(e, i))

            self._paint_card(card)

    def get_sorted_sections(self):
        sections = list(enumerate(self.config_data.get("sections", [])))
        mode = self.sort_var.get().strip().lower()
        if mode == "alphabetical (z-a)":
            sections.sort(key=lambda x: x[1].get("name", "").lower(), reverse=True)
        elif mode == "size (largest)":
            sections.sort(key=lambda x: self._section_file_stats(x[1].get("path", ""))[0], reverse=True)
        elif mode == "size (smallest)":
            sections.sort(key=lambda x: self._section_file_stats(x[1].get("path", ""))[0])
        elif mode == "date (newest)":
            sections.sort(key=lambda x: self._section_file_stats(x[1].get("path", ""))[1], reverse=True)
        elif mode == "date (oldest)":
            sections.sort(key=lambda x: self._section_file_stats(x[1].get("path", ""))[1])
        else:
            sections.sort(key=lambda x: x[1].get("name", "").lower())
        return sections

    def _section_file_stats(self, path):
        if not path or not os.path.exists(path):
            return 0, 0
        try:
            stat = os.stat(path)
            return int(stat.st_size), float(stat.st_mtime)
        except OSError:
            return 0, 0

    def _paint_card(self, card):
        t = self.theme
        card.configure(bg=t["card"], highlightthickness=1, highlightbackground=t["secondary"])
        for w in card.winfo_children():
            if isinstance(w, tk.Label):
                w.configure(bg=t["card"], fg=t["text"])
            elif isinstance(w, tk.Frame):
                w.configure(bg=t["card"])

    def section_context_menu(self, event, index):
        menu = tk.Menu(self.root, tearoff=0)
        menu.add_command(label="Edit Section", command=lambda: self.edit_section(index))
        menu.add_command(label="Change Image", command=lambda: self.change_section_image(index))
        menu.add_command(label="Delete Section", command=lambda: self.delete_section(index))
        menu.tk_popup(event.x_root, event.y_root)

    def change_section_image(self, index):
        path = filedialog.askopenfilename(title="Choose section image", filetypes=[("Images", "*.png *.jpg *.jpeg *.webp *.gif")])
        if not path:
            return
        self.config_data["sections"][index]["image"] = path
        self.save_config()
        self.render_sections()

    def build_preview(self, section, size=(304, 152)):
        image_path = section.get("image", "")
        if not image_path or not os.path.exists(image_path):
            return None

        image = Image.open(image_path)
        base = next(ImageSequence.Iterator(image)).convert("RGBA")

        mode = section.get("image_mode", "contain")
        sx = max(25, int(section.get("scale_x", 100))) / 100.0
        sy = max(25, int(section.get("scale_y", 100))) / 100.0
        tw, th = size

        if mode == "stretch":
            nw, nh = max(1, int(tw * sx)), max(1, int(th * sy))
        elif mode == "original":
            nw, nh = max(1, int(base.width * sx)), max(1, int(base.height * sy))
        else:
            fit = min(tw / base.width, th / base.height) if mode == "contain" else max(tw / base.width, th / base.height)
            ratio = fit * ((sx + sy) / 2.0)
            nw, nh = max(1, int(base.width * ratio)), max(1, int(base.height * ratio))

        scaled = base.resize((nw, nh), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", size, (0, 0, 0, 0))
        canvas.alpha_composite(scaled, dest=((tw - nw) // 2, (th - nh) // 2))

        mask = Image.new("L", size, 0)
        draw = ImageDraw.Draw(mask)
        shape = section.get("shape", "rounded")
        if shape == "circle":
            draw.ellipse((0, 0, tw, th), fill=255)
        elif shape == "hexagon":
            pts = [(tw * 0.2, 0), (tw * 0.8, 0), (tw, th * 0.5), (tw * 0.8, th), (tw * 0.2, th), (0, th * 0.5)]
            draw.polygon(pts, fill=255)
        elif shape == "rectangle":
            draw.rectangle((0, 0, tw, th), fill=255)
        else:
            draw.rounded_rectangle((0, 0, tw, th), radius=28, fill=255)

        canvas.putalpha(mask)
        return ImageTk.PhotoImage(canvas)


if __name__ == "__main__":
    root = tk.Tk()
    LauncherExplorerApp(root)
    root.mainloop()
