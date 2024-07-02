# DarkScript3

User-friendly editor for FromSoftware's EMEVD format with high-level language and project features. For basic usage instructions, visit the [general tutorial](http://soulsmodding.wikidot.com/tutorial:learning-how-to-use-emevd) or [Elden Ring-specific tutorial](http://soulsmodding.wikidot.com/tutorial:intro-to-elden-ring-emevd).

DarkScript3 also includes MattScript, a way of formatting scripts (except in DS2, which has no control flow) which adds even more high-level language features, making scripts easier to understand and edit. See [MattScript documentation](http://soulsmodding.wikidot.com/tutorial:mattscript-documentation).

.NET 6.0 Desktop and ASP.NET Runtimes are required for 3.4.x. Install them from https://aka.ms/dotnet/6.0/windowsdesktop-runtime-win-x64.exe and https://aka.ms/dotnet/6.0/aspnetcore-runtime-win-x64.exe before attempting to run the tool.

## Supported games

* Dark Souls ([instruction list](https://soulsmods.github.io/emedf/ds1-emedf.html))
* Bloodborne ([instruction list](https://soulsmods.github.io/emedf/bb-emedf.html))
* Dark Souls II ([instruction list](https://soulsmods.github.io/emedf/ds2-emedf.html))
* Dark Souls II: SOTFS ([instruction list](https://soulsmods.github.io/emedf/ds2scholar-emedf.html))
* Dark Souls III ([instruction list](https://soulsmods.github.io/emedf/ds3-emedf.html))
* Sekiro ([instruction list](https://soulsmods.github.io/emedf/sekiro-emedf.html))
* Elden Ring ([instruction list](https://soulsmods.github.io/emedf/er-emedf.html))
* Aside from these, you can bring your own EMEDF JSON file

## Images
![DarkScript 3 screenshot](https://i.imgur.com/wXufTwa.png)

## Tips

### Keyboard shortcuts

Aside from the usual text file navigation, there are many hotkeys supported.
Some useful ones are:

* Ctrl+F, Ctrl+H - show find/replace dialogs (select text first to auto-fill them)
* Ctrl+G - show goto-line dialog
+ Ctrl+Z, Ctrl+Shift+Z or Ctrl+Y - undo/redo
* Tab, Shift+Tab - indent/unindent text
* Ctrl+Shift+C - comment/uncomment text
* Ctrl+Scroll - zoom in/out
* Ctrl+Space - open autocomplete menu
* Ctrl+-, Ctrl+Shift+- - backwards/forwards line navigation
* Ctrl+Tab, Ctrl+Shift+Tab - forwards/backwards tab
* Ctrl+Click or Ctrl+Enter on number - go to event definition from anywhere, or event initialization from definition
* Ctrl+1 on number - show byte-equivalent float for an integer

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

## Contributors

AinTunez - creator of DarkScript3 and main scripting format  
thefifthmatt (gracenotes) - feature development and MattScript  
HotPocketRemix - EMEDFs and reversing help  
Meowmaritus - dark menu and SoulsFormats contributions  
TKGP - further SoulsFormats contributions  
Pav, Grimrukh, others - additional reversing help  
