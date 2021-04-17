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

You can also define your own helper functions in other JS files and import them like this:

```js
Scripter.Import("path/to/my/file.js");
```

## Images
![DarkScript 3 screenshot](https://i.imgur.com/mKBkZuk.png)

