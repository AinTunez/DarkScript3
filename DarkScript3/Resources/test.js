// Should produce warnings but otherwise work
$Event(1, Default, function() {
    Goto(L0);
L0: L1:
    Goto(L0);
L2: L1:
    NoOp();
});

$Event(2, Default, function() {
    if (badcond) {
        SetEventFlag(1, ON);
    }
    worsecond = worsecond;
    EndIf(worstcond.Passed);
});

// More cases
$Event(100, Default, function() {
    if (EventFlag(1)) {
    }
    SetEventFlag(1, ON);
    if (InArea(10000, 15)) {
    } else {
        SetEventFlag(2, ON);
    }
    if (InArea(10000, 30)) {
    } else {
    }
    SetEventFlag(3, ON);
});

$Event(101, Default, function() {
    if (EventFlag(100)) {
        if (!EventFlag(150)) {
            SetEventFlag(150, ON);
        }
        if (EventFlag(200)) {
            SetEventFlag(300, ON);
        } else {
            SetEventFlag(400, ON);
        }
    }
});

$Event(102, Default, function() {
    c = EventFlag(10);
    if (EventFlag(99)) {
        c2 = c && EventFlag(20);
        WaitFor(c2);
    } else {
        WaitFor(c);
    }
    // TODO: This causes compilation output to differ (but in a safe way)
    // Label0();
});

$Event(103, Default, function() {
    // 1. Comments
    if (EventFlag(99)) {  // 2. Wait for state change
        WaitFor(/* 3. negated */ !EventFlag(99));
    } else {
        WaitFor(EventFlag(99));
    }  // 4. If state change
L0:  // 5. Jump target
    /* 6. Then the event
     * flag is set */
S0: // 7. End
    NoOp();
});

function setFirstFlagOnly(start, end) {
    SetEventFlag(start, ON);
    BatchSetEventFlags(start + 1, end, OFF);
}

$Event(104, Default, function() {
    const exampleRange = [5100, 5119];
    EndIf(!EventFlag(exampleRange[0]));
    setFirstFlagOnly(exampleRange[1], exampleRange[1]);
});

$Event(105, Default, function () {
    const amt = 9;
    if (EventFlag(500)) {
        setFirstFlagOnly(500, 500 + amt);
    }
    GotoIf(S0, EventFlag(600))
    setFirstFlagOnly(600, 600 + amt);
S0:
    NoOp();
});

$Event(106, Default, function () {
L0:
    SetEventFlag(100, ON);
    Goto(hello);
L0_:
    NoOp();
hello_:
    SetEventFlag(100, OFF);
});

$Event(107, Default, function () {
    // Keep nesting
    if (EventFlag(100)) {
        SetEventFlag(100, OFF);
    } else if (EventFlag(200)) {
        SetEventFlag(200, OFF);
    } else {
        SetEventFlag(300, OFF);
    }
});

$Event(108, Default, function () {
    // This case should not be rewritten as if/else. Elden Ring 950 or 11052860
    GotoIf(L1, EventFlag(100));
    GotoIf(L2, EventFlag(200));
    GotoIf(L3, EventFlag(300));
    Goto(L10);
L3:
    Goto(L10);
L2:
    Goto(L10);
L1:
    Goto(L10);
L10:
    SetEventFlag(1000, ON);
});

$Event(109, Default, function () {
    const ids = [10, 20, 30];
    for (let i = 1; i <= 2; i++) {
        for (let j = i; j <= 2; j++) {
            myCond |= EventFlag(10 * i);
            myCond |= EventFlag(10 * i + j);
        }
    }
    WaitFor(myCond);
    if (EventFlag(99)) {
        // Nested with if statements
        for (; false;) {
            if (EventFlag(77)) {
                cond2 &= EventFlag(55);
                WaitFor(cond2);
            }
        }
    }
    // of loop and repeated ReserveSkip
    for (const id of ids) {
        if (!EventFlag(id)) {
            WaitFor(EventFlag(id));
        }
    }
});

$Event(110, Default, function () {
    // TODO: Make this case repack cleanly.
    if (EventFlag(500)) {
        if (EventFlag(500)) {
            SetEventFlag(500, OFF);
        }
    }
});

/*
// TODO (?) This fails
$Event(111, Default, function () {
    // Case of goto without skip, as in DS3 20005940
    if (!HasHollowArenaMatchType(HollowArenaMatchType.TwoVersusThree)) {
        DisplayBanner(TextBannerType.HollowArenaWin);
    }
L0:
    EndEvent();
});
*/
