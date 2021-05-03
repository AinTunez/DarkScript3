# MattScript

MattScript is an uncreatively named extension to DarkScript3 developed by
thefifthmatt which adds additional syntax for control flow and condition
expressions for writing advanced scripts with ease. It can be activated in
events by defining them with `$Event` instead of `Event`.

On its own, DarkScript3 works by evaluating a JavaScript file which calls many
instruction functions. Each instruction function adds an instruction to an
event. Inside of `Event`s, you can also use other JavaScript like helper
functions and loops.

Inside of `$Event`s, JavaScript constructs are instead used to represent control
flow logic in the script itself. When saving a file, they are not run as
JavaScript directly. They are manually parsed, translated into a list of
instructions as a preprocessing step, and *then* run.

For example, an `$Event` might have this code:

```js
$Event(11005000, Default, function(X0_4, X4_4, X8_4) {
    if (ThisEventSlot()) {
        DeactivateObject(X4_4, Disabled);
        EndEvent();
    }
    WaitFor(EntityInRadiusOfEntity(10000, X0_4, 3));
    ForceAnimationPlayback(X4_4, X8_4, false, true, false);
    DeactivateObject(X4_4, Disabled);
});
```

When saving a file, it is converted into this equivalent `Event` which is then
executed by DarkScript3:

```js
Event(11005000, Default, function(X0_4, X4_4, X8_4) {
    SkipIfEventFlag(2, OFF, TargetEventFlagType.EventIDAndSlotNumber, 0);
    DeactivateObject(X4_4, Disabled);
    EndUnconditionally(EventEndType.End);
    IfEntityInoutsideRadiusOfEntity(MAIN, InsideOutsideState.Inside, 10000, X0_4, 3);
    ForceAnimationPlayback(X4_4, X8_4, false, true, false);
    DeactivateObject(X4_4, Disabled);
});
```

Files can be automatically converted to `$Event` by selecting "Convert To
MattScript" in one of two places. It can be done when opening a new emevd
file. It can also be performed on an existing JS file from the Edit menu. This
will attempt to rewrite the entire file while preserving existing comments, and
also show a preview of the converted version. Once you have a converted file,
you can also preview its simpler compiled form in the View menu.

## Condition expressions

`$Event`s add condition functions for things which would otherwise be
standalone instructions. These can only be used in a few specific places:
`if` statements, built-in control flow commands like `WaitFor`, and condition
variables. These are all turned into various instructions in the end.

A condition function cannot be used as an argument to another function or
instruction. It can only be used in specific constructs described below.

A very commonly used condition function is event flag state, which is
represented with `EventFlag(400)` to check if a flag is on and `!EventFlag(400)`
to check if it's off. Most condition checks can be negated in this way. This
function will be compiled into `IfEventFlag`, `EndIfEventFlag`,
`SkipIfEventFlag`, or `GotoIfEventFlag` depending on how it's used.

Most condition functions have multiple versions. For instance, the instruction
`IfPlayerHasdoesntHaveItem(AND_02, ItemType.Goods, 9405, OwnershipState.DoesntOwn);`
can be represented with the function
`PlayerHasdoesntHaveItem(ItemType.Goods, 9405, OwnershipState.DoesntOwn)`,
but there's also a shorter version where the yes/no enum is turned into regular
negation, as `!PlayerHasItem(ItemType.Goods, 9405)`. The longer version is
only used if this isn't possible, like if the enum is parameterized.

Condition functions based on comparing numerical values can also use comparison
operators directly. The instruction
`IfCharacterHPRatio(AND_03, 10000, ComparisonType.LessOrEqual, 0, ComparisonType.Equal, 1);`
can be represented as the condition function
`CompareHPRatio(10000, ComparisonType.LessOrEqual, 0)`.
In this case, the entity-group-related arguments are allowed to be optional.
It can also be represented in the even shorter form `HPRatio(10000) <= 0`. For
comparisons like this, the function call is always on the left side.
It is forbidden to call `HPRatio` outside of a comparison.
Direct numerical comparisons like `X0_4 != 0` are also permitted, using
instructions like `IfParameterComparison` under the hood.

As a simple example, suppose there is an area in the map, and as long as a
fight is active, we want to slowly auto-heal the player as long as they are
under 70% HP and in that area. A simple version of that might be as follows:

```js
$Event(13705010, Restart, function() {
    WaitFor(InArea(10000, 3704310));
    EndIf(EventFlag(13700860));
    if (HPRatio(10000) < 0.7) {
      SetSpEffect(10000, 4099);
      WaitFixedTimeSeconds(2.5);
    }
    RestartEvent();
});
```

Three condition functions are used here, `InArea`, `EventFlag`, and `HPRatio`.
Clicking on any of them in DarkScript3, you can see their arguments and
alternate versions in the doc view.

