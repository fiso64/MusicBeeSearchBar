# MusicBeeSearchBar

A modern, customizable search bar plugin for MusicBee that provides unified search across your music library.
  
  
<img src="/demo.gif" width="700" height="auto"/>



## Features

- **Unified Search**: Search for artists, albums, and songs all in one place
- **Fuzzy Search**: Intelligent matching that always prioritizes the best results
- **Filtering**: Use prefixes to filter results:
  - `a:` for artists
  - `l:` for albums
  - `s:` for songs
  - `p:` for playlists
- **Customizable Actions**: Configure different actions for each result type:
  - Default click
  - Ctrl + click
  - Shift + click
  - Ctrl + Shift + click
- **Action Types**:
  - Play (with optional shuffle)
  - Queue Next
  - Queue Last
  - Search in Tab
  - Open Filter
- **Quick Launcher**: Can be used as a quick song/album selector while MusicBee is hidden

## Installation

1.  Download the the latest release [here](https://github.com/fiso64/MusicBeeSearchBar/releases/latest).
2.  Extract the files into the MusicBee Plugins folder (usually located at `MusicBee\Plugins`).
3.  Restart MusicBee.
4.  The plugin should now be available in Preferences > Plugins.

## Usage

1. Set up a hotkey in MusicBee's preferences to show the search bar
2. Press the configured hotkey to open the search bar
3. Start typing to search
4. Use arrow keys to navigate results
5. Press Enter to execute the default action
6. Use modifier keys (Ctrl/Shift) with Enter or click for alternative actions

## Configuration

Access the configuration panel through:
- MusicBee Preferences > Plugins > Modern Search Bar > Configure
- Or press Ctrl+P while the search bar is open

### Configurable Options

- **Actions**: Configure what happens when selecting results
  - Different actions for artists, albums, songs, etc
  - Separate actions for different key combinations
  - Various action types with customizable parameters

- **Appearance**:
  - Base color
  - Maximum visible results
  - etc

## Keyboard Shortcuts

- `Alt+D`: Focus search box
- `Alt+R`: Execute artist action for current result
- `Alt+A`: Execute album action for current result
- `Ctrl+P`: Open configuration
- `Esc`: Close search bar
- `Up/Down`: Navigate results
- `Enter`: Execute action
- `Ctrl/Shift + Enter`: Execute alternative actions
