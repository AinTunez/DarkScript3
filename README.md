# DarkScript3

User-friendly editor for FromSoftware's EMEVD format. For basic usage instructions, visit the [tutorial](http://soulsmodding.wikidot.com/tutorial:learning-how-to-use-emevd).

## Images
![DarkScript 3 screenshot](https://i.imgur.com/mKBkZuk.png)

## Tips

### Keyboard shortcuts

Aside from the usual text file navigation, there are many hotkeys supported.
Some useful ones are:

* Ctrl+F, Ctrl+H - show find/replace dialogs (select text first to auto-fill)
* F3 - find next
* Ctrl+G - show goto-line dialog
* Tab, Shift+Tab - indent/unindent text
* Ctrl+Shift+C - comment/uncomment text
* Ctrl+Scroll - zoom in/out
* Ctrl+Space - open autocomplete menu
* Ctrl+Click, Ctrl+Enter on number - go to event definition from anywhere, or event initialization from definition
* Ctrl+-, Ctrl+Shift+- - backwards/forwards navigation

### Importing other files

You can define events or helper functions in other JS files and import them as JavaScript modules.

```js
import { Boss, BossFlag, checkBossFlag } from "mod.js";
```

An example mod.js, in the same directory as the emevd file, might look as follows:

```js
export const Boss = {
    ARTORIAS: 1210820,
    KALAMEET: 1210400,
    SUPER_KALAMEET: 1210420,
};

export const BossFlag = {
    ARTORIAS: 11210001,
    KALAMEET: 11210004,
    SUPER_KALAMEET: 11210006,
};

export function checkBossFlag(eventFlag) {
    EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventFlag, eventFlag);
}
```

This can be used in the emevd file like `checkBossFlag(BossFlag.SUPER_KALAMEET);`. This example is
overly simplistic, and it might hurt script readability to make too many trivial helpers.
Note that functions like this don't act like events and still have to be called from within events,
so common_func is usually preferable in games where it is available. For games without common_func,
events defined in imported scripts will be added to the emevd as if they were defined in the script
that imports them.

See [import](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Statements/import)
and [export](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Statements/export)
for all syntax options, including namespaced imports.

### CodeBlock

In regular mode (using Event instead of $Event), you can use the `CodeBlock` class
to avoid having to keep track of line skip counts.

```js
Event(12345, Restart, function () {
  
  // create a code block
  const block = new CodeBlock(() => {
    SetEventFlag(760, OFF);
    SetEventFlag(762, OFF);
    SetEventFlag(765, OFF);
    // ...
    EndUnconditionally();
  });
  
  // pass the length to the skip instruction
  SkipIfEventFlag(block.length, OFF, TargetEventFlagType.EventIDAndSlotNumber, 12345000);
  
  // execute the block
  block.Exec();
})
```