`WaitFor` is the only way to make a script temporarily stop based on a
condition. `if`, `EndIf`, and everything else all check if the condition is
true at that very moment and then act immediately based on that.

Finally, condition functions can be combined using the logical operators `&&`
and `||`. A more standard way of writing the event might be as follows:

```js
$Event(13705010, Restart, function() {
    WaitFor(InArea(10000, 3704310) && HPRatio(10000) < 0.7);
    EndIf(EventFlag(13700860));
    SetSpEffect(10000, 4099);
    WaitFixedTimeSeconds(2.5);
    RestartEvent();
});
```

## Condition variables

An even more fromsoft-ian way of structuring the above example might be as
follows, which brings us to condition variables:

```js
$Event(13705010, Restart, function() {
    fightOver = EventFlag(13700860);
    WaitFor(fightOver || (InArea(10000, 3704310) && HPRatio(10000) < 0.7));
    EndIf(fightOver.Passed);
    SetSpEffect(10000, 4099);
    WaitFixedTimeSeconds(2.5);
    RestartEvent();
});
```

Every `x = y` statement in an `$Event` is assumed to be a condition variable
assignment (excluding const declarations; see below). They allow more direct
access to condition groups.

For the most part, `$Event` hides details of how condition groups are
structured. Expressions using `||` and `&&` are converted to use them behind the
scenes, like this:

```js
$Event(13705011, Restart, function() {
    WaitFor(InArea(10000, 3704310) && HPRatio(10000) < 0.7);
});

Event(13705011, Restart, function() {
    IfInoutsideArea(AND_01, InsideOutsideState.Inside, 10000, 3704310, 1);
    IfCharacterHPRatio(AND_01, 10000, ComparisonType.Less, 0.7, ComparisonType.Equal, 1);
    IfConditionGroup(MAIN, PASS, AND_01);
});
```

Another way of writing the event using an explicit condition variable would be
like this. (Note: it is strongly recommended to use the `&&` expression instead,
because explicit groups aren't automatically reused and there is a finite
number of total groups.)

```js
$Event(13705011, Restart, function() {
    areaLowHp &= InArea(10000, 3704310);
    areaLowHp &= HPRatio(10000) < 0.7;
    WaitFor(areaLowHp);
});
```

A summary of their rules:

1. Every assignment to a condition groups/variable adds another thing to check
   for when evaluating them.
1. Regular condition groups/variables come in two types, AND groups and OR
   groups. Adding a condition to an AND group adds another thing that must be
   true. Adding a condition to an OR group adds an alternative way for it to be
   true.
   
   These are represented using the `&=` and `|=` assignment operators in
   `$Event`s. These operators add a new condition to the given condition
   variable (which does *not* need to be declared in advance). You can also use
   `=` for standalone assignments, which will automatically turn into either
   `&=` or `|=` during compilation as appropriate.
1. After a `WaitFor` passes (also called the MAIN group, a special group that
   waits for its condition to become true), all condition variable conditions
   are cleared. All of their previous requirements are forgotten, and must be
   redeclared if they are to be reused in the same way.
1. However, after a `WaitFor`, it *does* remember which conditions were true or
   false at the time the overall check succeeded. This is called "compiled"
   condition group state in regular emevd, and is represented by a made-up
   property called `.Passed` here.
