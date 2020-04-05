const Default = REST.Default;
const End = REST.End;
const Restart = REST.Restart;

var _event = void 0;

function Event(id, restBehavior, instructions) {
    var evt = new EVENT();
    evt.ID = id;
    evt.RestBehavior = restBehavior;
    
    Console.WriteLine("Adding Event " + id);

    _event = evt;
    instructions.apply(this, _GetArgs(instructions));
    _event = void 0;

    EVD.Events.Add(evt);
    return evt;
}

function _GetArgs(func) {
    var start = func.toString().indexOf("(");
    var end = func.toString().indexOf(")");
    var args = func.toString().substring(start, end).replace("(", "").replace(")", "");
    return args.split(/\s*,\s*/).map(arg => arg);
}

function _Instruction(bank, index, args) {
    if (!_event) return;

    var layer = void 0;
    if (args.length) {
        var lastArg = args.pop();
        if (lastArg.layerValue) {
            layer = lastArg.layerValue;
        } else {
            args.push(lastArg);
        }
    }

    Console.WriteLine(args.length);
    var argOut = $$$_host.newArr(args.length);
    for (var i = 0; i < args.length; i++) {
        argOut[i] = args[i];
    }

    var ins = void 0;
    if (layer) {
        ins = Scripter.MakeInstruction(_event, bank, index, layer, argOut);
    } else {
        ins = Scripter.MakeInstruction(_event, bank, index, argOut);
    }

    return ins;
}

function $LAYERS(...args) {
    var layer = 0;
    for (var i = 0; i < args.length; i++)
        layer |= 1 << args[i];
    return { layerValue: layer };
} 