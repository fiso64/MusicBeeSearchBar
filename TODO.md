
- fix first music explorer search misbehaving

- search sort artists (maybe also for songs and albums)

- rounded corners

- show images for all result types

- disallow matching a single text part multiple times. E.g query 'NAME NA' should not match 'NAME'.

- add s: synonym prefix for t:. Add other prefix synonyms, like al: (whatever is defined in musicbee)

- add alt+shift+a/r/s shortcuts to prepend l:, a: and s: respectively (and delete previous prefix if there is one)

- fix ctrl+backspace not working when text is selected

- use the musicbee skin color for the search bar by default

- more configuration: result type order, prefixes, "enabled" (bools controlling whether result type included in index), "require prefix" (bools controlling whether or not prefix is required), etc.

- test performance on large library and optimize if needed

- Setup wizard on first run: First screen: Configure actions for the three result types. Second screen: Prompt to go to hotkey settings and assign a hotkey to open the search bar.

- see if it's possible to open specific tab types (music explorer, library) programmatically

- playlist search, prefix p: (maybe require prefix to include playlists in results). Add way to open the playlist SearchInTabInLeftSidebar: Switch to tab, focus left sidebar somehow, type the name of the item. Careful: If there are two playlists in the sidebar starting with the same prefix then typing that prefix will switch focus between the two playlists every time a letter is typed. Maybe instead of new action type, add boolean 'UseTypeSpecificSearchBar' = true which will make SearchInTab use the left sidebar only for playlist results.
- Also, consider what needs to be typed for playlists that are in one or multiple parent folders. Also, consider what it should do for playlists when set to OpenFilterInTab

- fully configurable and arbitrary shortcuts for every item type

- command pallete with > prefix
  - find a way to get a list of inbuilt command names
  - list inbuilt and plugin commands in the palette
  - add shortcut to show the palette
  - integrate with morehotkeys to add custom commands to the command palette, and add a way to remove the default palette entries

- right click context menu listing all possible actions. For song and album results add also submenus Album and Artist to perform the actions on the album or artist (e.g song result right click > Artist > Play will play the artist). Is it possible to display musicbee's context menu?

- make result type order used by groupByType configurable

- better ui (e.g larger album items, maybe multiple artists on one line) to easily differentiate between artist/album/song. Maybe modify search to include album songs when searching for an album, and show them on the right of the larger album entry.

- arbitrary tag search, like side and genres. Arbitrary tags should be excluded by default from the database index but can be enabled manually, with a configurable keyword that defaults to 'tagname:'. When enabled a corresponding hotkey is automatically added as well. Display a table in configuration:
'tagname | keyword | require keyword | enable'
where the last two columns are checkboxes, 'require keyword' checked by default, 'enable' unchecked. Multiple keywords can be enabled if they are separated with ; in the keyword column.

- multi-tag filtering, e.g `artist:foo genre:bar`. Bonus: draw a box around each tag:value pair for a visual indication.

- integrate with morehotkeys to override result accept action with a custom one, maybe override for a specific result type only
- maybe custom shortcuts for search bar via more hotkeys, or via ui
- configure search result context menu via MoreHotkeys