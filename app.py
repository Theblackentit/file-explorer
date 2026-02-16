import json
import os
import subprocess
from pathlib import Path
import tkinter as tk
from tkinter import colorchooser, filedialog, messagebox, simpledialog, ttk

from PIL import Image, ImageDraw, ImageSequence, ImageTk

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
    "sections": [],
    "file_images": {},
}


class LauncherExplorerApp:
    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title(APP_NAME)
        self.root.geometry("1460x900")
        self.root.minsize(1200, 760)

        self.config_data = self.load_config()
        self.theme = self.current_theme()

        self.theme_background_tk = None
        self.theme_background_src = None
        self.sticker_items = []
        self.sticker_after_jobs = []
        self.section_images = []

        self.setup_style()
        self.build_ui()
        self.apply_theme()
        self.populate_roots()
        self.render_sections()

    def load_config(self):
        if not CONFIG_FILE.exists():
            return json.loads(json.dumps(DEFAULT_CONFIG))

        with CONFIG_FILE.open("r", encoding="utf-8") as f:
            loaded = json.load(f)

        merged = json.loads(json.dumps(DEFAULT_CONFIG))
        merged.update(loaded)
        merged["themes"] = {**DEFAULT_CONFIG["themes"], **loaded.get("themes", {})}

        for section in merged.get("sections", []):
            section.setdefault("shape", "rounded")
            section.setdefault("image_mode", "contain")
            section.setdefault("scale_x", 100)
            section.setdefault("scale_y", 100)
        return merged

    def save_config(self):
        with CONFIG_FILE.open("w", encoding="utf-8") as f:
            json.dump(self.config_data, f, indent=2)

    def current_theme(self):
        name = self.config_data.get("theme", "arc-neon")
        return self.config_data["themes"].get(name, DEFAULT_CONFIG["themes"]["arc-neon"])

    def setup_style(self):
        self.style = ttk.Style()
        if "clam" in self.style.theme_names():
            self.style.theme_use("clam")

    def build_ui(self):
        top_shell = tk.Frame(self.root, height=86)
        top_shell.pack(fill="x", padx=12, pady=(10, 6))

        title = tk.Label(top_shell, text=APP_NAME, font=("Segoe UI Semibold", 22))
        title.pack(side="left", padx=(10, 20), pady=(8, 0))

        sub = tk.Label(top_shell, text="Anime launcher vibe • custom themes • rich cards", font=("Segoe UI", 10))
        sub.pack(side="left", pady=(12, 0))

        btns = tk.Frame(top_shell)
        btns.pack(side="right", padx=8, pady=8)

        actions = [
            ("+ Add Section", self.add_section),
            ("Map File Image", self.map_file_image),
            ("Theme Studio", self.open_theme_studio),
            ("Refresh", self.refresh),
        ]
        for text, fn in actions:
            ttk.Button(btns, text=text, command=fn).pack(side="left", padx=4)

        self.accent_bar = tk.Canvas(self.root, height=4, highlightthickness=0)
        self.accent_bar.pack(fill="x", padx=12, pady=(0, 8))

        self.main = tk.PanedWindow(self.root, orient="horizontal", sashwidth=8, bd=0)
        self.main.pack(fill="both", expand=True, padx=12, pady=(0, 10))

        left = tk.Frame(self.main, width=380)
        right = tk.Frame(self.main)
        self.main.add(left)
        self.main.add(right)

        self.tree = ttk.Treeview(left, columns=("fullpath",), show="tree")
        scroll = ttk.Scrollbar(left, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=scroll.set)
        self.tree.pack(side="left", fill="both", expand=True)
        scroll.pack(side="right", fill="y")

        self.tree.bind("<<TreeviewOpen>>", self.on_tree_open)
        self.tree.bind("<Double-1>", self.on_tree_double_click)

        right_shell = tk.Frame(right)
        right_shell.pack(fill="both", expand=True)

        self.canvas = tk.Canvas(right_shell, highlightthickness=0)
        self.scroll = ttk.Scrollbar(right_shell, orient="vertical", command=self.canvas.yview)
        self.canvas.configure(yscrollcommand=self.scroll.set)

        self.content = tk.Frame(self.canvas)
        self.content.bind("<Configure>", lambda _e: self.canvas.configure(scrollregion=self.canvas.bbox("all")))
        self.content_window = self.canvas.create_window((0, 0), window=self.content, anchor="nw")
        self.canvas.bind("<Configure>", self._on_canvas_resize)

        self.canvas.pack(side="left", fill="both", expand=True)
        self.scroll.pack(side="right", fill="y")

        self.zoom_var = tk.IntVar(value=100)
        self.sort_var = tk.StringVar(value="Alphabetical (A-Z)")

        self.cards_toolbar = tk.Frame(self.content)
        self.cards_toolbar.pack(fill="x", padx=24, pady=(18, 0))

        tk.Label(self.cards_toolbar, text="Sort:", font=("Segoe UI Semibold", 10)).pack(side="left")
        self.sort_combo = ttk.Combobox(
            self.cards_toolbar,
            textvariable=self.sort_var,
            values=[
                "Alphabetical (A-Z)",
                "Alphabetical (Z-A)",
                "Size (Largest)",
                "Size (Smallest)",
                "Date (Newest)",
                "Date (Oldest)",
            ],
            state="readonly",
            width=22,
        )
        self.sort_combo.pack(side="left", padx=(8, 16))
        self.sort_combo.bind("<<ComboboxSelected>>", lambda _e: self.render_sections())

        tk.Label(self.cards_toolbar, text="Zoom:", font=("Segoe UI Semibold", 10)).pack(side="left")
        self.zoom_scale = tk.Scale(
            self.cards_toolbar,
            from_=70,
            to=170,
            orient="horizontal",
            variable=self.zoom_var,
            length=180,
            showvalue=True,
            resolution=5,
            command=lambda _v: self.render_sections(),
        )
        self.zoom_scale.pack(side="left", padx=(8, 0))

        self.cards_container = tk.Frame(self.content)
        self.cards_container.pack(fill="both", expand=True, padx=24, pady=24)

    def apply_theme(self):
        self.theme = self.current_theme()
        t = self.theme

        self.root.configure(bg=t["bg"])
        self.main.configure(bg=t["bg"])
        self.accent_bar.configure(bg=t["secondary"])

        self.style.configure("TButton", padding=(12, 7), font=("Segoe UI Semibold", 10), foreground=t["text"])
        self.style.map("TButton", background=[("active", t["accent"])])
        self.style.configure("Treeview", font=("Segoe UI", 10), rowheight=28)
        self.style.configure("Treeview", background=t["panel"], fieldbackground=t["panel"], foreground=t["text"])
        self.style.map("Treeview", background=[("selected", t["accent"])], foreground=[("selected", "white")])

        self._recolor_recursive(self.root, t)
        self._render_background()
        self._render_stickers()

    def _recolor_recursive(self, widget, theme):
        cls = widget.winfo_class()
        if cls in {"Frame", "PanedWindow"}:
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
            img = Image.open(path).convert("RGBA")
            img = img.resize((width, height), Image.Resampling.LANCZOS)
            shade = Image.new("RGBA", img.size, (0, 0, 0, 105))
            img.alpha_composite(shade)
            self.theme_background_tk = ImageTk.PhotoImage(img)
            self.theme_background_src = (path, width, height)

        self.content.configure(bg=self.theme["panel"])
        if not hasattr(self, "background_label"):
            self.background_label = tk.Label(self.content, bd=0, highlightthickness=0)
            self.background_label.place(x=0, y=0, relwidth=1, relheight=1)

        self.background_label.configure(image=self.theme_background_tk)
        self.background_label.image = self.theme_background_tk
        self.background_label.place(x=0, y=0, width=width, height=height)
        self.background_label.lower()
        self.cards_toolbar.lift()
        self.cards_container.lift()

    def clear_stickers(self):
        for job in self.sticker_after_jobs:
            self.root.after_cancel(job)
        self.sticker_after_jobs.clear()

        for item in self.sticker_items:
            item["label"].destroy()
        self.sticker_items.clear()

    def _render_stickers(self):
        self.clear_stickers()
        for sticker in self.theme.get("stickers", []):
            path = sticker.get("path", "")
            if not path or not os.path.exists(path):
                continue

            label = tk.Label(self.content, bd=0, highlightthickness=0)
            label.place(x=int(sticker.get("x", 20)), y=int(sticker.get("y", 20)))
            size = max(24, int(sticker.get("size", 96)))

            if path.lower().endswith(".gif"):
                gif = Image.open(path)
                frames = [
                    ImageTk.PhotoImage(frame.convert("RGBA").resize((size, size), Image.Resampling.LANCZOS))
                    for frame in ImageSequence.Iterator(gif)
                ]
                if not frames:
                    label.destroy()
                    continue
                item = {"label": label, "frames": frames, "index": 0}
                self.sticker_items.append(item)
                self._animate_sticker(item)
            else:
                frame = Image.open(path).convert("RGBA").resize((size, size), Image.Resampling.LANCZOS)
                tk_img = ImageTk.PhotoImage(frame)
                label.configure(image=tk_img)
                label.image = tk_img
                self.sticker_items.append({"label": label, "frames": [tk_img], "index": 0})

    def _animate_sticker(self, sticker):
        frames = sticker["frames"]
        idx = sticker["index"] % len(frames)
        sticker["label"].configure(image=frames[idx])
        sticker["label"].image = frames[idx]
        sticker["index"] = (idx + 1) % len(frames)
        self.sticker_after_jobs.append(self.root.after(110, lambda s=sticker: self._animate_sticker(s)))

    def populate_roots(self):
        self.tree.delete(*self.tree.get_children())
        if os.name == "nt":
            drives = [f"{d}:\\" for d in "ABCDEFGHIJKLMNOPQRSTUVWXYZ" if os.path.exists(f"{d}:\\")]
            for drive in drives:
                node = self.tree.insert("", "end", text=drive, values=(drive,))
                self.tree.insert(node, "end", text="...")
        else:
            node = self.tree.insert("", "end", text="/", values=("/",))
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
        except PermissionError:
            return

    def on_tree_double_click(self, _event):
        node = self.tree.focus()
        self.open_path(self.tree.set(node, "fullpath"))

    def open_path(self, path):
        if not path:
            return
        if os.name == "nt":
            os.startfile(path)
        else:
            subprocess.Popen(["xdg-open", path])

    def refresh(self):
        self.apply_theme()
        self.populate_roots()
        self.render_sections()

    def add_section(self):
        self.section_editor_window()

    def edit_section(self, index):
        self.section_editor_window(index=index)

    def section_editor_window(self, index=None):
        editing = index is not None
        source = self.config_data["sections"][index] if editing else {
            "name": "",
            "path": "",
            "image": "",
            "shape": "rounded",
            "image_mode": "contain",
            "scale_x": 100,
            "scale_y": 100,
        }

        win = tk.Toplevel(self.root)
        win.title("Edit Section" if editing else "Create Section")
        win.geometry("720x520")
        win.transient(self.root)

        shell = tk.Frame(win)
        shell.pack(fill="both", expand=True, padx=14, pady=14)

        left = tk.Frame(shell)
        left.pack(side="left", fill="both", expand=True)

        right = tk.LabelFrame(shell, text="Live Preview")
        right.pack(side="right", fill="both", padx=(14, 0))

        def row(parent, label_text, initial, browse=None):
            r = tk.Frame(parent)
            r.pack(fill="x", pady=5)
            tk.Label(r, text=label_text, width=14, anchor="w").pack(side="left")
            var = tk.StringVar(value=initial)
            ttk.Entry(r, textvariable=var).pack(side="left", fill="x", expand=True, padx=(0, 6))
            if browse:
                ttk.Button(r, text="Browse", command=lambda: browse(var)).pack(side="left")
            return var

        name_var = row(left, "Name", source.get("name", ""))
        path_var = row(left, "Target", source.get("path", ""), browse=self._pick_target)
        image_var = row(left, "Image", source.get("image", ""), browse=self._pick_image)

        shape_row = tk.Frame(left)
        shape_row.pack(fill="x", pady=5)
        tk.Label(shape_row, text="Shape", width=14, anchor="w").pack(side="left")
        shape_var = tk.StringVar(value=source.get("shape", "rounded"))
        ttk.Combobox(shape_row, textvariable=shape_var, values=["rounded", "rectangle", "circle", "hexagon"], state="readonly").pack(side="left", fill="x", expand=True)

        mode_row = tk.Frame(left)
        mode_row.pack(fill="x", pady=5)
        tk.Label(mode_row, text="Image mode", width=14, anchor="w").pack(side="left")
        mode_var = tk.StringVar(value=source.get("image_mode", "contain"))
        ttk.Combobox(mode_row, textvariable=mode_var, values=["contain", "cover", "original", "stretch"], state="readonly").pack(side="left", fill="x", expand=True)

        sx_row = tk.Frame(left)
        sx_row.pack(fill="x", pady=4)
        tk.Label(sx_row, text="Scale X", width=14, anchor="w").pack(side="left")
        sx_var = tk.IntVar(value=int(source.get("scale_x", 100)))
        tk.Scale(sx_row, from_=25, to=240, variable=sx_var, orient="horizontal", showvalue=True, resolution=1, length=320).pack(side="left", fill="x", expand=True)

        sy_row = tk.Frame(left)
        sy_row.pack(fill="x", pady=4)
        tk.Label(sy_row, text="Scale Y", width=14, anchor="w").pack(side="left")
        sy_var = tk.IntVar(value=int(source.get("scale_y", 100)))
        tk.Scale(sy_row, from_=25, to=240, variable=sy_var, orient="horizontal", showvalue=True, resolution=1, length=320).pack(side="left", fill="x", expand=True)

        tip = tk.Label(left, text="Use 'original' + 100% to keep native image size (no forced compression).", font=("Segoe UI", 9))
        tip.pack(fill="x", pady=(8, 0))

        preview_canvas = tk.Canvas(right, width=340, height=180, highlightthickness=0)
        preview_canvas.pack(padx=10, pady=10)
        preview_photo = {"img": None}

        def render_preview(*_args):
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
                preview_photo["img"] = img
                preview_canvas.create_image(10, 10, anchor="nw", image=img)
            else:
                preview_canvas.create_text(170, 90, text="Preview appears here", fill=self.theme.get("text", "white"), font=("Segoe UI", 12))

        for v in (image_var, shape_var, mode_var):
            v.trace_add("write", render_preview)
        sx_var.trace_add("write", render_preview)
        sy_var.trace_add("write", render_preview)
        render_preview()

        def save():
            payload = {
                "name": name_var.get().strip() or "Unnamed",
                "path": path_var.get().strip(),
                "image": image_var.get().strip(),
                "shape": shape_var.get().strip(),
                "image_mode": mode_var.get().strip(),
                "scale_x": int(sx_var.get()),
                "scale_y": int(sy_var.get()),
            }
            if not payload["path"]:
                messagebox.showwarning(APP_NAME, "Please choose a file or folder target.")
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
        file_choice = filedialog.askopenfilename(title="Choose target file/executable")
        if file_choice:
            var.set(file_choice)
            return
        folder_choice = filedialog.askdirectory(title="Choose target folder")
        if folder_choice:
            var.set(folder_choice)

    def _pick_image(self, var):
        selected = filedialog.askopenfilename(title="Choose image", filetypes=[("Images", "*.png *.jpg *.jpeg *.webp *.gif")])
        if selected:
            var.set(selected)

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
            key = key.strip()
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

        self.config_data["file_images"][key] = image
        self.save_config()
        messagebox.showinfo(APP_NAME, "File image mapping saved.")

    def open_theme_studio(self):
        win = tk.Toplevel(self.root)
        win.title("Theme Studio")
        win.geometry("760x560")
        win.transient(self.root)

        themes = self.config_data["themes"]
        theme_names = sorted(themes.keys())
        active = tk.StringVar(value=self.config_data.get("theme", theme_names[0]))

        def apply_selected_theme():
            self.config_data["theme"] = active.get()
            self.save_config()
            self.refresh()
            try:
                theme_status.configure(text=f"Current: {active.get()}")
            except NameError:
                pass

        top = tk.Frame(win)
        top.pack(fill="x", padx=12, pady=12)
        tk.Label(top, text="Theme", width=11, anchor="w").pack(side="left")
        picker = ttk.Combobox(top, textvariable=active, values=theme_names, state="readonly")
        picker.pack(side="left", fill="x", expand=True)

        def add_theme():
            name = simpledialog.askstring(APP_NAME, "New theme name:", parent=win)
            if not name:
                return
            if name in themes:
                messagebox.showinfo(APP_NAME, "Theme already exists.")
                return
            themes[name] = json.loads(json.dumps(DEFAULT_CONFIG["themes"]["arc-neon"]))
            picker.configure(values=sorted(themes.keys()))
            active.set(name)
            load_theme_values()

        ttk.Button(top, text="New", command=add_theme).pack(side="left", padx=6)
        ttk.Button(top, text="Apply", command=apply_selected_theme).pack(side="left", padx=(0, 6))

        theme_status = tk.Label(top, text=f"Current: {self.config_data.get('theme', '')}", font=("Segoe UI", 9))
        theme_status.pack(side="right")

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

        color_row("Background", "bg")
        color_row("Panel", "panel")
        color_row("Card", "card")
        color_row("Text", "text")
        color_row("Accent", "accent")
        color_row("Secondary", "secondary")

        bgf = tk.Frame(editor)
        bgf.pack(fill="x", pady=(10, 4))
        tk.Label(bgf, text="Background image", width=14, anchor="w").pack(side="left")
        bg_var = tk.StringVar()
        ttk.Entry(bgf, textvariable=bg_var).pack(side="left", fill="x", expand=True, padx=(0, 6))
        ttk.Button(bgf, text="Browse", command=lambda: self._pick_image(bg_var)).pack(side="left")

        sticker_block = tk.LabelFrame(editor, text="Stickers / small GIFs")
        sticker_block.pack(fill="both", expand=True, pady=(10, 6))
        sticker_list = tk.Listbox(sticker_block)
        sticker_list.pack(fill="both", expand=True, padx=8, pady=8)

        controls = tk.Frame(sticker_block)
        controls.pack(fill="x", padx=8, pady=(0, 8))

        def add_sticker():
            path = filedialog.askopenfilename(title="Choose sticker image/GIF", filetypes=[("Images", "*.png *.jpg *.jpeg *.webp *.gif")])
            if not path:
                return
            stickers = themes[active.get()].setdefault("stickers", [])
            stickers.append({"path": path, "x": 20 + len(stickers) * 24, "y": 20 + len(stickers) * 24, "size": 96})
            load_theme_values()

        def remove_sticker():
            sel = sticker_list.curselection()
            if not sel:
                return
            del themes[active.get()].setdefault("stickers", [])[sel[0]]
            load_theme_values()

        ttk.Button(controls, text="Add Sticker/GIF", command=add_sticker).pack(side="left")
        ttk.Button(controls, text="Remove", command=remove_sticker).pack(side="left", padx=6)

        def load_theme_values(*_args):
            data = themes[active.get()]
            for key in ("bg", "panel", "card", "text", "accent", "secondary"):
                fields[key][0].set(data.get(key, ""))
                fields[key][1].configure(bg=data.get(key, "#ffffff"))
            bg_var.set(data.get("background_image", ""))

            sticker_list.delete(0, "end")
            for s in data.get("stickers", []):
                sticker_list.insert("end", f"{Path(s['path']).name} (x={s.get('x', 0)}, y={s.get('y', 0)}, size={s.get('size', 96)})")

        picker.bind("<<ComboboxSelected>>", load_theme_values)
        load_theme_values()

        bottom = tk.Frame(win)
        bottom.pack(fill="x", padx=12, pady=(0, 12))

        def save_theme():
            current = themes[active.get()]
            for key in ("bg", "panel", "card", "text", "accent", "secondary"):
                current[key] = fields[key][0].get().strip() or DEFAULT_CONFIG["themes"]["arc-neon"][key]
            current["background_image"] = bg_var.get().strip()
            current.setdefault("stickers", [])
            self.config_data["theme"] = active.get()
            self.save_config()
            self.refresh()
            theme_status.configure(text=f"Current: {active.get()}")
            messagebox.showinfo(APP_NAME, "Theme saved and applied.")

        ttk.Button(bottom, text="Save Theme", command=save_theme).pack(side="right")
        ttk.Button(bottom, text="Apply Selected", command=apply_selected_theme).pack(side="right", padx=(0, 8))

    def render_sections(self):
        for child in self.cards_container.winfo_children():
            child.destroy()
        self.section_images.clear()

        zoom = max(70, min(170, int(self.zoom_var.get()))) / 100
        card_w = int(320 * zoom)
        card_h = int(260 * zoom)
        img_w = int(286 * zoom)
        img_h = int(142 * zoom)
        name_font = max(10, int(13 * zoom))
        path_font = max(8, int(8 * zoom))

        sorted_sections = self.get_sorted_sections()
        cols = 4 if zoom <= 1 else 3
        for display_idx, (idx, section) in enumerate(sorted_sections):
            row, col = divmod(display_idx, cols)
            card = tk.Frame(self.cards_container, width=card_w, height=card_h, bd=0, relief="flat")
            card.grid(row=row, column=col, padx=12, pady=12, sticky="nsew")
            card.grid_propagate(False)

            img = self.build_preview(section, size=(img_w, img_h))
            if img:
                image_label = tk.Label(card, image=img)
                image_label.image = img
                self.section_images.append(img)
            else:
                image_label = tk.Label(card, text="No image", font=("Segoe UI", max(9, int(10 * zoom))))
            image_label.pack(pady=(12, 8))

            tk.Label(card, text=section.get("name", "Unnamed"), font=("Segoe UI Semibold", name_font)).pack()
            tk.Label(card, text=section.get("path", ""), font=("Segoe UI", path_font), wraplength=img_w).pack(pady=(2, 8))

            row_btns = tk.Frame(card)
            row_btns.pack(pady=4)
            ttk.Button(row_btns, text="Open", command=lambda p=section.get("path", ""): self.open_path(p)).pack(side="left", padx=4)
            ttk.Button(row_btns, text="Edit", command=lambda i=idx: self.edit_section(i)).pack(side="left", padx=4)

            card.bind("<Button-3>", lambda e, i=idx: self.section_context_menu(e, i))
            for child in card.winfo_children():
                child.bind("<Button-3>", lambda e, i=idx: self.section_context_menu(e, i))

            self._paint_card(card)

    def _section_file_stats(self, path):
        if not path or not os.path.exists(path):
            return 0, 0
        try:
            stat = os.stat(path)
            return int(stat.st_size), float(stat.st_mtime)
        except OSError:
            return 0, 0

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

    def build_preview(self, section, size=(286, 142)):
        image_path = section.get("image", "")
        if not image_path or not os.path.exists(image_path):
            return None

        image = Image.open(image_path)
        base = next(ImageSequence.Iterator(image)).convert("RGBA")

        mode = section.get("image_mode", "contain")
        sx = max(25, int(section.get("scale_x", 100))) / 100.0
        sy = max(25, int(section.get("scale_y", 100))) / 100.0
        target_w, target_h = size

        if mode == "stretch":
            new_w = max(1, int(target_w * sx))
            new_h = max(1, int(target_h * sy))
        elif mode == "original":
            new_w = max(1, int(base.width * sx))
            new_h = max(1, int(base.height * sy))
        else:
            fit_ratio = min(target_w / base.width, target_h / base.height) if mode == "contain" else max(target_w / base.width, target_h / base.height)
            avg_scale = (sx + sy) / 2.0
            ratio = fit_ratio * avg_scale
            new_w = max(1, int(base.width * ratio))
            new_h = max(1, int(base.height * ratio))

        scaled = base.resize((new_w, new_h), Image.Resampling.LANCZOS)

        canvas = Image.new("RGBA", size, (0, 0, 0, 0))
        x = (target_w - new_w) // 2
        y = (target_h - new_h) // 2
        canvas.alpha_composite(scaled, dest=(x, y))

        mask = Image.new("L", size, 0)
        draw = ImageDraw.Draw(mask)
        shape = section.get("shape", "rounded")
        if shape == "circle":
            draw.ellipse((0, 0, target_w, target_h), fill=255)
        elif shape == "hexagon":
            pts = [
                (target_w * 0.2, 0),
                (target_w * 0.8, 0),
                (target_w, target_h * 0.5),
                (target_w * 0.8, target_h),
                (target_w * 0.2, target_h),
                (0, target_h * 0.5),
            ]
            draw.polygon(pts, fill=255)
        elif shape == "rectangle":
            draw.rectangle((0, 0, target_w, target_h), fill=255)
        else:
            draw.rounded_rectangle((0, 0, target_w, target_h), radius=28, fill=255)

        canvas.putalpha(mask)
        return ImageTk.PhotoImage(canvas)


if __name__ == "__main__":
    root = tk.Tk()
    LauncherExplorerApp(root)
    root.mainloop()
