﻿var rconParser;
var eventParser;

var plugin = {
    author: 'RaidMax, Chase',
    version: 0.2,
    name: 'Plutonium T4 Parser',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser(this.name);
        eventParser = manager.GenerateDynamicEventParser(this.name);
        
        rconParser.Configuration.CommandPrefixes.Kick         = 'clientkick {0}';
        rconParser.Configuration.CommandPrefixes.Ban          = 'clientkick {0}';
        rconParser.Configuration.CommandPrefixes.TempBan      = 'clientkick {0}';
        rconParser.Configuration.CommandPrefixes.RConResponse = '\xff\xff\xff\xffprint\n';
        rconParser.Configuration.GuidNumberStyle              = 7; // Integer
        
        rconParser.Version  = 'Plutonium T4';
        rconParser.GameName = 5; // T4

        eventParser.Configuration.GuidNumberStyle = 7; // Integer
        eventParser.Configuration.GameDirectory   = 'raw';
 
        eventParser.Version  = 'Plutonium T4';
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};
