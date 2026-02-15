import json
import os
from pathlib import Path
import tkinter as tk
from tkinter import filedialog, messagebox, simpledialog, ttk

try:
    from PIL import Image, ImageDraw, ImageTk
except ImportError:
    Image = ImageDraw = ImageTk = None

APP_NAME = "Launcher File Explorer"
CONFIG_FILE = Path("launcher_config.json")

DEFAULT_CONFIG = {
    "theme": "dark",
    "themes": {
        "dark": {
            "bg": "#1e1e2f",
            "panel": "#282a3a",
            "card": "#33354a",
            "text": "#e7e7f0",
            "accent": "#6ea8fe",
        },
        "light": {
            "bg": "#f7f7fb",
            "panel": "#e8e8ef",
            "card": "#ffffff",
            "text": "#1a1a22",
            "accent": "#2f6fed",
        },
    },
    "sections": [],
    "file_images": {},
}


class LauncherExplorerApp:
    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title(APP_NAME)
        self.root.geometry("1280x800")

        self.config_data = self.load_config()
        self.theme_name = self.config_data.get("theme", "dark")
        self.theme = self.config_data["themes"].get(self.theme_name, DEFAULT_CONFIG["themes"]["dark"])

        self.tk_images = []

        self.setup_style()
        self.build_ui()
        self.apply_theme()
        self.populate_roots()
        self.render_sections()

    def load_config(self):
        if CONFIG_FILE.exists():
            try:
                with CONFIG_FILE.open("r", encoding="utf-8") as f:
                    loaded = json.load(f)
                merged = DEFAULT_CONFIG.copy()
                merged.update(loaded)
                if "themes" in loaded:
                    merged["themes"] = {**DEFAULT_CONFIG["themes"], **loaded["themes"]}
                return merged
            except Exception as exc:
                messagebox.showwarning(APP_NAME, f"Could not read config. Using defaults.\n{exc}")
        return DEFAULT_CONFIG.copy()

    def save_config(self):
        with CONFIG_FILE.open("w", encoding="utf-8") as f:
            json.dump(self.config_data, f, indent=2)

    def setup_style(self):
        self.style = ttk.Style()
        if "clam" in self.style.theme_names():
            self.style.theme_use("clam")

    def build_ui(self):
        self.root.configure(bg=self.theme["bg"])

        top_bar = tk.Frame(self.root, height=48)
        top_bar.pack(fill="x")

        tk.Label(top_bar, text=APP_NAME, font=("Segoe UI", 14, "bold")).pack(side="left", padx=10)

        tk.Button(top_bar, text="Add Section", command=self.add_section).pack(side="left", padx=5)
        tk.Button(top_bar, text="Map File Image", command=self.map_file_image).pack(side="left", padx=5)
        tk.Button(top_bar, text="Theme", command=self.manage_theme).pack(side="left", padx=5)
        tk.Button(top_bar, text="Refresh", command=self.refresh).pack(side="left", padx=5)

        main = tk.PanedWindow(self.root, orient="horizontal", sashwidth=6)
        main.pack(fill="both", expand=True)

        left = tk.Frame(main, width=350)
        right = tk.Frame(main)
        main.add(left)
        main.add(right)

        self.tree = ttk.Treeview(left, columns=("fullpath",), show="tree")
        ysb = ttk.Scrollbar(left, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=ysb.set)
        self.tree.pack(side="left", fill="both", expand=True)
        ysb.pack(side="right", fill="y")

        self.tree.bind("<<TreeviewOpen>>", self.on_tree_open)
        self.tree.bind("<Double-1>", self.on_tree_double_click)

        right_top = tk.Frame(right)
        right_top.pack(fill="both", expand=True)

        self.canvas = tk.Canvas(right_top, highlightthickness=0)
        self.scroll = ttk.Scrollbar(right_top, orient="vertical", command=self.canvas.yview)
        self.grid_frame = tk.Frame(self.canvas)
        self.grid_frame.bind(
            "<Configure>",
            lambda e: self.canvas.configure(scrollregion=self.canvas.bbox("all")),
        )
        self.canvas.create_window((0, 0), window=self.grid_frame, anchor="nw")
        self.canvas.configure(yscrollcommand=self.scroll.set)

        self.canvas.pack(side="left", fill="both", expand=True)
        self.scroll.pack(side="right", fill="y")

    def apply_theme(self):
        t = self.theme
        self.root.configure(bg=t["bg"])
        for widget in self.root.winfo_children():
            self.recursive_color(widget, t)

        self.style.configure("Treeview", background=t["panel"], foreground=t["text"], fieldbackground=t["panel"])
        self.style.map("Treeview", background=[("selected", t["accent"])])

    def recursive_color(self, widget, theme):
        cls = widget.winfo_class()
        if cls in {"Frame", "PanedWindow", "Canvas"}:
            widget.configure(bg=theme["bg"] if cls != "Canvas" else theme["panel"])
        elif cls == "Label":
            widget.configure(bg=theme["bg"], fg=theme["text"])
        elif cls == "Button":
            widget.configure(bg=theme["card"], fg=theme["text"], activebackground=theme["accent"], activeforeground="white")
        for child in widget.winfo_children():
            self.recursive_color(child, theme)

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

    def on_tree_open(self, event):
        node = self.tree.focus()
        path = self.tree.set(node, "fullpath")
        if not path:
            return
        children = self.tree.get_children(node)
        if children and self.tree.item(children[0], "text") != "...":
            return

        self.tree.delete(*children)
        try:
            for name in sorted(os.listdir(path), key=lambda s: s.lower()):
                full = os.path.join(path, name)
                child = self.tree.insert(node, "end", text=name, values=(full,))
                if os.path.isdir(full):
                    self.tree.insert(child, "end", text="...")
        except PermissionError:
            pass

    def on_tree_double_click(self, event):
        node = self.tree.focus()
        path = self.tree.set(node, "fullpath")
        if os.path.isfile(path):
            try:
                os.startfile(path) if os.name == "nt" else os.system(f'xdg-open "{path}"')
            except Exception as exc:
                messagebox.showerror(APP_NAME, f"Could not open file:\n{exc}")

    def refresh(self):
        self.theme = self.config_data["themes"].get(self.config_data.get("theme", "dark"), DEFAULT_CONFIG["themes"]["dark"])
        self.apply_theme()
        self.populate_roots()
        self.render_sections()

    def add_section(self):
        name = simpledialog.askstring(APP_NAME, "Section name:")
        if not name:
            return
        target = filedialog.askopenfilename(title="Pick file/executable")
        if not target:
            target = filedialog.askdirectory(title="Or pick folder")
            if not target:
                return
        image = filedialog.askopenfilename(title="Optional image", filetypes=[("Images", "*.png *.jpg *.jpeg *.webp")])
        shape = simpledialog.askstring(APP_NAME, "Shape (rectangle, rounded, circle, hexagon):", initialvalue="rounded") or "rounded"

        self.config_data["sections"].append({
            "name": name,
            "path": target,
            "image": image,
            "shape": shape.lower().strip(),
        })
        self.save_config()
        self.render_sections()

    def map_file_image(self):
        key = simpledialog.askstring(APP_NAME, "Enter extension (e.g. .exe) or full file path:")
        if not key:
            return
        image = filedialog.askopenfilename(title="Pick image", filetypes=[("Images", "*.png *.jpg *.jpeg *.webp")])
        if not image:
            return
        self.config_data["file_images"][key.strip()] = image
        self.save_config()
        messagebox.showinfo(APP_NAME, "Mapping saved.")

    def manage_theme(self):
        choice = simpledialog.askstring(APP_NAME, f"Current theme: {self.config_data['theme']}\nEnter existing theme name or new theme name:")
        if not choice:
            return
        choice = choice.strip()

        if choice not in self.config_data["themes"]:
            self.config_data["themes"][choice] = {
                "bg": self.pick_color("Background") or "#1e1e2f",
                "panel": self.pick_color("Panel") or "#282a3a",
                "card": self.pick_color("Card") or "#33354a",
                "text": self.pick_color("Text") or "#e7e7f0",
                "accent": self.pick_color("Accent") or "#6ea8fe",
            }
        self.config_data["theme"] = choice
        self.save_config()
        self.refresh()

    def pick_color(self, label):
        return simpledialog.askstring(APP_NAME, f"{label} color in hex (e.g. #112233):")

    def render_sections(self):
        for child in self.grid_frame.winfo_children():
            child.destroy()
        self.tk_images.clear()

        sections = self.config_data.get("sections", [])
        for idx, section in enumerate(sections):
            card = tk.Frame(self.grid_frame, width=260, height=190, relief="flat", bd=0)
            card.grid(row=idx // 4, column=idx % 4, padx=12, pady=12, sticky="nsew")
            card.grid_propagate(False)

            preview = self.build_preview(section.get("image"), section.get("shape", "rounded"), size=(230, 115))
            if preview:
                lbl = tk.Label(card, image=preview)
                lbl.image = preview
                self.tk_images.append(preview)
            else:
                lbl = tk.Label(card, text="No Image", font=("Segoe UI", 10))
            lbl.pack(pady=8)

            tk.Label(card, text=section.get("name", "Unnamed"), font=("Segoe UI", 11, "bold")).pack()
            tk.Label(card, text=section.get("path", ""), font=("Segoe UI", 8), wraplength=230).pack()

            tk.Button(card, text="Open", command=lambda p=section.get("path", ""): self.open_path(p)).pack(pady=6)
            self.recursive_color(card, self.theme)

    def build_preview(self, image_path, shape, size=(230, 115)):
        if not Image or not image_path or not os.path.exists(image_path):
            return None
        try:
            img = Image.open(image_path).convert("RGBA").resize(size)
            mask = Image.new("L", size, 0)
            draw = ImageDraw.Draw(mask)
            w, h = size

            if shape == "circle":
                draw.ellipse((0, 0, w, h), fill=255)
            elif shape == "hexagon":
                points = [(w * 0.2, 0), (w * 0.8, 0), (w, h * 0.5), (w * 0.8, h), (w * 0.2, h), (0, h * 0.5)]
                draw.polygon(points, fill=255)
            elif shape == "rectangle":
                draw.rectangle((0, 0, w, h), fill=255)
            else:
                draw.rounded_rectangle((0, 0, w, h), radius=20, fill=255)

            img.putalpha(mask)
            return ImageTk.PhotoImage(img)
        except Exception:
            return None

    def open_path(self, path):
        if not path:
            return
        try:
            os.startfile(path) if os.name == "nt" else os.system(f'xdg-open "{path}"')
        except Exception as exc:
            messagebox.showerror(APP_NAME, f"Could not open:\n{exc}")


def main():
    root = tk.Tk()
    app = LauncherExplorerApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
