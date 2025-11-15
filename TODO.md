
- fix scroll offset bug
when I navigate down with the arrow key, I sometimes see a part of the result below the one that is currently selected. Whether or not I see that depends on whether a header is currently visible or not. 
Concrete example:

(other items)
SONGS (header, visible)
song 1
song 2
-> song 3 (selected)
song 4 (I see the upper half of song 4)

other case:

SONGS (header, out of view)
song 1
song 2
...
song n-2
song n-1
-> song n (selected)
(song n appears right at the bottom, song n+1 is not visible at all until I navigate down, as it should be)

I think the number of visible headers matters for this as well. So when more headers are visible, I see more of the next song that I haven't yet reached.

Once again: When headers are visible, the scroll offset is too large and the selected item is shy of the bottom, and it looks like by exactly the sum of the heights of the visible headers.

---

- fix scroll offset bug
- add top match (can be turned off): displays an enlarged top match item as first result. 
    - this will require larger album thumbs
    - ensure top match thumbs are created first before any other result and also cached. make subsequent album thumbs retrieval fast by resizing from the top match thumb (if it has been cached) instead of the original.
- find a way to use musicbee's internal album cover thumb cache
- find a way to open artists in music explorer more directly
  - check if pure album artists can be opened like that in music explorer. if not, add a configurable action for pure album artists.
- add an album artist section in default results (only shown when album artist not contained in artists)
- readd icons on the right (configurable), and make icon size fixed
- fix all dpi issues
- lyrics search support?
- make settings more descriptive and add tooltips
- should lyrics search have contains check always disabled, regardless of config?
- maybe relax contains check to ignore ws (xcla should match x-cla)
- add a way to open tag editor on a selected result
- add alt+enter actions (default to tag editor)
- add a way to search in a specific album, artist, or playlist. e.g. inalbum:, alias ina:, example: inalbum:Album Name; Track Name. Think whether or when to use exact match for in: queries.
- add a way to insert a result as an in: query (left arrow)
- add a menu for each result item (right arrow or right click to open). opens a separate list of actions.


- search sort artists (maybe also for songs and albums)

- fix sort artists in music explorer (honorifics, dj prefixes)
	- maybe it's possible to open an artist in music explorer programmatically?

- album artist search (only show the ones that don't appear in the artists table)

- add composers

- improve result ordering when group by type is off: If artist=Foo and song or album name is FooBar, searching for Fo should display the artist first (naturally, without special cases)

- rounded corners, change scrollbar color? (probably need another UI library for that)

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