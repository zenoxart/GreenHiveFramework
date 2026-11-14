# Images

| File | Content |
|---|---|
| `logo.svg` | Green Hive logo (same as the app icon `src/GreenHive.Wpf.UI/Assets/GreenHive.ico`) |
| `screenshot-shell.svg` | shell: Module Explorer, documents overview, AI agent tool window |
| `screenshot-inbox.svg` | workflow inbox with an AI proposal and the generated review form |
| `screenshot-reports.svg` | reports: parameters, chart and table |
| `screenshot-profile.svg` | profile flyout in the title area |
| `screenshot-wheel.svg` | navigation wheel (`Ctrl+Tab`): modules inside, open views outside |

The `screenshot-*.svg` files are **illustrations** of the real views (same layout, texts and demo data).
To replace them with real screenshots: start the backend and the client, sign in as `maria.huber`, set the window
to 1440 × 900, capture the views (`Win+Shift+S`) as PNG with the same names, and change the extensions
in the main `README.md`.

The app icon is generated from `logo.svg` (16–256 px, PNG frames):
`python tools/make-icon.py docs/images/logo.svg src/GreenHive.Wpf.UI/Assets/GreenHive.ico` (needs `rsvg-convert`).
