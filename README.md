# DarkScript3

User-friendly editor for FromSoftware's EMEVD format. For basic usage instructions, visit the [tutorial](http://soulsmodding.wikidot.com/tutorial:learning-how-to-use-emevd).

## Tips

You can use the `CodeBlock` class to avoid having to keep track of line skip counts.

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

You can also define events or helper functions in other JS files and import them like below. **The path is relative to the program EXE, not the event file.** If you want to import a file from elsewhere, use the absolute path.

```js
Scripter.Import("path/to/my/file.js");
```

This is not a true `import` statement, so duplicate variable/const/function names –– even across different files –– will cause problems. Avoid this with proper scoping or fully unique naming.


## Images
![DarkScript 3 screenshot](https://i.imgur.com/mKBkZuk.png)

