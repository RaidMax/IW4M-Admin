var rconParser;
var eventParser;

var plugin = {
    author: 'FrenchFry, RaidMax',
    version: 0.9,
    name: 'CoD4x Parser',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser(this.name);
        eventParser = manager.GenerateDynamicEventParser(this.name);

        rconParser.Configuration.StatusHeader.Pattern = 'num +score +ping +playerid +steamid +name +lastmsg +address +qport +rate *';
        rconParser.Configuration.Status.Pattern = '^ *([0-9]+) +-?([0-9]+) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]{16,32})|0) +([[0-9]+|0]) +(.{0,34}) +([0-9]+) +(\\d+\\.\\d+\\.\\d+.\\d+\\:-*\\d{1,5}|0+.0+:-*\\d{1,5}|loopback|bot) +(-*[0-9]+) +([0-9]+) *$';
        rconParser.Configuration.Status.AddMapping(104, 6); // RConName
        rconParser.Configuration.Status.AddMapping(105, 8); // RConIPAddress
        rconParser.Configuration.CommandPrefixes.RConResponse = '\xff\xff\xff\xffprint\n';

        rconParser.Configuration.Dvar.Pattern = '^"(.+)" is: "(.+)?" default: "(.+)?" info: "(.+)?"$';
        rconParser.Configuration.Dvar.AddMapping(109, 2); // DVAR latched value
        rconParser.Configuration.Dvar.AddMapping(110, 4); // dvar info
        rconParser.Configuration.GuidNumberStyle = 7; // Integer
        rconParser.Configuration.NoticeLineSeparator = '. '; // CoD4x does not support \n in the client notice
        rconParser.Configuration.DefaultRConPort = 28960;
        rconParser.Version = 'CoD4 X - win_mingw-x86 build 1056 Dec 12 2020';
        rconParser.GameName = 1; // IW3

        eventParser.Configuration.GameDirectory = 'main';
        eventParser.Configuration.GuidNumberStyle = 7; // Integer
        eventParser.Version = 'CoD4 X - win_mingw-x86 build 1056 Dec 12 2020';
        eventParser.GameName = 1; // IW3
        eventParser.URLProtocolFormat = 'cod4://{{ip}}:{{port}}';
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};
