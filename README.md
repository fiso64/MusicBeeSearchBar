# MusicBee Search Bar

A modern, customizable search bar plugin for MusicBee that provides unified search across your music library.
Also functions as a command palette that can search MusicBee and plugin commands.
  
<video src="https://github.com/user-attachments/assets/4a83946e-6c1e-40f3-98b9-fd58b875b417" width="800" autoplay loop muted playsinline></video>

## Features

- **Unified Search**: Search for artists, albums, and songs all in one place
- **Fuzzy Search**: Intelligent matching that always prioritizes the best results
- **Filtering**: Use prefixes to filter results:
  - `a:` for artists
  - `l:` for albums
  - `s:` for songs
  - `p:` for playlists
  - `>`  for commands
- **Customizable Actions**: Configure different actions for each result type:
  - Default click / Enter
  - Ctrl + click / Enter
  - etc.
- **Action Types**:
  - Play (with optional shuffle)
  - Queue Next
  - etc.
- **Quick Launcher**: Can be used as a quick song/album selector while MusicBee is hidden

## Installation

1. Download the the latest release [here](https://github.com/fiso64/MusicBeeSearchBar/releases/latest).
2. Extract the files into the MusicBee Plugins folder (usually located at `MusicBee\Plugins`).
3. Restart MusicBee
4. Set up a hotkey in MusicBee's preferences to show the search bar.

## Keyboard Shortcuts

- `Alt+R`: Execute artist action for current result
- `Alt+A`: Execute album action for current result
- `Ctrl+P`: Open configuration
- `Enter`: Execute action
- `Ctrl/Shift + Enter`: Execute alternative actions

## Command Palette Notes
The command palette feature (activated with the `>` prefix) can be used to search and execute built-in or plugin commands. Note:
- Some commands may fail to execute, or give an error
- Some command names might differ from how they appear in MusicBee's context menus

If you find inconsistent command names or broken commands, you can create an issue [here](https://github.com/fiso64/MusicBeeSearchBar/issues).


## Performance Note
If you experience any slowness (i.e., either the startup or the search after a character is typed does not complete instantly), then please create an issue on [github](https://github.com/fiso64/MusicBeeSearchBar/issues) (also mention your library statistics).
There are a few easy ways to make it faster which I haven't bothered to implement because the performance on my small personal library (~11000 tracks) as well as on a synthetic library of 1000000 tracks is good.
