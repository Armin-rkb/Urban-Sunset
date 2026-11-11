# Urban Sunset: Web & Mobile Edition

A remake of the 2015 GD2A endless runner (the Unity 5 project in the repository root), rebuilt in
**Unity 6** for **WebGL**, with phones as the main target.

Once published it is playable at **https://armin-rkb.github.io/Urban-Sunset/**
(see the one-time setup below).

## How to play

| Action | Touch | Keyboard |
| --- | --- | --- |
| Jump (hold for higher) | Tap the **right side** of the screen | Space / ↑ / W |
| Double jump | Tap right again in the air | Space again |
| Slide (duck under crows & baseballs) | Hold the **left side** | ↓ / S / Z |
| Fast-fall | Hold left in the air, or **swipe down** | ↓ in the air |
| Pause | ❚❚ button (auto-pauses when you switch apps) | Esc / P |

You have 3 hearts. Pickups: **Soda** +1000, **Shield** (smash through everything for 6 s),
**Clock** (slow motion), **Heart** (+1 life). The city speeds up the longer you survive.

### Mobile controls
- Input fires on touch-down, not on release, so there is no added delay.
- Each finger is tracked on its own: hold slide with one thumb and tap jump with the other.
- Large invisible touch zones (left 40% / right 60%) with on-screen pads that light up when pressed.
- Jump buffering: a tap just before landing still jumps. Releasing early gives a short hop.
- Haptic feedback on hits and pickups (Android), safe-area aware HUD (notches), auto-pause on app switch.
- The web page blocks scroll, zoom and long-press menus. It has a fullscreen + landscape-lock button,
  a "turn your phone" hint, and can be installed to the home screen (PWA manifest) to play fullscreen on iOS.
- Works in portrait too: the camera widens the view so you can still see what's coming.

## Project layout

```
UrbanSunsetWeb/
  Assets/Scripts/        Game code (the whole game is built at runtime; there is no scene to wire up)
  Assets/Editor/         Import rules + WebGL build script (menu: Urban Sunset > Build WebGL)
  Assets/Resources/      Sprites, audio and font, reused from the original project
  Assets/Plugins/WebGL/  Browser bridge (vibration, share, touch detection)
  Assets/WebGLTemplates/ Mobile-friendly HTML page that hosts the game
```

Open the folder in Unity **6000.0.84f1** (any Unity 6.0 LTS should work). To try it in the editor, run
**Urban Sunset > Create Main Scene** once, open `Assets/Scenes/Main.unity` and press Play.

## One-time setup: automatic build & hosting (GitHub Pages)

`.github/workflows/webgl.yml` builds the game on every push with [GameCI](https://game.ci) and
publishes it to the `gh-pages` branch. Unity needs a licence to build, even in CI, so:

1. **Get your Unity licence file** (a free Personal licence works):
   sign in to Unity Hub on your computer, then copy the contents of `Unity_lic.ulf`:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`

   Details: https://game.ci/docs/github/activation
2. In the GitHub repo go to **Settings → Secrets and variables → Actions** and add:
   `UNITY_LICENSE` (the whole .ulf file), `UNITY_EMAIL`, `UNITY_PASSWORD`.
3. Re-run the **Build & publish WebGL** workflow (Actions tab → *Run workflow*).
4. After it succeeds, go to **Settings → Pages**, choose **Deploy from a branch**, branch `gh-pages`, folder `/ (root)`.
   The game appears at `https://<owner>.github.io/<repo>/` a minute later. Later pushes update it automatically.

The first build takes about 15 to 25 minutes. Later builds are faster because the Library folder is cached.

## Credits
Original game: GD2A student project (2015). Font: *Press Start 2P* by CodeMan38 (SIL Open Font License, see
`Assets/Resources/Fonts/OFL.txt`).
