
### (FINAL)

- what if two distinct artists are normalized to the same key?
  - In the current code, the last one wins (bad!)
  - Instead, let's only deduplicate when only case differs (i.e. Artist = artist), not when they conincide in full normalization.
  - Same issue is present for albums!
- ignore diacritics
- improve colors for icons and detail text in terrible setups like https://github.com/fiso64/MusicBeeSearchBar/issues/4

---

- add a way to customize opened filter. prompt:
```
for the open filter in tab action settings, add a new checkbox Use Custom Filter, as well as a button "Customize Filter" at the bottom (button should be greyed out when the checkbox is off). Clicking it should bring up a new customization form, where users can customize up to two filters. Everything should be completely customizable.

filter item: MetadataType, ComparisonType, value

all three all dropdowns. the first two are clear. the available values in the third dropdown will depend on the result type that this action belongs to. for example, for song results, it will have TrackTitle, Artist, SortArtist, Filepath. 
```

- lyrics search support?
  - should lyrics search have contains check always disabled, regardless of config?
- add a way to open tag editor on a selected result
- add alt+enter actions (default to tag editor)
- add a way to search in a specific album, artist, or playlist. e.g. inalbum:, alias ina:, example: inalbum:Album Name; Track Name. Think whether or when to use exact match for in: queries.
- add a way to insert a result as an in: query (left arrow)
- add a menu for each result item (right arrow or right click to open). opens a separate list of actions.

- improve result ordering when group by type is off: If artist=Foo and song or album name is FooBar, searching for Fo should display the artist first (naturally, without special cases)

- add s: synonym prefix for t:. Add other prefix synonyms, like al: (whatever is defined in musicbee)

- add alt+shift+a/r/s shortcuts to prepend l:, a: and s: respectively (and delete previous prefix if there is one)

- fix ctrl+backspace not working when text is selected

- more configuration: result type order, prefixes, "enabled" (bools controlling whether result type included in index), "require prefix" (bools controlling whether or not prefix is required), etc.

- test performance on large library and optimize if needed

- fully configurable and arbitrary shortcuts for every item type

- right click context menu listing all possible actions. For song and album results add also submenus Album and Artist to perform the actions on the album or artist (e.g song result right click > Artist > Play will play the artist). Is it possible to display musicbee's context menu?

- make result type order used by groupByType configurable

- arbitrary tag search, like side and genres. Arbitrary tags should be excluded by default from the database index but can be enabled manually, with a configurable keyword that defaults to 'tagname:'. When enabled a corresponding hotkey is automatically added as well. Display a table in configuration:
'tagname | keyword | require keyword | enable'
where the last two columns are checkboxes, 'require keyword' checked by default, 'enable' unchecked. Multiple keywords can be enabled if they are separated with ; in the keyword column.

- multi-tag filtering, e.g `artist:foo genre:bar`. Bonus: draw a box around each tag:value pair for a visual indication.