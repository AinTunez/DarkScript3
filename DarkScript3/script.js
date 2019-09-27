var _event = void 0;
var Default = REST.Default;
var End = REST.End;
var Restart = REST.Restart;

function Event(id, restBehavior, params, instructions) {
    var evt = new EVENT();
    evt.ID = id;
    evt.RestBehavior = restBehavior;
    _event = evt;
    instructions();
    EVD.Events.Add(_event);
    return _event;
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

    var argOut = $$$_host.newArr(args.length);
    for (var i = 0; i < args.length; i++) {
        argOut[i] = args[i];
    }

    if (layer) {
        var ins = Scripter.MakeInstruction(bank, index, layer, argOut);
        _event.Instructions.Add(ins);
        return ins;
    } else {
        var ins = Scripter.MakeInstruction(bank, index, argOut);
        _event.Instructions.Add(ins);
        return ins;
    }
}

function LAYERS(...args) {
    var layer = 0;
    for (var i = 0; i < args.length; i++)
        layer |= 1 << args[i];
    return { layerValue: layer };
} 