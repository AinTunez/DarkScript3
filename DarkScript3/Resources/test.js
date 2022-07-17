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
    Label0();
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
    Goto(S0);
L0_:
    NoOp();
S0_:
    SetEventFlag(100, OFF);
});