1. (edge cases) `.Passed` values will persist across multiple `WaitFor`s as long
   as their condition groups are not redeclared and
   `ClearCompiledConditionGroupState()` is not used. If condition variables are
   referenced without being assigned to (don't do this!), they are true by
   default, except for unassigned `.Passed` expressions which are false by
   default.
   
tl;dr: Do not reuse condition variables after a `WaitFor` to reuse their logic!!
The most you can do is either redeclare their logic or check `myCond.Passed`
retroactively.

Note: condition groups named `and01`/`or02`/etc. will directly use that
condition group number, but it is preferable to use more descriptive names where
possible.

A common usage of condition variables is to have conditions whose definitions
can change based on event parameters, like this excerpt from the DS3 fog gate
traversal event:

```js
$Event(20005800, Restart, function(X0_4, X4_4, X8_4, X12_4, X16_4, X20_4, X24_4, X28_4) {
    ...
    if (X28_4 != 0) {
        areaFlag |= InArea(10000, X28_4);
    }
    areaFlag |= EventFlag(X24_4);
    cond &= areaFlag && !PlayerIsNotInOwnWorld();
    WaitFor(cond);
    ...
});
```

Plenty of other interesting uses are possible, like this Sekiro event.
Condition variables can basically be used anywhere a condition is expected, not
just in `WaitFor`s.

```js
$Event(11105130, Restart, function() {
    SetEventFlag(11100130, OFF);
    if (!EventFlag(8302)) {
        flag |= EventFlag(11100301);
    }
    if (EventFlag(8302)) {
        flag |= EventFlag(11100480);
    }
    if (flag) {
        SetEventFlag(11100130, ON);
    }
    WaitFor(
        EventFlagState(CHANGE, TargetEventFlagType.EventFlag, 8302)
            || EventFlagState(CHANGE, TargetEventFlagType.EventFlag, 11100301)
            || EventFlagState(CHANGE, TargetEventFlagType.EventFlag, 11100480));
    RestartEvent();
});
```

In this case, you could also implement it as follows, although this uses
slightly more condition groups behind the scenes.

```js
$Event(11105130, Restart, function() {
    if ((!EventFlag(8302) && EventFlag(11100301))
        || (EventFlag(8302) && EventFlag(11100480))) {
        SetEventFlag(11100130, ON);
    } else {
        SetEventFlag(11100130, OFF);
    }
    ...
});
```

The key thing to keep in mind is that conditions will keep accumulating
until a `WaitFor`, after which point they will be wiped out.

## Control flow

`$Event`s have a bunch of built-in commands which are used to support condition
functions and various high-level control flow constructs.

### WaitFor(cond)

Pauses execution if the given condition is not true, and unpauses it when it
becomes true.

In plain emevd, this corresponds to MAIN group evaluation.

### EndEvent(), RestartEvent()

Ends or restarts the event.

These are directly equivalent to `EndUnconditionally` in plain emevd.

### EndIf(cond), RestartIf(cond)

Ends or restarts the event if the condition is true, and continues to the next
instruction otherwise.

This is equivalent to various `EndIf` instructions in plain emevd.

### if (cond), else

Enters the if block if the condition is true. Otherwise, if there is an else
block, it enters that that.

This is syntactic sugar for various skip/goto statements. It will turn into a
goto rather than a skip if it's in a game that supports labels and there's a
label command on the jump target.

### Labels

These are declared as JavaScript labels, and they come in two types.

The first is a label command, only available in Bloodborne onwards, and these
are represented using labels name `L0` `L1` all the way up to `L20`.

The second is a synthetic label only used within the compiler for calculating
skip line amounts. It can be named anything.

A toy example of their use:

```js
$Event(9194, Default, function() {
    GotoIf(L0, EventFlag(6006));
    Goto(waitToRestart);
L0:
    SetEventFlag(6006, OFF);
waitToRestart:
    WaitFor(EventFlag(6007));
    RestartEvent();
});
```

Becomes:

```js
Event(9194, Default, function() {
    GotoIfEventFlag(Label.LABEL0, ON, TargetEventFlagType.EventFlag, 6006);
    SkipUnconditionally(2);
    Label0();
    SetEventFlag(6006, OFF);
    IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, 6007);
    EndUnconditionally(EventEndType.Restart);
});
```

One downside of using JavaScript labels is that they must always have a
statement following them, so in order to use a label at the end of an event
or block, you can use the fake `NoOp();` (no operation) command as a label
target. As the name implies, it completely disappears in compilation.

### Goto(label)

Jumps to the given real or synthetic label by name. Jumps are only allowed in
a forwards direction. If you want to revisit previous parts of an event, you
must carefully restart it.

`Goto`s are turned into `GotoUnconditionally` and `SkipUnconditionally`
instructions depending on whether real labels are used or not.

### GotoIf(label, cond)

Jumps to the given real or synthetic label if the condition is true, otherwise
continues to the next line.

`GotoIf`s are turned into various `GotoIf` and `SkipIf` instructions in plain
emevd depending on whether real labels are used or not.

## Mixing regular JavaScript

Because JavaScript is mainly used for condition group definitions and for
emevd control flow, the use of JavaScript for script-writing helpers is
significantly limited in `$Event`s.

There are a few places where you can use arbitrary JavaScript which will be
left as-is when converting `$Event`s to `Event`s. This should be done carefully,
as it may introduce bugs.

Const variable declarations in events are permitted. Arguments to instructions
and conditions functions can use those values, or generally any values.

An example of constants and simple expressions:

```js
$Event(13105265, Restart, function() {
    const bossEntity = 3100395;
    WaitFor(CharacterAIState(bossEntity, AIStateType.Combat));
    SetSpEffect(bossEntity, 15999);
    WaitFixedTimeSeconds(60 * buffMinutes[bossEntity]);
    ClearSpEffect(bossEntity, 15999);
});
```

Calls to standalone functions are also left as-is, as long as their names start
with a lowercase character. **Big warning: if/goto statements ignore these
statements when determining how many lines to skip**, so do not use function
calls unless you double-check the compilation preview to make sure skip offsets
do not get messed up.
