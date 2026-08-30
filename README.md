# DOSBox Selector

Right-click empty space in a Dolphin folder, choose **DOSBox Here**, and pick which DOSBox
flavor to run. The folder is mounted as `C:` and you land at a DOS prompt.

If the folder contains a `.conf` file, the flavor it was written for is highlighted and
pre-selected.

## Multiple config files

Configs are a **ticked, ordered list**, not a single choice. Every fork accepts its config
flag more than once and applies the files left to right, so a later file overrides an
earlier one — while `[autoexec]` sections *accumulate* rather than replace. That's the
split base-plus-override layout GOG ships:

```
dosbox_game.conf          memsize = 16,  [autoexec] mount C .
dosbox_game_single.conf   memsize = 63,  [autoexec] echo OVERRIDE APPLIED
```

Run together, you get 63 MB *and* both autoexec blocks. Use ▲/▼ to change the order; the
numbers on the left show the sequence and the command preview updates as you go.

Verified against DOSBox 0.74-3, Staging 0.82.2 and DOSBox-X 2026.08.02 — all three behave
the same way.

**What gets ticked by default:** if any file names the selected flavor, just those. If none
of them identify themselves — hand-written configs like GOG's carry no generator header —
all of them, since a split set is normally meant to be applied together. Either way it's a
starting point; retick and reorder freely, and the preview always shows exactly what runs.

Built for KDE Plasma 6 / Dolphin on .NET 10 with Avalonia.

## Install

```sh
./install.sh
killall dolphin     # restart Dolphin so it notices the new menu entry
```

This publishes the app to `~/.local/share/dosbox-selector/` and writes a service menu to
`~/.local/share/kio/servicemenus/dosbox-here.desktop`. `./uninstall.sh` reverses it and
leaves your flavor list alone.

## Flavors

Three are configured out of the box — DOSBox (vanilla), DOSBox Staging and DOSBox-X — but
none of that is hardcoded. **Manage flavors…** in the picker (or `DosBoxSelector --manage`)
edits the list, and adding a fourth fork is pure data:

| Field | Meaning |
|---|---|
| Executable / Flatpak app ID | `dosbox`, `/home/you/DOSBox/dosbox`, or `com.dosbox_x.DOSBox-X` |
| Config flag | `-conf` or `--conf` — forks disagree. Omitted when you launch without a config. |
| Config filenames | Globs this flavor claims, e.g. `dosbox-x.conf; dosbox-x-*.conf` |
| Version text to confirm | Expected in `-version` output — see below |
| Extra places to look | Globs searched when the command isn't on your PATH |
| Mount folder as C: | Adds `-c "MOUNT C <dir>" -c "C:"` |
| Extra arguments | Free-form; `{dir}` and `{conf}` are substituted |

Stored as JSON at `~/.config/dosbox-selector/flavors.json`.

### Why "version text to confirm" exists

Two flavors can share a binary name. dosbox-staging's package installs its binary as plain
`/usr/bin/dosbox` — the same name vanilla DOSBox uses — so looking up the name is not enough
to know which fork you actually have. Each candidate binary is run with `-version` and the
output checked against this field, so Staging claims `/usr/bin/dosbox` and vanilla falls
through to its search paths.

The probe flag is single-dash on purpose: vanilla DOSBox 0.74-3 doesn't recognise long
options and responds to an unrecognised argument by **launching the emulator**. Probes run
with stdin closed and are killed after 5 seconds so a bad entry can't leave a window open.

### Flatpak and folders outside $HOME

The DOSBox-X flatpak ships with `filesystems=home`, so a folder on another drive is invisible
inside its sandbox and the mount silently fails with *"Drive C does not exist!"*. Every
flatpak launch gets `--filesystem=<folder>` for that run only — no permanent permission
change to the app.

## Troubleshooting

```sh
DosBoxSelector /path/to/folder --diagnose
```

Prints which config files were found, which flavor each was matched to and why, which
flavors were detected and where, and the exact command each would run. No window opens.
