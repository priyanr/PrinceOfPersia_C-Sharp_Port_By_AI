# POPCS — agent guide

This repo is a DOS-accurate C# (.NET 10) remake of Prince of Persia that runs off the
original DOS install. **`CLAUDE.md` is the authoritative guide** (build and run commands,
data sources, coordinates, architecture, pitfalls); read it first.

- `HISTORY.md` — findings, verification, abandoned approaches
- `PENDING.md` — open work
- `CHANGELOG.md` — newest first; add an entry when behaviour changes
- `SESSION_HISTORY.md` — per-session log

Golden rule: port the original (Apple II source or SDLPoP) routine by routine; don't tune
offsets or constants by hand.
