# Steam Loading Popups website

A static two-page site: `index.html` (landing page) and `download.html`
(always links to the latest GitHub release). No build step, no framework,
no dependencies — plain HTML/CSS plus a small inline script.

## How the download button stays up to date

`download.html` fetches `https://api.github.com/repos/bobchomp/gameloading/releases/latest`
client-side, finds the `.exe` asset, and points the Download button straight
at it (showing the version, file size, and release date). If that request
ever fails (rate limiting, no releases yet, etc.), the button falls back to
linking at `https://github.com/bobchomp/gameloading/releases/latest`, which
always resolves to the newest release on GitHub's side. Either way the
button always works — nothing to update here when a new version ships.

## Deploying to Vercel

1. Import this GitHub repo into a new Vercel project.
2. In the project's settings, set **Root Directory** to `website`.
3. Framework preset: **Other** (no build command / output directory needed —
   it's plain static files).
4. Deploy. `vercel.json` in this folder enables clean URLs, so `/download`
   serves `download.html` without the `.html` extension.

## Local preview

Any static file server works, e.g. from this folder:

```bash
python3 -m http.server 8000
```

Then open `http://localhost:8000`.
