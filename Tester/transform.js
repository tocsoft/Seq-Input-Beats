
var msg = message.Message;

while (msg.length > 0) {
    var match = msg.match(/\[ ([^ ]*) = (.*?) \](?: |\n)?/m)
    if (!match) {
        break;
    }

    msg = msg.replace(match[0], '')

    const key = match[1];
    const value = match[2];

    if (key == 'msg') {
        message.Message = value;
    } else if (key == 'date') {
        message.UtcTimestamp = new Date(value);
    } else {
        message.Properties[key] = value;
    }
}

// further expand properties collection!!!

if (msg.length > 0) {
    message.Exception = msg;
}
