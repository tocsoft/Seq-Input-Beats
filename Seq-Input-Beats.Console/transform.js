var msg = message.Message;

var properties = ""
var propBag = {}
while (msg.length > 0) {
    var match = msg.match(/\[ ([^ ]*) = ((?:.|\n)*?) \](?: |\n)?/m)
    if (!match) {
        break;
    }

    msg = msg.replace(match[0], '')

    const key = match[1];
    const value = match[2];
    try
    {
        if (key == 'msg') {
            message.Message = value;
        } else if (key == 'date') {
            value = value.replace(",", ".")
            message.UtcTimestamp = value;
        } else if (key == 'properties') {
            properties = value;
        } else {
            propBag[key] = value;
        }
    } catch (err)
    {
        propBag[key] = value;
    }
}

// trim curlies
properties = properties.substring(properties.indexOf('{') + 1, properties.lastIndexOf('}'))
while (properties.length > 0) {
    var match = properties.match(/ ?(.*?): (?:null|"(.*?)"),?/m)
    if (!match) {
        break;
    }

    properties = properties.replace(match[0], '')
    const key = match[1];
    const value = match[2];
    message.Properties[key] = value;
}

for (var k in propBag) {
    if (propBag.hasOwnProperty(k)) {
        message.Properties[k] = propBag[k]
    }
}

// further expand properties collection!!!

if (msg.length > 0) {
    message.Exception = msg;
}
