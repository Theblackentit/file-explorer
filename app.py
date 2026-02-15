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
    "theme": "neon-night",
    "themes": {
        "neon-night": {
            "bg": "#121421",
            "panel": "#1b1f33",
            "card": "#232946",
            "text": "#e9edff",
            "accent": "#7c9cff",
            "background_image": "",
            "stickers": [],
        },
        "soft-light": {
            "bg": "#eef1f8",
            "panel": "#dfe4f1",
            "card": "#ffffff",
            "text": "#1f263f",
            "accent": "#4f7cff",
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
        self.root.geometry("1400x860")
        self.root.minsize(1100, 720)

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
        return merged

    def save_config(self):
        with CONFIG_FILE.open("w", encoding="utf-8") as f:
            json.dump(self.config_data, f, indent=2)

    def current_theme(self):
        name = self.config_data.get("theme", "neon-night")
        return self.config_data["themes"].get(name, DEFAULT_CONFIG["themes"]["neon-night"])

    def setup_style(self):
        self.style = ttk.Style()
        if "clam" in self.style.theme_names():
            self.style.theme_use("clam")

    def build_ui(self):
        top = tk.Frame(self.root, height=60)
        top.pack(fill="x", padx=10, pady=(10, 4))

        title = tk.Label(top, text=APP_NAME, font=("Segoe UI Semibold", 18))
        title.pack(side="left", padx=(8, 16))

        actions = [
            ("+ Add Section", self.add_section),
            ("Map File Image", self.map_file_image),
            ("Theme Studio", self.open_theme_studio),
            ("Refresh", self.refresh),
        ]
        for label, callback in actions:
            ttk.Button(top, text=label, command=callback).pack(side="left", padx=4)

        self.main = tk.PanedWindow(self.root, orient="horizontal", sashwidth=8, bd=0)
        self.main.pack(fill="both", expand=True, padx=10, pady=(0, 10))

        left = tk.Frame(self.main, width=340)
        right = tk.Frame(self.main)
        self.main.add(left)
        self.main.add(right)

        self.tree = ttk.Treeview(left, columns=("fullpath",), show="tree")
        tree_scroll = ttk.Scrollbar(left, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=tree_scroll.set)
        self.tree.pack(side="left", fill="both", expand=True)
        tree_scroll.pack(side="right", fill="y")

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

        self.bg_item = None
        self.sticker_layer = tk.Frame(self.content)
        self.sticker_layer.place(x=0, y=0, relwidth=1, relheight=1)

        self.cards_container = tk.Frame(self.content)
        self.cards_container.pack(fill="both", expand=True, padx=20, pady=20)

    def apply_theme(self):
        self.theme = self.current_theme()
        t = self.theme

        self.root.configure(bg=t["bg"])
        self.main.configure(bg=t["bg"])

        self.style.configure("TButton", padding=(10, 6), font=("Segoe UI", 10))
        self.style.configure("Treeview", font=("Segoe UI", 10), rowheight=26)
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
        elif cls == "Button":
            widget.configure(bg=theme["card"], fg=theme["text"], activebackground=theme["accent"], activeforeground="white")

        for child in widget.winfo_children():
            self._recolor_recursive(child, theme)

    def _on_canvas_resize(self, event):
        self.canvas.itemconfig(self.content_window, width=event.width)
        self._render_background()

    def _render_background(self):
        path = self.theme.get("background_image", "")
        if not path or not os.path.exists(path):
            if self.bg_item is not None:
                self.canvas.delete(self.bg_item)
                self.bg_item = None
            self.theme_background_src = None
            self.theme_background_tk = None
            return

        width = max(self.canvas.winfo_width(), 800)
        height = max(self.canvas.winfo_height(), 500)

        if self.theme_background_src != (path, width, height):
            img = Image.open(path).convert("RGBA").resize((width, height), Image.Resampling.LANCZOS)
            overlay = Image.new("RGBA", img.size, (0, 0, 0, 110))
            img.alpha_composite(overlay)
            self.theme_background_tk = ImageTk.PhotoImage(img)
            self.theme_background_src = (path, width, height)

        if self.bg_item is None:
            self.bg_item = self.canvas.create_image(0, 0, image=self.theme_background_tk, anchor="nw")
            self.canvas.tag_lower(self.bg_item)
        else:
            self.canvas.itemconfig(self.bg_item, image=self.theme_background_tk)

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

            x = sticker.get("x", 24)
            y = sticker.get("y", 24)
            size = max(24, int(sticker.get("size", 96)))
            label = tk.Label(self.content, bd=0, highlightthickness=0)
            label.place(x=x, y=y)

            if path.lower().endswith(".gif"):
                gif = Image.open(path)
                frames = []
                for frame in ImageSequence.Iterator(gif):
                    frames.append(ImageTk.PhotoImage(frame.convert("RGBA").resize((size, size), Image.Resampling.LANCZOS)))
                if not frames:
                    label.destroy()
                    continue
                item = {"label": label, "frames": frames, "index": 0}
                self.sticker_items.append(item)
                self._animate_sticker(item)
            else:
                frame = Image.open(path).convert("RGBA").resize((size, size), Image.Resampling.LANCZOS)
                img = ImageTk.PhotoImage(frame)
                label.configure(image=img)
                label.image = img
                self.sticker_items.append({"label": label, "frames": [img], "index": 0})

    def _animate_sticker(self, sticker):
        frames = sticker["frames"]
        idx = sticker["index"] % len(frames)
        sticker["label"].configure(image=frames[idx])
        sticker["label"].image = frames[idx]
        sticker["index"] = (idx + 1) % len(frames)
        job = self.root.after(120, lambda s=sticker: self._animate_sticker(s))
        self.sticker_after_jobs.append(job)

    def populate_roots(self):
        self.tree.delete(*self.tree.get_children())

        if os.name == "nt":
            for drive in [f"{d}:\\" for d in "ABCDEFGHIJKLMNOPQRSTUVWXYZ" if os.path.exists(f"{d}:\\")]:
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
        path = self.tree.set(node, "fullpath")
        self.open_path(path)

    def open_path(self, path):
        if not path:
            return
        if os.name == "nt":
            os.startfile(path)
        elif os.name == "posix":
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
        source = self.config_data["sections"][index] if editing else {"name": "", "path": "", "image": "", "shape": "rounded"}

        win = tk.Toplevel(self.root)
        win.title("Edit Section" if editing else "Create Section")
        win.geometry("520x300")
        win.transient(self.root)

        form = tk.Frame(win)
        form.pack(fill="both", expand=True, padx=14, pady=14)

        def row(label_text, initial, browse=None):
            row_frame = tk.Frame(form)
            row_frame.pack(fill="x", pady=5)
            tk.Label(row_frame, text=label_text, width=14, anchor="w").pack(side="left")
            var = tk.StringVar(value=initial)
            entry = ttk.Entry(row_frame, textvariable=var)
            entry.pack(side="left", fill="x", expand=True, padx=(0, 6))
            if browse:
                ttk.Button(row_frame, text="Browse", command=lambda: browse(var)).pack(side="right")
            return var

        name_var = row("Name", source.get("name", ""))
        path_var = row("Target", source.get("path", ""), browse=self._pick_target)
        image_var = row("Image", source.get("image", ""), browse=self._pick_image)

        shape_row = tk.Frame(form)
        shape_row.pack(fill="x", pady=5)
        tk.Label(shape_row, text="Shape", width=14, anchor="w").pack(side="left")
        shape_var = tk.StringVar(value=source.get("shape", "rounded"))
        shape_box = ttk.Combobox(shape_row, textvariable=shape_var, values=["rounded", "rectangle", "circle", "hexagon"], state="readonly")
        shape_box.pack(side="left", fill="x", expand=True)

        def save():
            payload = {
                "name": name_var.get().strip() or "Unnamed",
                "path": path_var.get().strip(),
                "image": image_var.get().strip(),
                "shape": shape_var.get().strip(),
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

        btns = tk.Frame(form)
        btns.pack(fill="x", pady=(10, 0))
        ttk.Button(btns, text="Save", command=save).pack(side="right")

    def _pick_target(self, var):
        file_choice = filedialog.askopenfilename(title="Choose target file/executable")
        if file_choice:
            var.set(file_choice)
            return
        folder_choice = filedialog.askdirectory(title="Choose target folder")
        if folder_choice:
            var.set(folder_choice)

    def _pick_image(self, var):
        selected = filedialog.askopenfilename(
            title="Choose image",
            filetypes=[("Images", "*.png *.jpg *.jpeg *.webp *.gif")],
        )
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
        win.geometry("700x520")
        win.transient(self.root)

        themes = self.config_data["themes"]
        theme_names = sorted(themes.keys())

        active = tk.StringVar(value=self.config_data.get("theme", theme_names[0]))

        top = tk.Frame(win)
        top.pack(fill="x", padx=12, pady=12)
        tk.Label(top, text="Theme", width=10, anchor="w").pack(side="left")
        picker = ttk.Combobox(top, textvariable=active, values=theme_names, state="readonly")
        picker.pack(side="left", fill="x", expand=True)

        def add_theme():
            name = simpledialog.askstring(APP_NAME, "New theme name:", parent=win)
            if not name:
                return
            if name in themes:
                messagebox.showinfo(APP_NAME, "Theme already exists.")
                return
            themes[name] = json.loads(json.dumps(DEFAULT_CONFIG["themes"]["neon-night"]))
            picker.configure(values=sorted(themes.keys()))
            active.set(name)
            load_theme_values()

        ttk.Button(top, text="New", command=add_theme).pack(side="left", padx=6)

        fields = {}

        editor = tk.Frame(win)
        editor.pack(fill="both", expand=True, padx=12)

        def color_row(label, key):
            r = tk.Frame(editor)
            r.pack(fill="x", pady=5)
            tk.Label(r, text=label, width=14, anchor="w").pack(side="left")
            var = tk.StringVar()
            entry = ttk.Entry(r, textvariable=var)
            entry.pack(side="left", fill="x", expand=True, padx=(0, 6))

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

        bg_frame = tk.Frame(editor)
        bg_frame.pack(fill="x", pady=(12, 5))
        tk.Label(bg_frame, text="Background image", width=14, anchor="w").pack(side="left")
        bg_var = tk.StringVar()
        ttk.Entry(bg_frame, textvariable=bg_var).pack(side="left", fill="x", expand=True, padx=(0, 6))
        ttk.Button(bg_frame, text="Browse", command=lambda: self._pick_image(bg_var)).pack(side="left")

        sticker_block = tk.LabelFrame(editor, text="Stickers / small GIFs")
        sticker_block.pack(fill="both", expand=True, pady=(12, 8))

        sticker_list = tk.Listbox(sticker_block)
        sticker_list.pack(fill="both", expand=True, padx=8, pady=8)

        controls = tk.Frame(sticker_block)
        controls.pack(fill="x", padx=8, pady=(0, 8))

        def add_sticker():
            path = filedialog.askopenfilename(
                title="Choose sticker image/GIF",
                filetypes=[("Images", "*.png *.jpg *.jpeg *.webp *.gif")],
            )
            if not path:
                return
            stickers = themes[active.get()].setdefault("stickers", [])
            stickers.append({"path": path, "x": 20 + len(stickers) * 24, "y": 20 + len(stickers) * 24, "size": 96})
            load_theme_values()

        def remove_sticker():
            sel = sticker_list.curselection()
            if not sel:
                return
            idx = sel[0]
            del themes[active.get()].setdefault("stickers", [])[idx]
            load_theme_values()

        ttk.Button(controls, text="Add Sticker/GIF", command=add_sticker).pack(side="left")
        ttk.Button(controls, text="Remove", command=remove_sticker).pack(side="left", padx=6)

        def load_theme_values(*_args):
            data = themes[active.get()]
            for key in ("bg", "panel", "card", "text", "accent"):
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
            for key in ("bg", "panel", "card", "text", "accent"):
                current[key] = fields[key][0].get().strip() or DEFAULT_CONFIG["themes"]["neon-night"][key]
            current["background_image"] = bg_var.get().strip()
            current.setdefault("stickers", [])

            self.config_data["theme"] = active.get()
            self.save_config()
            self.refresh()
            messagebox.showinfo(APP_NAME, "Theme saved.")

        ttk.Button(bottom, text="Save Theme", command=save_theme).pack(side="right")

    def render_sections(self):
        for child in self.cards_container.winfo_children():
            child.destroy()

        self.section_images.clear()

        sections = self.config_data.get("sections", [])
        cols = 4
        for idx, section in enumerate(sections):
            row = idx // cols
            col = idx % cols
            card = tk.Frame(self.cards_container, width=300, height=230, bd=0, relief="flat")
            card.grid(row=row, column=col, padx=12, pady=12, sticky="nsew")
            card.grid_propagate(False)

            img = self.build_preview(section.get("image", ""), section.get("shape", "rounded"), size=(268, 120))
            if img:
                image_label = tk.Label(card, image=img)
                image_label.image = img
                self.section_images.append(img)
            else:
                image_label = tk.Label(card, text="No image", font=("Segoe UI", 10))
            image_label.pack(pady=(10, 8))

            tk.Label(card, text=section.get("name", "Unnamed"), font=("Segoe UI Semibold", 12)).pack()
            tk.Label(card, text=section.get("path", ""), font=("Segoe UI", 8), wraplength=260).pack(pady=(2, 6))

            actions = tk.Frame(card)
            actions.pack(pady=4)
            ttk.Button(actions, text="Open", command=lambda p=section.get("path", ""): self.open_path(p)).pack(side="left", padx=4)
            ttk.Button(actions, text="Edit", command=lambda i=idx: self.edit_section(i)).pack(side="left", padx=4)

            card.bind("<Button-3>", lambda e, i=idx: self.section_context_menu(e, i))
            for child in card.winfo_children():
                child.bind("<Button-3>", lambda e, i=idx: self.section_context_menu(e, i))

            self._recolor_recursive(card, self.theme)

    def section_context_menu(self, event, index):
        menu = tk.Menu(self.root, tearoff=0)
        menu.add_command(label="Edit Section", command=lambda: self.edit_section(index))
        menu.add_command(label="Change Image", command=lambda: self.change_section_image(index))
        menu.add_command(label="Delete Section", command=lambda: self.delete_section(index))
        menu.tk_popup(event.x_root, event.y_root)

    def change_section_image(self, index):
        path = filedialog.askopenfilename(
            title="Choose section image",
            filetypes=[("Images", "*.png *.jpg *.jpeg *.webp *.gif")],
        )
        if not path:
            return
        self.config_data["sections"][index]["image"] = path
        self.save_config()
        self.render_sections()

    def build_preview(self, image_path, shape, size=(268, 120)):
        if not image_path or not os.path.exists(image_path):
            return None

        image = Image.open(image_path)
        frame = next(ImageSequence.Iterator(image)).convert("RGBA")
        img = frame.resize(size, Image.Resampling.LANCZOS)

        mask = Image.new("L", size, 0)
        draw = ImageDraw.Draw(mask)
        w, h = size

        if shape == "circle":
            draw.ellipse((0, 0, w, h), fill=255)
        elif shape == "hexagon":
            pts = [(w * 0.2, 0), (w * 0.8, 0), (w, h * 0.5), (w * 0.8, h), (w * 0.2, h), (0, h * 0.5)]
            draw.polygon(pts, fill=255)
        elif shape == "rectangle":
            draw.rectangle((0, 0, w, h), fill=255)
        else:
            draw.rounded_rectangle((0, 0, w, h), radius=28, fill=255)

        img.putalpha(mask)
        return ImageTk.PhotoImage(img)


if __name__ == "__main__":
    root = tk.Tk()
    LauncherExplorerApp(root)
    root.mainloop()
